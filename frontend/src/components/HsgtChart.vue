<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'
import * as echarts from 'echarts'
import type { HsgtPoint } from '../types'

// 北向/南向资金：双轴柱状图（左轴北向成交额，右轴南向净流入，正红负绿）
const props = defineProps<{ points: HsgtPoint[] }>()

const el = ref<HTMLElement>()
let chart: echarts.ECharts | null = null

const UP = '#ef232a'
const DOWN = '#14b143'

function render() {
  if (!chart || !props.points.length) return
  chart.setOption({
    animation: false,
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'shadow' },
      valueFormatter: (v: number) => `${v} 亿`,
    },
    legend: { top: 0, data: ['北向成交额', '南向净流入'] },
    grid: { left: 70, right: 70, top: 32, bottom: 44 },
    xAxis: {
      type: 'category',
      data: props.points.map((p) => p.date.slice(5)),
      axisLine: { lineStyle: { color: '#c0c4cc' } },
    },
    yAxis: [
      {
        name: '北向成交',
        axisLabel: { formatter: (v: number) => `${Math.round(v)}亿` },
        splitLine: { lineStyle: { color: '#f0f0f0' } },
      },
      {
        name: '南向净流入',
        axisLabel: { formatter: (v: number) => `${v}亿` },
        splitLine: { show: false },
      },
    ],
    dataZoom: [{ type: 'inside' }],
    series: [
      {
        name: '北向成交额',
        type: 'bar',
        yAxisIndex: 0,
        barMaxWidth: 18,
        itemStyle: { color: '#7d9ec9' },
        data: props.points.map((p) => p.northAmount),
      },
      {
        name: '南向净流入',
        type: 'bar',
        yAxisIndex: 1,
        barMaxWidth: 18,
        data: props.points.map((p) => ({
          value: p.southNetInflow,
          itemStyle: { color: p.southNetInflow >= 0 ? UP : DOWN },
        })),
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
