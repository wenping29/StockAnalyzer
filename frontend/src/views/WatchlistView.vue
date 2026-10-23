<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import * as echarts from 'echarts'
import type { SearchItem, WatchlistItem } from '../types'
import {
  fetchWatchlist, addToWatchlist, removeFromWatchlist, searchSymbol,
} from '../api/quote'

const router = useRouter()
const items = ref<WatchlistItem[]>([])
const loading = ref(false)

// 远程搜索
const options = ref<SearchItem[]>([])
const searching = ref(false)
let searchTimer: ReturnType<typeof setTimeout> | null = null

// 统计
const upCount = computed(() => items.value.filter((w) => (w.changePct ?? 0) > 0).length)
const downCount = computed(() => items.value.filter((w) => (w.changePct ?? 0) < 0).length)
const avgMonth = computed(() => {
  const rows = items.value.filter((w) => w.monthReturnPct != null)
  if (!rows.length) return null
  return rows.reduce((s, w) => s + (w.monthReturnPct ?? 0), 0) / rows.length
})
const totalMainNet = computed(() =>
  items.value.reduce((s, w) => s + (w.mainNetIn5d ?? 0), 0))

async function load() {
  loading.value = true
  try {
    items.value = await fetchWatchlist()
  } finally {
    loading.value = false
  }
}

function remoteSearch(keyword: string) {
  if (searchTimer) clearTimeout(searchTimer)
  if (!keyword.trim()) {
    options.value = []
    return
  }
  searchTimer = setTimeout(async () => {
    searching.value = true
    try {
      options.value = await searchSymbol(keyword.trim())
    } finally {
      searching.value = false
    }
  }, 300)
}

async function add(item: SearchItem) {
  const res = await addToWatchlist(item.symbol)
  ElMessage.success(res.message)
  load()
}

async function remove(row: WatchlistItem) {
  await removeFromWatchlist(row.symbol)
  ElMessage.success('已移除')
  load()
}

function pctClass(v: number | null) {
  return v == null ? '' : v >= 0 ? 'up' : 'down'
}
function fmtPct(v: number | null) {
  return v == null ? '—' : `${v >= 0 ? '+' : ''}${v.toFixed(2)}%`
}
function fmtYi(v: number | null) {
  return v == null ? '—' : `${(v / 1e8).toFixed(2)} 亿`
}

const typeLabel: Record<string, string> = { Stock: '股票', Etf: 'ETF', Fund: '场外基金' }

// 近1月收益排行（横向条形图）
const rankEl = ref<HTMLElement>()
let rankChart: echarts.ECharts | null = null

function renderRank() {
  if (!rankChart) return
  const rows = items.value
    .filter((w) => w.monthReturnPct != null)
    .sort((a, b) => (a.monthReturnPct ?? 0) - (b.monthReturnPct ?? 0))
  if (!rows.length) {
    rankChart.clear()
    return
  }
  rankChart.setOption({
    animation: false,
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' }, valueFormatter: (v: number) => `${v}%` },
    grid: { left: 90, right: 40, top: 6, bottom: 24 },
    xAxis: { type: 'value', axisLabel: { formatter: (v: number) => `${v}%` } },
    yAxis: {
      type: 'category',
      data: rows.map((r) => r.name),
      axisLabel: { width: 84, overflow: 'truncate' },
    },
    series: [
      {
        type: 'bar',
        data: rows.map((r) => ({
          value: +(r.monthReturnPct ?? 0).toFixed(2),
          itemStyle: { color: (r.monthReturnPct ?? 0) >= 0 ? '#ef232a' : '#14b143' },
        })),
        label: { show: true, position: 'right', formatter: (p: any) => `${p.value >= 0 ? '+' : ''}${p.value}%` },
        barMaxWidth: 18,
      },
    ],
  })
}

function resize() {
  rankChart?.resize()
}

onMounted(() => {
  rankChart = echarts.init(rankEl.value!)
  window.addEventListener('resize', resize)
  load()
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', resize)
  rankChart?.dispose()
})

watch(items, renderRank)
</script>

