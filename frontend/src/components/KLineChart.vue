<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'
import * as echarts from 'echarts'
import type { KLinePoint } from '../types'

const props = defineProps<{ points: KLinePoint[] }>()

const el = ref<HTMLElement>()
let chart: echarts.ECharts | null = null

const UP = '#ef232a'      // A 股习惯：涨红
const DOWN = '#14b143'    // 跌绿

function render() {
  if (!chart || !props.points.length) return
  const pts = props.points
  const times = pts.map((p) => p.time)
  // ECharts K 线数据顺序：[开盘, 收盘, 最低, 最高]
  const kdata = pts.map((p) => [p.open, p.close, p.low, p.high])
  const volumes = pts.map((p, i) => ({
    value: p.volume,
    itemStyle: { color: p.close >= (pts[i - 1]?.close ?? p.open) ? UP : DOWN },
  }))

  chart.setOption({
    animation: false,
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'cross' },
      backgroundColor: 'rgba(255,255,255,0.96)',
      borderColor: '#ddd',
      textStyle: { color: '#303133', fontSize: 12 },
    },
    axisPointer: { link: [{ xAxisIndex: 'all' }] },
    grid: [
      { left: 60, right: 20, top: 20, height: '58%' },
      { left: 60, right: 20, top: '76%', height: '14%' },
    ],
    xAxis: [
      { type: 'category', data: times, boundaryGap: true, axisLine: { lineStyle: { color: '#c0c4cc' } } },
      { type: 'category', gridIndex: 1, data: times, boundaryGap: true, axisLabel: { show: false }, axisTick: { show: false } },
    ],
    yAxis: [
      { scale: true, splitLine: { lineStyle: { color: '#f0f0f0' } } },
      { gridIndex: 1, axisLabel: { show: false }, splitLine: { show: false } },
    ],
    dataZoom: [
      { type: 'inside', xAxisIndex: [0, 1], start: 60, end: 100 },
      { type: 'slider', xAxisIndex: [0, 1], bottom: 8, height: 18, start: 60, end: 100 },
    ],
    series: [
      {
        name: 'K线',
        type: 'candlestick',
        data: kdata,
        itemStyle: { color: UP, color0: DOWN, borderColor: UP, borderColor0: DOWN },
      },
      {
        name: '成交量',
        type: 'bar',
        xAxisIndex: 1,
        yAxisIndex: 1,
        data: volumes,
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

watch(() => props.points, render, { deep: false })
</script>

<template>
  <div ref="el" class="chart-box" />
</template>
