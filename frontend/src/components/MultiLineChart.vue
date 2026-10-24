<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'
import * as echarts from 'echarts'

// 通用多序列折线图：支持阶梯线（存准率/LPR）与双 Y 轴（series 指定 yAxis: 0/1）
const props = defineProps<{
  times: string[]
  series: { name: string; data: (number | null)[]; color?: string; dashed?: boolean; yAxis?: 0 | 1 }[]
  yLabel?: string
  y2Label?: string
  step?: boolean
  unit?: string
}>()

const el = ref<HTMLElement>()
let chart: echarts.ECharts | null = null

const PALETTE = ['#409eff', '#e6a23c', '#67c23a', '#f56c6c', '#909399']

const hasDualAxis = () => props.series.some((s) => s.yAxis === 1)

function render() {
  if (!chart || !props.times.length) return
  const dual = hasDualAxis()
  chart.setOption({
    animation: false,
    tooltip: {
      trigger: 'axis',
      valueFormatter: (v: number) => `${v}${props.unit ?? ''}`,
    },
    legend: { top: 0 },
    grid: { left: 60, right: dual ? 60 : 20, top: 34, bottom: 46 },
    xAxis: {
      type: 'category',
      data: props.times,
      boundaryGap: false,
      axisLine: { lineStyle: { color: '#c0c4cc' } },
    },
    yAxis: [
      {
        name: props.yLabel ?? '',
        scale: true,
        axisLabel: { formatter: (v: number) => `${v}${props.unit ?? ''}` },
        splitLine: { lineStyle: { color: '#f0f0f0' } },
      },
      ...(dual
        ? [{
            name: props.y2Label ?? '',
            scale: true,
            axisLabel: { formatter: (v: number) => `${v}` },
            splitLine: { show: false },
          }]
        : []),
    ],
    dataZoom: [{ type: 'inside', start: 0, end: 100 }],
    series: props.series.map((s, i) => ({
      name: s.name,
      type: 'line',
      yAxisIndex: s.yAxis ?? 0,
      step: props.step ? 'end' : false,
      showSymbol: false,
      connectNulls: true,
      lineStyle: {
        color: s.color ?? PALETTE[i % PALETTE.length],
        width: 2,
        type: s.dashed ? 'dashed' : 'solid',
      },
      itemStyle: { color: s.color ?? PALETTE[i % PALETTE.length] },
      data: s.data,
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

watch(() => [props.times, props.series], render)
</script>

<template>
  <div ref="el" class="chart-box-sm" />
</template>
