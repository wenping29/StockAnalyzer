<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'
import * as echarts from 'echarts'
import type { KLinePoint } from '../types'

// 折线趋势图：用于场外基金净值等只有收盘值的序列
const props = defineProps<{ points: KLinePoint[]; valueLabel?: string }>()

const el = ref<HTMLElement>()
let chart: echarts.ECharts | null = null

function render() {
  if (!chart || !props.points.length) return
  const pts = props.points
  chart.setOption({
    animation: false,
    tooltip: { trigger: 'axis' },
    grid: { left: 60, right: 20, top: 20, bottom: 50 },
    xAxis: {
      type: 'category',
      data: pts.map((p) => p.time),
      axisLine: { lineStyle: { color: '#c0c4cc' } },
    },
    yAxis: { scale: true, splitLine: { lineStyle: { color: '#f0f0f0' } } },
    dataZoom: [
      { type: 'inside', start: 60, end: 100 },
      { type: 'slider', bottom: 8, height: 18, start: 60, end: 100 },
    ],
    series: [
      {
        name: props.valueLabel ?? '净值',
        type: 'line',
        data: pts.map((p) => p.close),
        showSymbol: false,
        lineStyle: { color: '#409eff', width: 2 },
        areaStyle: {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: 'rgba(64,158,255,0.25)' },
            { offset: 1, color: 'rgba(64,158,255,0.02)' },
          ]),
        },
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
