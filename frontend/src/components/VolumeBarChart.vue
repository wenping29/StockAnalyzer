<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'
import * as echarts from 'echarts'
import type { MarketVolumePoint } from '../types'

// 两市成交量趋势：沪/深堆叠柱状图（单位：亿元）
const props = defineProps<{ points: MarketVolumePoint[] }>()

const el = ref<HTMLElement>()
let chart: echarts.ECharts | null = null

function render() {
  if (!chart || !props.points.length) return
  chart.setOption({
    animation: false,
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'shadow' },
      valueFormatter: (v: number) => `${v} 亿`,
    },
    legend: { top: 0, data: ['沪市', '深市'] },
    grid: { left: 70, right: 20, top: 32, bottom: 44 },
    xAxis: {
      type: 'category',
      data: props.points.map((p) => p.date.slice(5)),
      axisLine: { lineStyle: { color: '#c0c4cc' } },
    },
    yAxis: {
      axisLabel: { formatter: (v: number) => `${(v / 10000).toFixed(1)}万亿` },
      splitLine: { lineStyle: { color: '#f0f0f0' } },
    },
    dataZoom: [{ type: 'inside' }],
    series: [
      {
        name: '沪市',
        type: 'bar',
        stack: 'total',
        barMaxWidth: 22,
        itemStyle: { color: '#c23531' },
        data: props.points.map((p) => p.sh),
      },
      {
        name: '深市',
        type: 'bar',
        stack: 'total',
        barMaxWidth: 22,
        itemStyle: { color: '#e8a0a8' },
        data: props.points.map((p) => p.sz),
      },
    ],
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

watch(() => props.points, render)
</script>

<template>
  <div ref="el" class="chart-box-sm" />
</template>
