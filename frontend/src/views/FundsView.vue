<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { FundEstimate, WatchlistItem } from '../types'
import { addToWatchlist, fetchWatchlist } from '../api/quote'
import { fetchFundEstimates } from '../api/funds'

const router = useRouter()

const funds = ref<{
  symbol: string
  name: string
  fundType: string | null
  nav: number | null
  navChgPct: number | null
  navDate: string | null
}[]>([])
const total = ref(0)
const loading = ref(false)
const page = ref(1)
const pageSize = 20
const fundTypes = ref<string[]>([])

const filters = reactive({ keyword: '', fundType: '' })

// 自选状态（背景色 + 按钮状态）
const watchSymbols = ref<Set<string>>(new Set())
// 点击行：右侧面板显示基金信息（跳转用"详情"按钮）
const detailRow = ref<any>(null)

// 当日重仓股估算（按基金代码索引），与列表分开展示，避免重仓股首次拉取拖慢列表
const estimateMap = ref<Record<string, FundEstimate>>({})
const estimateLoading = ref(false)
let estimateSeq = 0
let estimateTimer: ReturnType<typeof setInterval> | undefined

const detailEstimate = computed(() =>
  detailRow.value ? estimateMap.value[detailRow.value.symbol] ?? null : null)

async function loadWatchlist() {
  try {
    const wl: WatchlistItem[] = await fetchWatchlist()
    watchSymbols.value = new Set(wl.map((w) => w.symbol))
  } catch {
    watchSymbols.value = new Set()
  }
}

async function load() {
  loading.value = true
  try {
    const params = new URLSearchParams({
      page: String(page.value),
      pageSize: String(pageSize),
    })
    if (filters.keyword.trim()) params.set('keyword', filters.keyword.trim())
    if (filters.fundType) params.set('fundType', filters.fundType)
    const res = await fetch(`/api/funds?${params}`, {
      headers: { Authorization: `Bearer ${localStorage.getItem('token') || ''}` },
    })
    const data = await res.json()
    funds.value = data.items
    total.value = data.total
    loadEstimates()
  } finally {
    loading.value = false
  }
}

/** 拉取当前页基金的当日重仓股估算（列表先渲染，估算稍后填充）。 */
async function loadEstimates() {
  const symbols = funds.value.map((f) => f.symbol)
  const seq = ++estimateSeq
  if (symbols.length === 0) {
    estimateMap.value = {}
    return
  }
  estimateLoading.value = true
  try {
    const list = await fetchFundEstimates(symbols)
    if (seq !== estimateSeq) return
    estimateMap.value = Object.fromEntries(list.map((e) => [e.symbol, e]))
  } catch {
    if (seq === estimateSeq) estimateMap.value = {}
  } finally {
    if (seq === estimateSeq) estimateLoading.value = false
  }
}

function reportDateText(d: string | null) {
  return d ? d.slice(0, 10) : '—'
}

function search() {
  page.value = 1
  load()
}

async function addSelf(row: (typeof funds.value)[number]) {
  const res = await addToWatchlist(row.symbol)
  ElMessage.success(res.message)
  watchSymbols.value.add(row.symbol)
  watchSymbols.value = new Set(watchSymbols.value)
}

// 已在自选的行加背景色
function rowClass({ row }: { row: (typeof funds.value)[number] }) {
  return watchSymbols.value.has(row.symbol) ? 'in-watchlist' : ''
}

function pctClass(v: number | null) {
  return v == null ? '' : v >= 0 ? 'up' : 'down'
}
function fmtPct(v: number | null) {
  return v == null ? '—' : `${v >= 0 ? '+' : ''}${v.toFixed(2)}%`
}
// 显示用代码（隐藏 .F 后缀）
function displayCode(symbol: string) {
  return symbol.replace(/\.F$/, '')
}

async function syncNow() {
  await fetch('/api/funds/sync', {
    method: 'POST',
    headers: { Authorization: `Bearer ${localStorage.getItem('token') || ''}` },
  })
  ElMessage.success('已提交同步（约 30 秒），完成后刷新')
}

onMounted(async () => {
  load()
  loadWatchlist()
  fetch('/api/funds/types', { headers: { Authorization: `Bearer ${localStorage.getItem('token') || ''}` } })
    .then((r) => r.json())
    .then((r) => (fundTypes.value = r))
    .catch(() => {})
  // 盘中估算随行情变化，每分钟刷新一次（重仓股走服务端缓存，只重取实时行情）
  estimateTimer = setInterval(loadEstimates, 60_000)
})

