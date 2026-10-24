<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import type { IndexItem, IndexTrendData, KLinePoint } from '../types'
import { fetchIndices, fetchIndexKline, fetchIndexTrend } from '../api/quote'
import type { CollectCategory } from '../api/collect'
import RefreshButton from '../components/RefreshButton.vue'
import SparkLine from '../components/SparkLine.vue'
import KLineChart from '../components/KLineChart.vue'
import MarketMatrixChart from '../components/MarketMatrixChart.vue'
import IntradayTrendChart from '../components/IntradayTrendChart.vue'

const route = useRoute()

const items = ref<IndexItem[]>([])
const loading = ref(true)

const selected = ref<IndexItem | null>(null)
const period = ref<'day' | 'week' | 'month'>('day')

// 详情区 Tab：日K均线 / 大数据K线 / 股市矩阵 / 日内走势
type TabKey = 'kline' | 'bigkline' | 'matrix' | 'intraday'
const tabs: { key: TabKey; label: string }[] = [
  { key: 'kline', label: '日K · 均线' },
  { key: 'bigkline', label: '大数据K线' },
  { key: 'matrix', label: '股市矩阵' },
  { key: 'intraday', label: '日内走势' },
]
const tab = ref<TabKey>('kline')

const points = ref<KLinePoint[]>([])
const bigPoints = ref<KLinePoint[]>([])
const trend = ref<IndexTrendData | null>(null)
const klineLoading = ref(false)
const trendLoading = ref(false)

// 股市矩阵排除“辅助指标”（深证综指，仅用于两市成交量统计）
const matrixItems = computed(() => items.value.filter((i) => i.group !== '辅助指标'))

const groups = computed(() => {
  const map = new Map<string, IndexItem[]>()
  // "辅助指标"分组（如深证综指）用于成交量统计，不在指数页展示
  for (const it of items.value.filter((i) => i.group !== '辅助指标')) {
    if (!map.has(it.group)) map.set(it.group, [])
    map.get(it.group)!.push(it)
  }
  return [...map.entries()].map(([group, list]) => ({ group, list }))
})

/** 当前 Tab 是否无数据可展示 */
const showEmpty = computed(() => {
  if (tab.value === 'kline') return !points.value.length
  if (tab.value === 'bigkline') return !bigPoints.value.length
  if (tab.value === 'matrix') return !matrixItems.value.length
  return !trend.value
})

async function load() {
  loading.value = true
  try {
    items.value = await fetchIndices()
    if (!selected.value && items.value.length) {
      // 支持首页跳转带入 ?code= 直达指定指数
      const code = route.query.code as string
      select(items.value.find((i) => i.code === code) ?? items.value.find((i) => i.code === '000300') ?? items.value[0])
    }
  } finally {
    loading.value = false
  }
}

function select(item: IndexItem) {
  if (selected.value?.code === item.code) return
  selected.value = item
  loadTabData()
}

function pickPeriod(p: 'day' | 'week' | 'month') {
  if (period.value === p) return
  period.value = p
  loadTabData()
}

function pickTab(t: TabKey) {
  if (tab.value === t) return
  tab.value = t
  loadTabData()
}

/** 按当前 Tab 加载数据（股市矩阵直接用已加载的指数清单，无需请求） */
async function loadTabData() {
  if (!selected.value) return
  if (tab.value === 'kline' || tab.value === 'bigkline') {
    klineLoading.value = true
    try {
      const limit = tab.value === 'bigkline' ? 500 : 120
      const res = await fetchIndexKline(selected.value.code, period.value, limit)
      if (tab.value === 'bigkline') bigPoints.value = res.points
      else points.value = res.points
    } finally {
      klineLoading.value = false
    }
  } else if (tab.value === 'intraday') {
    await loadTrend()
  }
}

async function loadTrend() {
  if (!selected.value) return
  trendLoading.value = true
  try {
    trend.value = await fetchIndexTrend(selected.value.code)
  } catch {
    trend.value = null
  } finally {
    trendLoading.value = false
  }
}

