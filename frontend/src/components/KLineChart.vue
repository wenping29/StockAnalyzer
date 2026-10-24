<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'
import * as echarts from 'echarts'
import type { KLinePoint } from '../types'

const props = defineProps<{
  points: KLinePoint[]
  /** 均线周期列表（如 [5, 10, 20, 30]）；为空或不传则不画均线 */
  maPeriods?: number[]
  /** 数据缩放初始窗口起点（百分比），默认自动：多于 120 根时展示最近 120 根 */
  windowStart?: number
  /** 数据缩放初始窗口终点（百分比），默认 100 */
  windowEnd?: number
}>()

const el = ref<HTMLElement>()
let chart: echarts.ECharts | null = null

const UP = '#ef232a'      // A 股习惯：涨红
const DOWN = '#14b143'    // 跌绿

/** 均线配色（浅色背景，互不混淆） */
const MA_COLORS: Record<number, string> = {
  5: '#f5a623',   // 琥珀
  10: '#7e57c2',  // 紫
  20: '#1e88e5',  // 蓝
  30: '#00897b',  // 青
}

/** 简单移动平均（SMA）：前 n-1 个点为 null，折线自然断开 */
function maData(n: number, pts: KLinePoint[]): (number | null)[] {
  return pts.map((_, i) => {
    if (i < n - 1) return null
    let sum = 0
    for (let j = i - n + 1; j <= i; j++) sum += pts[j].close
    return Math.round((sum / n) * 100) / 100
  })
}

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

  const maLines = (props.maPeriods ?? []).map((n) => ({
    name: `MA${n}`,
    type: 'line',
    data: maData(n, pts),
    smooth: true,
    symbol: 'none',
    lineStyle: { width: 1.2, color: MA_COLORS[n] ?? '#606266' },
    itemStyle: { color: MA_COLORS[n] ?? '#606266' },
    z: 4,
  }))

  // 初始窗口：数据量大时默认只展示最近 120 根（大数据量 K 线可拖动滑块/滚轮缩放）
  const visible = 120
  const start = props.windowStart ?? (pts.length > visible ? ((pts.length - visible) / pts.length) * 100 : 0)
  const end = props.windowEnd ?? 100
  const top = maLines.length ? 30 : 18

  chart.setOption({
    animation: false,
    legend: maLines.length
      ? {
          show: true,
          top: 0,
          left: 8,
          itemWidth: 14,
          itemHeight: 2,
          itemGap: 10,
          textStyle: { fontSize: 11, color: '#606266' },
          data: maLines.map((l) => l.name),
        }
      : { show: false },
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'cross' },
      backgroundColor: 'rgba(255,255,255,0.96)',
      borderColor: '#ddd',
      textStyle: { color: '#303133', fontSize: 12 },
    },
    axisPointer: { link: [{ xAxisIndex: 'all' }] },
    grid: [
      { left: 60, right: 20, top, height: '55%' },
      { left: 60, right: 20, top: '74%', height: '14%' },
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
      { type: 'inside', xAxisIndex: [0, 1], start, end },
      { type: 'slider', xAxisIndex: [0, 1], bottom: 8, height: 18, start, end },
    ],
    series: [
      {
        name: 'K线',
        type: 'candlestick',
        data: kdata,
        itemStyle: { color: UP, color0: DOWN, borderColor: UP, borderColor0: DOWN },
      },
      ...maLines,
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