onUnmounted(() => {
  if (estimateTimer) clearInterval(estimateTimer)
})
</script>

<template>
  <div>
    <div class="page-card">
      <div class="toolbar">
        <el-input
          v-model="filters.keyword" placeholder="搜索基金代码 / 名称" clearable style="width: 220px"
          @keyup.enter="search" @clear="search" />
        <el-select v-model="filters.fundType" clearable filterable placeholder="全部类型" style="width: 180px" @change="search">
          <el-option v-for="t in fundTypes" :key="t" :label="t" :value="t" />
        </el-select>
        <el-button type="primary" @click="search">搜索</el-button>
        <div style="flex: 1" />
        <el-button size="small" @click="syncNow">同步基金清单</el-button>
        <span class="dim">净值 = 已加自选基金的最新采集值（加自选后自动采集）· 共 {{ total }} 只</span>
      </div>
    </div>

    <!-- 基金列表（左）+ 选中基金信息面板（右） -->
    <div class="list-layout">
      <div class="page-card table-card">
        <el-table :data="funds" v-loading="loading" :row-class-name="rowClass"
          @row-click="(r: any) => detailRow = r">
          <el-table-column label="自选" width="90" align="center">
            <template #default="{ row }">
              <el-button link size="small" type="primary" :disabled="watchSymbols.has(row.symbol)"
                @click.stop="addSelf(row)">{{ watchSymbols.has(row.symbol) ? '✓自选' : '＋自选' }}</el-button>
            </template>
          </el-table-column>
          <el-table-column label="详情" width="70" align="center">
            <template #default="{ row }">
              <el-button link size="small" type="success"
                @click.stop="router.push(`/analysis/${row.symbol}`)">详情</el-button>
            </template>
          </el-table-column>
          <el-table-column label="代码" width="100">
            <template #default="{ row }">{{ displayCode(row.symbol) }}</template>
          </el-table-column>
          <el-table-column label="名称" min-width="200">
            <template #default="{ row }">
              {{ row.name }}
              <el-tag v-if="row.fundType" size="small" type="info">{{ row.fundType }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="最新净值" width="110" align="right">
            <template #default="{ row }">{{ row.nav == null ? '未采集' : row.nav.toFixed(4) }}</template>
          </el-table-column>
          <el-table-column label="日涨幅" width="95" align="right">
            <template #default="{ row }">
              <span :class="pctClass(row.navChgPct)">{{ fmtPct(row.navChgPct) }}</span>
            </template>
          </el-table-column>
          <el-table-column label="估算涨幅" width="115" align="right">
            <template #default="{ row }">
              <el-tooltip v-if="estimateMap[row.symbol]?.estimatedChangePct != null" placement="top"
                :content="`按最新季报前十大重仓股加权 · 覆盖净值 ${estimateMap[row.symbol].coveredWeightPct.toFixed(2)}% · 报告期 ${reportDateText(estimateMap[row.symbol].reportDate)}`">
                <span :class="pctClass(estimateMap[row.symbol].estimatedChangePct)">
                  {{ fmtPct(estimateMap[row.symbol].estimatedChangePct) }}
                </span>
              </el-tooltip>
              <span v-else-if="estimateLoading" class="dim">计算中…</span>
              <span v-else class="dim">—</span>
            </template>
          </el-table-column>
          <el-table-column label="净值日期" width="120" align="right">
            <template #default="{ row }">{{ row.navDate ? row.navDate.slice(0, 10) : '—' }}</template>
          </el-table-column>
          <template #empty>
            <el-empty description="无符合条件的基金" :image-size="60" />
          </template>
        </el-table>

        <div class="pager">
          <el-pagination
            v-model:current-page="page"
            :page-size="pageSize"
            :total="total"
            layout="prev, pager, next, total"
            @current-change="load" />
        </div>
      </div>

      <!-- 右侧：选中基金信息面板 -->
      <div class="page-card detail-panel" v-if="detailRow">
        <div class="dp-head">
          <div class="dp-name">{{ detailRow.name }}</div>
          <div class="dp-code">{{ displayCode(detailRow.symbol) }}</div>
        </div>
        <div class="dp-price-row">
          <span class="dp-price" :class="pctClass(detailRow.navChgPct)">
            {{ detailRow.nav == null ? '未采集' : detailRow.nav.toFixed(4) }}
          </span>
          <span class="dp-chg" :class="pctClass(detailRow.navChgPct)">{{ fmtPct(detailRow.navChgPct) }}</span>
        </div>
        <div class="dp-grid">
          <div class="dp-item"><span class="k">基金类型</span><span class="v">{{ detailRow.fundType ?? '—' }}</span></div>
          <div class="dp-item"><span class="k">净值日期</span><span class="v">{{ detailRow.navDate ? detailRow.navDate.slice(0, 10) : '—' }}</span></div>
        </div>

        <!-- 当日重仓股估算 -->
        <div class="dp-estimate" v-if="detailEstimate">
          <div class="dp-est-head">
            <span class="k">今日重仓股估算</span>
            <span class="dp-est-pct" :class="pctClass(detailEstimate.estimatedChangePct)">
              {{ fmtPct(detailEstimate.estimatedChangePct) }}
            </span>
          </div>
          <div class="dp-est-sub">
            覆盖净值 {{ detailEstimate.coveredWeightPct.toFixed(2) }}% · 报告期 {{ reportDateText(detailEstimate.reportDate) }}
          </div>

          <div class="dp-holds" v-if="detailEstimate.holdings.length">
            <div class="dp-hold" v-for="h in detailEstimate.holdings" :key="`${h.secMarket}.${h.symbol}`">
              <span class="hname" :title="h.name">{{ h.name }}</span>
              <span class="hweight">{{ h.weightPct.toFixed(2) }}%</span>
              <span class="hchg" :class="pctClass(h.changePct)">{{ fmtPct(h.changePct) }}</span>
            </div>
          </div>
          <div class="dp-est-empty" v-else>暂无重仓股数据（债券/货币基金或尚未披露）</div>
        </div>
        <div class="dp-tip" v-else-if="estimateLoading">正在拉取重仓股…</div>

        <el-button type="primary" style="width: 100%; margin-top: 14px"
          @click="router.push(`/analysis/${detailRow.symbol}`)">进入净值走势分析 →</el-button>
        <div class="dp-tip">加入自选后自动采集历史净值</div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.toolbar { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.dim { color: #c0c4cc; font-size: 12px; }
.pager { display: flex; justify-content: flex-end; margin-top: 12px; }
/* 左列表 + 右信息面板 */
.list-layout { display: flex; gap: 16px; align-items: flex-start; }
.table-card { flex: 1; min-width: 0; }
.detail-panel {
  width: 320px;
  flex-shrink: 0;
  position: sticky;
  top: 12px;
}
.dp-head { margin-bottom: 8px; }
.dp-name { font-size: 17px; font-weight: 600; }
.dp-code { color: #909399; font-size: 12px; margin-top: 2px; }
.dp-price-row { display: flex; align-items: baseline; gap: 10px; margin: 6px 0 12px; }
.dp-price { font-size: 26px; font-weight: 700; }
.dp-chg { font-size: 14px; font-weight: 600; }
.dp-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 8px 12px; }
.dp-item { display: flex; flex-direction: column; padding: 6px 8px; background: #f8f9fb; border-radius: 6px; }
.dp-item .k { font-size: 11px; color: #909399; }
.dp-item .v { font-size: 14px; font-weight: 600; margin-top: 2px; }
.dp-tip { color: #c0c4cc; font-size: 11px; margin-top: 10px; text-align: center; }

/* 当日重仓股估算 */
.dp-estimate { margin-top: 12px; padding: 10px; background: #f8f9fb; border-radius: 8px; }
.dp-est-head { display: flex; align-items: baseline; justify-content: space-between; }
.dp-est-head .k { font-size: 12px; color: #606266; }
.dp-est-pct { font-size: 20px; font-weight: 700; }
.dp-est-sub { color: #909399; font-size: 11px; margin: 2px 0 8px; }
.dp-holds { display: flex; flex-direction: column; gap: 4px; max-height: 240px; overflow-y: auto; }
.dp-hold { display: flex; align-items: center; gap: 8px; font-size: 12px; }
.dp-hold .hname {
  flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: #303133;
}
.dp-hold .hweight { width: 48px; text-align: right; color: #909399; }
.dp-hold .hchg { width: 58px; text-align: right; font-weight: 600; }
.dp-est-empty { color: #c0c4cc; font-size: 11px; }

@media (max-width: 1100px) {
  .list-layout { flex-direction: column; }
  .detail-panel { width: 100%; position: static; }
}
/* 已加入自选的行：淡绿背景 */
:deep(tr.in-watchlist > td.el-table__cell) { background: #f0f9eb; }
:deep(tr.in-watchlist:hover > td.el-table__cell) { background: #e1f3d8; }
</style>