// 日内走势 Tab 每 60 秒自动刷新（与后端缓存周期一致）；离开 Tab 停止
let trendTimer: number | undefined
watch(tab, (t) => {
  if (trendTimer) {
    clearInterval(trendTimer)
    trendTimer = undefined
  }
  if (t === 'intraday') trendTimer = window.setInterval(loadTrend, 60_000)
})
onUnmounted(() => {
  if (trendTimer) clearInterval(trendTimer)
})

function pctClass(v: number) {
  return v >= 0 ? 'up' : 'down'
}
function fmtPct(v: number) {
  return `${v >= 0 ? '+' : ''}${v.toFixed(2)}%`
}

// 顶部"刷新"：按当前 Tab 触发对应分类采集，后台完成后重载数据
const refreshCategory = computed<CollectCategory>(() =>
  tab.value === 'intraday' ? 'intraday' : 'indices'
)
async function onCollected() {
  if (tab.value === 'intraday') {
    await loadTrend()
  } else {
    await load()
    await loadTabData()
  }
}

onMounted(load)
</script>

<template>
  <div v-loading="loading">
    <!-- 数据尚未采集完成的空状态 -->
    <div class="page-card" v-if="!loading && !items.length">
      <el-empty description="指数数据尚未采集完成（首次启动或东财接口限流时约需几分钟到一小时），稍后点击刷新；也可在管理后台手动触发“同步指数”">
        <el-button @click="load">刷新</el-button>
      </el-empty>
    </div>

    <!-- 选中指数详情：Tab 切换 日K均线 / 大数据K线 / 股市矩阵 / 日内走势 -->
    <div class="page-card" v-if="selected">
      <div class="kline-head">
        <h3>{{ selected.name }} <span class="code">{{ selected.code }}</span></h3>
        <span class="price" :class="pctClass(selected.changePct)">{{ selected.price.toFixed(2) }}</span>
        <span :class="pctClass(selected.changePct)">{{ fmtPct(selected.changePct) }}</span>
        <span v-if="selected.amount" class="amount">成交 {{ selected.amount >= 10000 ? (selected.amount / 10000).toFixed(2) + ' 万亿' : selected.amount.toFixed(0) + ' 亿' }}</span>
        <div style="flex: 1" />
        <RefreshButton :category="refreshCategory" label="刷新数据" @done="onCollected" />
        <div class="detail-tabs">
          <span v-for="t in tabs" :key="t.key"
            class="detail-tab" :class="{ active: tab === t.key }" @click="pickTab(t.key)">
            {{ t.label }}
          </span>
        </div>
      </div>

      <!-- K 线类 Tab 的周期切换 -->
      <div class="chart-toolbar" v-if="tab === 'kline' || tab === 'bigkline'">
        <div class="period-tabs">
          <span v-for="p in (['day', 'week', 'month'] as const)" :key="p"
            class="period-item" :class="{ active: period === p }" @click="pickPeriod(p)">
            {{ { day: '日', week: '周', month: '月' }[p] }}
          </span>
        </div>
        <span class="chart-title">
          {{
            tab === 'kline'
              ? 'K 线 + MA5 · MA10 · MA20 · MA30（涨红跌绿，下方为成交量，滚轮/滑块可缩放）'
              : '最近 500 根 K 线（大数据量），拖动底部滑块或滚轮缩放查看历史区间'
          }}
        </span>
      </div>
      <div class="chart-toolbar" v-else-if="tab === 'matrix'">
        <span class="chart-title">色块大小 = 当日成交额，颜色 = 涨跌幅（红涨绿跌），点击色块可切换指数</span>
      </div>
      <div class="chart-toolbar" v-else-if="tab === 'intraday'">
        <span class="chart-title">
          分时走势（{{ trend?.date || '最近交易日' }}，每 60 秒自动刷新）· 昨收不在当日波动区间时价格轴自动断轴压缩空档
        </span>
      </div>

      <div v-loading="tab === 'intraday' ? trendLoading : klineLoading">
        <KLineChart
          v-if="tab === 'kline' && points.length"
          :key="`kline-${selected.code}-${period}`"
          :points="points" :ma-periods="[5, 10, 20, 30]" />
        <KLineChart
          v-if="tab === 'bigkline' && bigPoints.length"
          :key="`bigkline-${selected.code}-${period}`"
          :points="bigPoints" :ma-periods="[5, 10, 20, 30]" />
        <MarketMatrixChart
          v-if="tab === 'matrix' && matrixItems.length"
          :key="`matrix-${matrixItems.length}`"
          :items="matrixItems" @select="select" />
        <IntradayTrendChart
          v-if="tab === 'intraday' && trend"
          :key="`trend-${selected.code}-${trend.date}`"
          :data="trend" />
        <el-empty v-if="showEmpty" :image-size="60"
          :description="tab === 'intraday' ? '暂无分时数据（东财实时接口暂不可用或该指数不支持）' : '暂无数据'" />
      </div>
    </div>

    <!-- 指数网格（按分组） -->
    <div class="page-card" v-for="g in groups" :key="g.group">
      <div class="group-title">{{ g.group }}</div>
      <div class="index-grid">
        <div
          v-for="idx in g.list" :key="idx.code"
          class="index-cell" :class="{ active: selected?.code === idx.code }"
          @click="select(idx)"
        >
          <div class="cell-head">
            <span class="cell-name">{{ idx.name }}</span>
            <span class="cell-pct" :class="pctClass(idx.changePct)">{{ fmtPct(idx.changePct) }}</span>
          </div>
          <div class="cell-price" :class="pctClass(idx.changePct)">{{ idx.price.toFixed(2) }}</div>
          <SparkLine :data="idx.trend" :up="idx.changePct >= 0" />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.kline-head { display: flex; align-items: baseline; gap: 12px; margin-bottom: 12px; flex-wrap: wrap; }
