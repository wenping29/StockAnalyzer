<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'
import * as echarts from 'echarts'
import type { IndexTrendData } from '../types'

const props = defineProps<{ data: IndexTrendData }>()

const el = ref<HTMLElement>()
let chart: echarts.ECharts | null = null

const UP = '#ef232a'    // A 股习惯：涨红
const DOWN = '#14b143'  // 跌绿
const AVG = '#f5a623'   // 均价线（琥珀）

/** 断轴标记：双斜线（∥ 形），绘制在价格轴空档上下边缘 */
const BREAK_PATH = 'path://M-4,-4.5 L0,4.5 M0,-4.5 L4,4.5'

/** 分时图固定时间刻度（命中才显示，其余隐藏；16:00 为港股收市） */
const TICKS = new Set(['09:30', '10:30', '11:30', '13:00', '14:00', '15:00', '16:00'])

function render() {
  if (!chart) return
  const d = props.data
  const pts = d.points
  if (!pts?.length) {
    chart.clear()
    return
  }
  const prev = d.prevClose > 0 ? d.prevClose : 0
  const times = pts.map((p) => p.time)

  // ---------- 数据范围与留白 ----------
  const prices = pts.map((p) => p.price)
  const avgs = pts.map((p) => p.avg)
  let rawMin = Math.min(...prices, ...avgs)
  let rawMax = Math.max(...prices, ...avgs)
  if (!isFinite(rawMin) || !isFinite(rawMax)) return
  let span = rawMax - rawMin
  if (span <= 0) span = Math.max(Math.abs(rawMax) * 0.001, 1e-4)
  let lo = rawMin - span * 0.1
  let hi = rawMax + span * 0.1
  const range = hi - lo

  // ---------- 断轴：昨收落在数据区间外时，压缩中间空档 ----------
  // 变换后坐标系 t(v)：区间内恒等，空档压缩为固定宽度 gapW 的条带
  let hasBreak = false
  let bandLo = 0  // 空档下边缘（原始值）
  let bandHi = 0  // 空档上边缘（原始值）
  let gapW = 0    // 空档压缩后的宽度（变换值）
  let markA = 0   // 断轴标记位置（变换值，空档上沿）
  let markB = 0   // 断轴标记位置（变换值，空档下沿）
  let tLo = lo
  let tHi = hi
  let t: (v: number) => number = (v) => v
  let inv: (v: number) => number = (v) => v

  if (prev > hi + range * 0.03) {
    // 低开：昨收在数据区间上方，空档 = (hi, prev)
    hasBreak = true
    bandLo = hi
    bandHi = prev - Math.max((prev - hi) * 0.18, range * 0.008)
    gapW = Math.max(range * 0.02, span * 0.01)
    const g = (v: number) => (v <= bandLo ? v : bandLo + gapW + (v - bandHi))
    const f = (v: number) => (v <= bandLo ? v : v - gapW + bandHi - bandLo)
    t = g
    inv = f
    tLo = g(lo)
    tHi = g(prev)
    markA = bandLo
    markB = bandLo + gapW
    inBandFn = (v) => v > bandLo && v < bandLo + gapW
  } else if (prev > 0 && prev < lo - range * 0.03) {
    // 高开：昨收在数据区间下方，空档 = (prev, lo)
    hasBreak = true
    bandHi = lo
    bandLo = prev + Math.max((lo - prev) * 0.18, range * 0.008)
    gapW = Math.max(range * 0.02, span * 0.01)
    const g = (v: number) => (v >= bandHi ? v : bandHi - gapW - (bandLo - v))
    const f = (v: number) => (v >= bandHi ? v : v + gapW + bandLo - bandHi)
    t = g
    inv = f
    tLo = g(prev)
    tHi = g(hi)
    markA = bandHi - gapW
    markB = bandHi
    inBandFn = (v) => v > bandHi - gapW && v < bandHi
  } else if (prev > 0) {
    // 昨收在区间附近：正常轴，把昨收纳入范围
    lo = Math.min(lo, prev)
    hi = Math.max(hi, prev)
    tLo = lo
    tHi = hi
  }

  const last = pts[pts.length - 1].price
  const lineColor = prev > 0 && last < prev ? DOWN : UP
  const useTicks = times.some((s) => TICKS.has(s))

  chart.setOption({
    animation: false,
    legend: {
      show: true,
      top: 0,
      left: 8,
      itemWidth: 12,
      itemHeight: 2,
      itemGap: 10,
      textStyle: { fontSize: 11, color: '#606266' },
      data: ['价格', '均价'],
    },
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'line', lineStyle: { color: '#aaa', type: 'dashed' } },
      backgroundColor: 'rgba(255,255,255,0.96)',
      borderColor: '#ddd',
      textStyle: { color: '#303133', fontSize: 12 },
      formatter: (ps: any[]) => {
        const p = ps.find((x) => x.seriesName === '价格') ?? ps[0]
        if (!p) return ''
        const pt = pts[p.dataIndex]
        if (!pt) return ''
        const pct = prev > 0 ? (pt.price / prev - 1) * 100 : 0
        const color = pct >= 0 ? UP : DOWN
        const sign = pct >= 0 ? '+' : ''
        return (
          `<b>${pt.time}</b><br/>` +
          `价格：${pt.price.toFixed(2)}<br/>` +
          `均价：${pt.avg.toFixed(2)}<br/>` +
          `涨跌幅：<span style="color:${color};font-weight:600">${sign}${pct.toFixed(2)}%</span><br/>` +
          `成交量：${(pt.volume / 1e4).toFixed(2)} 万手`
        )
      },
    },
    axisPointer: { link: [{ xAxisIndex: 'all' }] },
    grid: [
      { left: 60, right: 56, top: 22, height: '60%' },
      { left: 60, right: 56, top: '82%', height: '11%' },
    ],
    xAxis: [
      {
        type: 'category',
        data: times,
        boundaryGap: false,
        axisLine: { lineStyle: { color: '#c0c4cc' } },
        axisTick: { show: false },
        axisLabel: {
          fontSize: 10,
          color: '#909399',
          interval: useTicks ? 0 : undefined,
          formatter: (v: string) => (useTicks ? (TICKS.has(v) ? v : '') : v),
        },
      },
      {
        type: 'category',
        gridIndex: 1,
        data: times,
        boundaryGap: false,
        axisLabel: { show: false },
        axisTick: { show: false },
        axisLine: { lineStyle: { color: '#c0c4cc' } },
      },
    ],
    yAxis: [
      {
        // 价格轴（断轴：空档内不显示刻度与标签）
        type: 'value',
        scale: true,
        min: tLo,
        max: tHi,
        axisLine: { show: true, lineStyle: { color: '#c0c4cc' } },
        axisTick: { show: false },
        splitLine: { show: false },
        axisLabel: {
          fontSize: 10,
          color: '#909399',
          formatter: (v: number) => (inBand(v) ? '' : inv(v).toFixed(2)),
        },
      },
      {
        // 右侧涨跌幅轴（与价格轴同范围，保证刻度对齐）
        type: 'value',
        min: tLo,
        max: tHi,
        axisLine: { show: false },
        axisTick: { show: false },
        splitLine: { show: false },
        axisLabel: {
          fontSize: 10,
          color: '#909399',
          formatter: (v: number) =>
            inBand(v) || !prev ? '' : `${((inv(v) / prev - 1) * 100).toFixed(2)}%`,
        },
      },
      {
        gridIndex: 1,
        axisLabel: { show: false },
        axisLine: { show: false },
        axisTick: { show: false },
        splitLine: { show: false },
      },
    ],
    series: [
      {
        name: '价格',
        type: 'line',
        data: pts.map((p) => t(p.price)),
        showSymbol: false,
        symbol: 'none',
        lineStyle: { width: 1.4, color: lineColor },
        itemStyle: { color: lineColor },
        clip: false,
        z: 3,
        // 昨收基准虚线
        markLine: prev > 0
          ? {
              silent: true,
              symbol: 'none',
              lineStyle: { color: '#909399', type: 'dashed', width: 1 },
              label: {
                show: true,
                position: 'insideEndTop',
                fontSize: 10,
                color: '#909399',
                formatter: `昨收 ${prev.toFixed(2)}`,
              },
              data: [{ yAxis: t(prev) }],
            }
          : undefined,
        // 断轴标记（∥）
        markPoint: hasBreak
          ? {
              silent: true,
              data: [
                {
                  coord: [0, markA],
                  symbol: BREAK_PATH,
                  symbolSize: 9,
                  itemStyle: { color: '#909399' },
                },
                {
                  coord: [0, markB],
                  symbol: BREAK_PATH,
                  symbolSize: 9,
                  itemStyle: { color: '#909399' },
                },
              ],
            }
          : undefined,
      },
      {
        name: '均价',
        type: 'line',
        data: pts.map((p) => t(p.avg)),
        showSymbol: false,
        symbol: 'none',
        lineStyle: { width: 1, color: AVG },
        itemStyle: { color: AVG },
        clip: false,
        z: 2,
      },
      {
        name: '成交量',
        type: 'bar',
        xAxisIndex: 1,
        yAxisIndex: 2,
        data: pts.map((p) => ({
          value: p.volume,
          // 分钟量颜色：价格在均价上方为红，下方为绿
          itemStyle: { color: p.price >= p.avg ? UP : DOWN },
        })),
        barCategoryGap: '20%',
      },
    ],
  })
}

// 断轴空档判断（render 内按是否断轴赋值，供轴标签 formatter 使用）
let inBandFn: (v: number) => boolean = () => false
function inBand(v: number) {
  return inBandFn(v)
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

watch(() => props.data, render, { deep: false })
</script>

<template>
  <div ref="el" class="chart-box-intraday" />
</template>

<style scoped>
.chart-box-intraday { width: 100%; height: 430px; }
@media (max-width: 768px) {
  .chart-box-intraday { height: 360px; }
}
</style>