<template>
  <div>
    <!-- 组合统计 -->
    <div class="stat-cards" style="margin-bottom: 16px">
      <div class="stat-card">
        <div class="label">自选标的</div>
        <div class="value">{{ items.length }}</div>
        <div class="sub">最多 50 个</div>
      </div>
      <div class="stat-card">
        <div class="label">今日涨 / 跌</div>
        <div class="value"><span class="up">{{ upCount }}</span> / <span class="down">{{ downCount }}</span></div>
        <div class="sub">自选今日分布</div>
      </div>
      <div class="stat-card">
        <div class="label">近1月平均收益</div>
        <div class="value" :class="pctClass(avgMonth)">{{ fmtPct(avgMonth) }}</div>
        <div class="sub">自选组合</div>
      </div>
      <div class="stat-card">
        <div class="label">主力净流入合计(近5日)</div>
        <div class="value" :class="pctClass(totalMainNet)">{{ fmtYi(totalMainNet) }}</div>
        <div class="sub">全部自选</div>
      </div>
    </div>

    <!-- 近1月收益排行 -->
    <div class="page-card" v-if="items.length">
      <div class="chart-toolbar"><span class="chart-title">自选近1月收益排行</span></div>
      <div ref="rankEl" style="width: 100%; height: 300px" />
    </div>

    <!-- 自选列表 -->
    <div class="page-card">
      <div class="toolbar">
        <el-select
          filterable
          remote
          clearable
          placeholder="搜索代码 / 拼音 / 名称（如 600519、maotai、茅台）"
          style="width: 420px"
          :remote-method="remoteSearch"
          :loading="searching"
          @change="add"
        >
          <el-option
            v-for="o in options"
            :key="o.symbol + o.type"
            :label="`${o.name} (${o.symbol})`"
            :value="o"
          >
            <span>{{ o.name }}</span>
            <span style="float: right; color: #909399; font-size: 12px">
              {{ o.symbol }} · {{ typeLabel[o.type] ?? o.type }}
            </span>
          </el-option>
        </el-select>
        <span class="hint">选中即加入自选，后台自动采集数据</span>
        <div style="flex: 1" />
        <el-button :loading="loading" @click="load">刷新</el-button>
      </div>

      <el-table :data="items" v-loading="loading" @row-click="(r: WatchlistItem) => router.push(`/analysis/${r.symbol}`)" row-class-name="row-click">
        <el-table-column label="代码" width="100">
          <template #default="{ row }">{{ row.symbol }}</template>
        </el-table-column>
        <el-table-column label="名称" min-width="140">
          <template #default="{ row }">
            {{ row.name }}
            <el-tag size="small" type="info">{{ typeLabel[row.type] ?? row.type }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="最新价" width="100" align="right">
          <template #default="{ row }">{{ row.latestPrice ?? '采集中…' }}</template>
        </el-table-column>
        <el-table-column label="涨跌幅" width="100" align="right">
          <template #default="{ row }">
            <span :class="pctClass(row.changePct)">{{ fmtPct(row.changePct) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="近1月收益" width="110" align="right">
          <template #default="{ row }">
            <span :class="pctClass(row.monthReturnPct)">{{ fmtPct(row.monthReturnPct) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="主力净流入(近5日)" width="160" align="right">
          <template #default="{ row }">
            <span :class="pctClass(row.mainNetIn5d)">{{ fmtYi(row.mainNetIn5d) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="90" align="center">
          <template #default="{ row }">
            <el-button link type="danger" @click.stop="remove(row)">移除</el-button>
          </template>
        </el-table-column>
        <template #empty>
          <el-empty description="自选为空，搜索添加标的，或等待首次自动采集（示例标的）" />
        </template>
      </el-table>
    </div>
  </div>
</template>

<style scoped>
.toolbar { display: flex; align-items: center; gap: 12px; margin-bottom: 14px; }
.hint { color: #909399; font-size: 12px; }
.chart-toolbar { display: flex; align-items: center; margin-bottom: 8px; }
.chart-title { color: #606266; font-size: 13px; }
</style>
