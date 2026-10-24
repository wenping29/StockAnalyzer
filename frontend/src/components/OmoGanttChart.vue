<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import * as echarts from 'echarts'
import type { OmoItem } from '../types'

// 央行逆回购甘特图：x 轴为时间，每笔操作画成 [操作日 → 到期日] 的浮动柱，
// 柱高 = 投放量（亿元），按期限分系列（图例可开关）
const props = defineProps<{ items: OmoItem[] }>()

const el = ref<HTMLElement>()
let chart: echarts.ECharts | null = null

const TERM_ORDER = ['隔夜', '7天', '14天', '28天', '63天', '91天', '182天', '3个月', '6个月', '9个月', '1年']
const TERM_COLORS: Record<string, string> = {
  '隔夜': '#f97316',
  '7天': '#c23531',
  '14天': '#e8a0a8',
  '21天': '#f7ba2a',
  '28天': '#b8860b',
  '3个月': '#8b5cf6',
  '6个月': '#6366f1',
  '9个月': '#3b82f6',
  '1年': '#0ea5e9',
}

const terms = computed(() => {
  const present = [...new Set(props.items.map((i) => i.term))]
  return present.sort((a, b) => {
    const ia = TERM_ORDER.indexOf(a), ib = TERM_ORDER.indexOf(b)
    return (ia < 0 ? 99 : ia) - (ib < 0 ? 99 : ib)
  })
})

const series = computed(() =>
  terms.value.map((term) => ({
    name: term,
    type: 'custom' as const,
    renderItem: renderItem as any,
    encode: { x: [0, 1], y: 2 },
    clip: true,
    itemStyle: { color: TERM_COLORS[term] ?? '#909399', opacity: 0.85 },
    data: props.items
      .filter((i) => i.term === term && i.amount > 0)
      .map((i) => [Date.parse(i.date), Date.parse(i.maturityDate) + 86400000, i.amount, i.term, i.rate]),
  })))

function renderItem(params: { dataIndex: number }, api: { value(v: number): number; coord(v: number[]): number[]; style(): object }) {
  const start = api.coord([api.value(0), api.value(2)])
  const end = api.coord([api.value(1), api.value(2)])
  const zeroY = api.coord([api.value(0), 0])[1]
  return {
    type: 'rect',
    shape: {
      x: start[0],
      y: start[1],
      width: Math.max(end[0] - start[0], 2),
      height: Math.max(zeroY - start[1], 1),
    },
    style: api.style(),
  }
}

function render() {
  if (!chart || !props.items.length) return
  const today = Date.now()
  chart.setOption({
    animation: false,
    tooltip: {
      trigger: 'item',
      formatter: (p: { value: [number, number, number, string, number | null] }) => {
        const [s, e, amt, term, rate] = p.value
        const fmt = (t: number) => new Date(t).toISOString().slice(0, 10)
        return [
          `<b>${term} 逆回购</b>`,
          `投放日：${fmt(s)}`,
          `到期日：${fmt(e - 86400000)}`,
          `投放量：${amt.toLocaleString()} 亿元`,
          rate != null ? `操作利率：${rate}%` : '',
        ].filter(Boolean).join('<br/>')
      },
    },
    legend: { top: 0, data: terms.value },
    grid: { left: 70, right: 20, top: 32, bottom: 44 },
    xAxis: {
      type: 'time',
      axisLine: { lineStyle: { color: '#c0c4cc' } },
      axisLabel: { formatter: (v: number) => new Date(v).toISOString().slice(0, 10) },
    },
    yAxis: {
      name: '亿元',
      splitLine: { lineStyle: { color: '#f0f0f0' } },
      axisLabel: { formatter: (v: number) => v.toLocaleString() },
    },
    dataZoom: [{ type: 'inside' }],
    series: series.value.map((s, i) => i === 0
      ? { ...s, markLine: { symbol: 'none', lineStyle: { color: '#409eff', type: 'dashed' }, label: { formatter: '今日' }, data: [{ xAxis: today }] } }
      : s),
  }, { notMerge: true })
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

watch(() => props.items, render, { deep: true })
</script>

<template>
  <div v-if="items.length" ref="el" style="width: 100%; height: 360px" />
  <div v-else class="empty-hint">暂无数据：点击右上角"同步最新数据"拉取央行公告（首次约需 3~5 分钟）</div>
</template>

<style scoped>
.empty-hint { color: #c0c4cc; font-size: 13px; text-align: center; padding: 60px 0; }
</style>