.kline-head h3 { font-size: 17px; }
.code { color: #909399; font-size: 13px; font-weight: normal; }
.price { font-size: 22px; font-weight: 600; }
.amount { color: #909399; font-size: 13px; }

/* 详情 Tab（下划线风格） */
.detail-tabs { display: flex; gap: 4px; border-bottom: 1px solid #ebeef5; }
.detail-tab {
  padding: 6px 14px; cursor: pointer; font-size: 14px; color: #606266;
  border-bottom: 2px solid transparent; margin-bottom: -1px; user-select: none;
  white-space: nowrap;
}
.detail-tab:hover { color: #409eff; }
.detail-tab.active { color: #409eff; border-bottom-color: #409eff; font-weight: 600; }

.chart-toolbar { display: flex; align-items: center; gap: 16px; margin-bottom: 10px; flex-wrap: wrap; }
.chart-title { color: #606266; font-size: 13px; }
.period-tabs { display: flex; border: 1px solid #dcdfe6; border-radius: 6px; overflow: hidden; }
.period-item { padding: 4px 16px; cursor: pointer; font-size: 13px; color: #606266; background: #fff; user-select: none; }
.period-item + .period-item { border-left: 1px solid #dcdfe6; }
.period-item.active { background: #409eff; color: #fff; }

.group-title { font-size: 14px; font-weight: 600; margin-bottom: 12px; color: #606266; }
.index-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(170px, 1fr)); gap: 12px; }
.index-cell {
  border: 1px solid #ebeef5; border-radius: 8px; padding: 10px 12px; cursor: pointer;
  transition: all 0.15s;
}
.index-cell:hover { border-color: #409eff; }
.index-cell.active { border-color: #409eff; background: #ecf5ff; }
.cell-head { display: flex; justify-content: space-between; align-items: baseline; }
.cell-name { font-size: 13px; color: #606266; }
.cell-pct { font-size: 13px; font-weight: 600; }
.cell-price { font-size: 17px; font-weight: 600; margin: 2px 0 4px; }
</style>
