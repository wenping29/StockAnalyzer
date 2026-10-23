<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'
import * as echarts from 'echarts'
import type { MoneyFlowItem } from '../types'

// 资金流柱状图：主力/大单/中单/小单每日净流入（单位：亿元，流入为正显示红色、流出绿色由正负轴体现）
const props = defineProps<{ items: MoneyFlowItem[] }>()

const el = ref<HTMLElement>()
let chart: echarts.ECharts | null = null

const SERIES = [
  { name: '主力', key: 'mainNet', color: '#c23531' },
  { name: '大单', key: 'largeNet', color: '#e87c25' },
  { name: '中单', key: 'midNet', color: '#ca8622' },
  { name: '小单', key: 'smallNet', color: '#5470c6' },
] as const

function yi(v: number) {
  return +(v / 1e8).toFixed(2)
}

function render() {
  if (!chart || !props.items.length) return
  chart.setOption({
    animation: false,
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'shadow' },
      valueFormatter: (v: number) => `${v} 亿`,
    },
    legend: { top: 0 },
    grid: { left: 60, right: 20, top: 36, bottom: 30 },
    xAxis: {
      type: 'category',
      data: props.items.map((i) => i.date),
      axisLine: { lineStyle: { color: '#c0c4cc' } },
    },
    yAxis: {
      axisLabel: { formatter: (v: number) => `${v}亿` },
      splitLine: { lineStyle: { color: '#f0f0f0' } },
    },
    series: SERIES.map((s) => ({
      name: s.name,
      type: 'bar',
      emphasis: { focus: 'series' },
      itemStyle: { color: s.color },
      data: props.items.map((i) => yi(i[s.key])),
    })),
  })
}

onMounted(() => {
  chart = echarts.init(el.value!)
  render()
  window.addEventListener('resize', resize)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', resize)
  chart?.dispose()
})

function resize() {
  chart?.resize()
}

watch(() => props.items, render)
</script>

<template>
  <div ref="el" class="chart-box-sm" />
</template>
