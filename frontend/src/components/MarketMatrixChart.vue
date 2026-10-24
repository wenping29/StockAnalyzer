<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'
import * as echarts from 'echarts'
import type { IndexItem } from '../types'

const props = defineProps<{ items: IndexItem[] }>()
const emit = defineEmits<{ (e: 'select', item: IndexItem): void }>()

const el = ref<HTMLElement>()
let chart: echarts.ECharts | null = null

/** 涨跌幅色带（A 股习惯：红涨绿跌；平盘灰色） */
const BUCKETS: { min: number; bg: string; fg: string }[] = [
  { min: 3, bg: '#a93226', fg: '#ffffff' },
  { min: 1.5, bg: '#cd6155', fg: '#ffffff' },
  { min: 0.5, bg: '#e6b0aa', fg: '#7b241c' },
  { min: -0.5, bg: '#d5dbdb', fg: '#566573' },
  { min: -1.5, bg: '#a9dfbf', fg: '#1e8449' },
  { min: -3, bg: '#52be80', fg: '#ffffff' },
  { min: -Infinity, bg: '#1e8449', fg: '#ffffff' },
]
function bucketOf(pct: number) {
  return BUCKETS.find((b) => pct >= b.min)!
}

function render() {
  if (!chart || !props.items.length) return
  const groups = new Map<string, IndexItem[]>()
  for (const it of props.items) {
    if (!groups.has(it.group)) groups.set(it.group, [])
    groups.get(it.group)!.push(it)
  }

  const data = [...groups.entries()].map(([group, list]) => ({
    name: group,
    // 分组权重 = 组内成交额合计
    value: list.reduce((s, i) => s + Math.max(i.amount, 0), 0),
    children: list.map((i) => {
      const b = bucketOf(i.changePct)
      return {
        name: i.name,
        // 成交额为 0（数据缺失）时保底一个最小权重，保证色块可见
        value: Math.max(i.amount, 1),
        code: i.code,
        price: i.price,
        changePct: i.changePct,
        itemStyle: { color: b.bg, borderColor: '#ffffff', borderWidth: 1 },
        label: { color: b.fg },
      }
    }),
  }))

  chart.setOption({
    animation: false,
    tooltip: {
      backgroundColor: 'rgba(255,255,255,0.96)',
      borderColor: '#ddd',
      textStyle: { color: '#303133', fontSize: 12 },
      formatter: (info: any) => {
        const d = info.data
        if (!d || d.changePct == null) return `<b>${info.name}</b>`
        const color = d.changePct >= 0 ? '#ef232a' : '#14b143'
        const sign = d.changePct >= 0 ? '+' : ''
        return (
          `<b>${d.name}（${d.code}）</b><br/>` +
          `最新价：${d.price.toFixed(2)}<br/>` +
          `涨跌幅：<span style="color:${color};font-weight:600">${sign}${d.changePct.toFixed(2)}%</span><br/>` +
          `成交额：${d.value.toFixed(1)} 亿`
        )
      },
    },
    series: [
      {
        type: 'treemap',
        name: '股市矩阵',
        roam: false,
        nodeClick: false,
        leafDepth: 2,
        breadcrumb: {
          show: true,
          top: 0,
          left: 8,
          height: 18,
          itemStyle: { color: '#f2f4f8', borderColor: '#dcdfe6', textStyle: { fontSize: 11 } },
          textStyle: { fontSize: 11, color: '#606266' },
        },
        levels: [
          // 一级：分组（A股宽基 / 风格与主题 / 跨境）
          { itemStyle: { borderColor: '#ffffff', borderWidth: 3, gapWidth: 3, color: '#f2f4f8' } },
          // 二级：单个指数色块（颜色按涨跌幅，node 级 itemStyle 覆盖）
          { itemStyle: { borderColor: '#ffffff', borderWidth: 1.5, gapWidth: 1.5 } },
        ],
        upperLabel: { show: true, height: 20, color: '#606266', fontSize: 12, fontWeight: 600 },
        label: {
          show: true,
          fontSize: 12,
          lineHeight: 16,
          formatter: (p: any) =>
            p.data?.changePct != null
              ? `${p.name}\n${p.data.changePct >= 0 ? '+' : ''}${p.data.changePct.toFixed(2)}%`
              : p.name,
        },
        itemStyle: { borderColor: '#ffffff' },
        data,
      },
    ],
  })
}

onMounted(() => {
  chart = echarts.init(el.value!)
  render()
  chart.on('click', (p: any) => {
    const item = props.items.find((i) => i.code === p?.data?.code)
    if (item) emit('select', item)
  })
  window.addEventListener('resize', resize)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', resize)
  chart?.dispose()
})

function resize() {
  chart?.resize()
}

// 指数清单刷新（如切换页面数据更新）时重绘
watch(() => props.items, render, { deep: false })
</script>

<template>
  <div ref="el" class="chart-box-matrix" />
</template>

<style scoped>
.chart-box-matrix { width: 100%; height: 480px; }
@media (max-width: 768px) {
  .chart-box-matrix { height: 380px; }
}
</style>
