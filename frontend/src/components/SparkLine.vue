<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'
import * as echarts from 'echarts'

// 迷你走势图（无坐标轴，用于指数卡片）
const props = defineProps<{ data: number[]; up?: boolean }>()

const el = ref<HTMLElement>()
let chart: echarts.ECharts | null = null

function render() {
  if (!chart || props.data.length < 2) return
  const color = props.up ? '#ef232a' : '#14b143'
  chart.setOption({
    animation: false,
    grid: { left: 0, right: 0, top: 2, bottom: 2 },
    xAxis: { type: 'category', show: false, boundaryGap: false, data: props.data.map((_, i) => i) },
    yAxis: { type: 'value', show: false, min: 'dataMin', max: 'dataMax' },
    series: [
      {
        type: 'line',
        data: props.data,
        showSymbol: false,
        lineStyle: { color, width: 1.5 },
        areaStyle: {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: color + '33' },
            { offset: 1, color: color + '05' },
          ]),
        },
      },
    ],
  })
}

onMounted(() => {
  chart = echarts.init(el.value!)
  render()
})

onBeforeUnmount(() => chart?.dispose())

function resize() {
  chart?.resize()
}
window.addEventListener('resize', resize)
onBeforeUnmount(() => window.removeEventListener('resize', resize))

watch(() => props.data, render)
</script>

<template>
  <div ref="el" style="width: 100%; height: 46px" />
</template>
