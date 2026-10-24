<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { StockListItem, BoardStat, WatchlistItem } from '../types'
import { fetchStocks, fetchBoardStats } from '../api/stocks'
import { fetchIndustries } from '../api/screener'
import { addToWatchlist, fetchWatchlist } from '../api/quote'

const router = useRouter()

const boards = ref<BoardStat[]>([])
const industries = ref<string[]>([])
const items = ref<StockListItem[]>([])
const total = ref(0)
const loading = ref(false)
const page = ref(1)
const pageSize = 20

// 自选状态（用于按钮显示"加自选/已自选"）
const watchSymbols = ref<Set<string>>(new Set())

async function loadWatchlist() {
  try {
    const wl: WatchlistItem[] = await fetchWatchlist()
    watchSymbols.value = new Set(wl.map((w) => w.symbol))
  } catch {
    watchSymbols.value = new Set()
  }
}

async function addSelf(row: StockListItem) {
  const res = await addToWatchlist(row.symbol)
  ElMessage.success(res.message)
  watchSymbols.value.add(row.symbol)
  watchSymbols.value = new Set(watchSymbols.value)
}

const board = ref('all')
const keyword = ref('')
const industry = ref('')
const sort = ref('marketCap')
const desc = ref(true)

const BOARD_LABEL: Record<string, string> = {
  all: '全部', sh: '沪市主板', star: '科创板', sz: '深市主板',
  chinext: '创业板', bse: '北交所', other: '其他',
}

async function load() {
  loading.value = true
  try {
    const res = await fetchStocks({
      board: board.value,
      keyword: keyword.value || undefined,
      industry: industry.value || undefined,
      sort: sort.value,
      desc: desc.value,
      page: page.value,
      pageSize,
    })
    items.value = res.items
    total.value = res.total
  } finally {
    loading.value = false
  }
}

function pickBoard(key: string) {
  if (board.value === key) return
  board.value = key
  page.value = 1
  load()
}

function search() {
  page.value = 1
  load()
}

function onSort(prop: string) {
  if (sort.value === prop) {
    desc.value = !desc.value
  } else {
    sort.value = prop
    desc.value = true
  }
  load()
}

function sortIcon(prop: string) {
  if (sort.value !== prop) return '⇅'
  return desc.value ? '↓' : '↑'
}

function pctClass(v: number | null) {
  return v == null ? '' : v >= 0 ? 'up' : 'down'
}
function fmt(v: number | null | undefined, digits = 2) {
  return v == null ? '—' : v.toFixed(digits)
}

onMounted(async () => {
  load()
  loadWatchlist()
  fetchBoardStats().then((r) => (boards.value = r)).catch(() => {})
  fetchIndustries().then((r) => (industries.value = r)).catch(() => {})
})
</script>

<template>
  <div>
    <!-- 板块 tab -->
    <div class="page-card">
      <div class="board-tabs">
        <div
          v-for="b in boards" :key="b.key"
          class="board-item" :class="{ active: board === b.key }"
          @click="pickBoard(b.key)"
        >
          <span class="board-name">{{ b.name }}</span>
          <span class="board-count">{{ b.count }}</span>
        </div>
      </div>

      <div class="toolbar">
        <el-input
          v-model="keyword" placeholder="搜索代码 / 名称" clearable style="width: 220px"
          @keyup.enter="search" @clear="search" />
        <el-select v-model="industry" clearable filterable placeholder="全部行业" style="width: 160px" @change="search">
          <el-option v-for="i in industries" :key="i" :label="i" :value="i" />
        </el-select>
        <el-button type="primary" @click="search">搜索</el-button>
        <div style="flex: 1" />
        <span class="dim">估值 = 最新交易日收盘 · 共 {{ total }} 只</span>
      </div>
    </div>

    <!-- 个股列表 -->
    <div class="page-card">
      <el-table :data="items" v-loading="loading" row-class-name="row-click"
        @row-click="(r: StockListItem) => router.push(`/analysis/${r.symbol}`)">
        <el-table-column label="代码" width="150">
          <template #default="{ row }">
            <el-button
              link size="small" type="primary"
              :disabled="watchSymbols.has(row.symbol)"
              @click.stop="addSelf(row)"
            >{{ watchSymbols.has(row.symbol) ? '✓自选' : '＋自选' }}</el-button>
            <span class="symbol-text">{{ row.symbol }}</span>
          </template>
        </el-table-column>
        <el-table-column label="名称" min-width="120">
          <template #default="{ row }">
            {{ row.name }}
            <el-tag size="small" type="info">{{ BOARD_LABEL[row.board] ?? row.board }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="行业" min-width="90">
          <template #default="{ row }">
            <span class="dim">{{ row.industry ?? '—' }}</span>
          </template>
        </el-table-column>
        <el-table-column width="100" align="right">
          <template #header><span class="th-sort" @click="onSort('closePrice')">最新价 {{ sortIcon('closePrice') }}</span></template>
          <template #default="{ row }">{{ row.closePrice.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column label="涨跌幅" width="95" align="right">
          <template #default="{ row }">
            <span :class="pctClass(row.changePct)">{{ row.changePct == null ? '—' : (row.changePct >= 0 ? '+' : '') + row.changePct.toFixed(2) + '%' }}</span>
          </template>
        </el-table-column>
        <el-table-column width="90" align="right">
          <template #header><span class="th-sort" @click="onSort('pe')">市盈率 {{ sortIcon('pe') }}</span></template>
          <template #default="{ row }">{{ fmt(row.peTtm, 1) }}</template>
        </el-table-column>
        <el-table-column label="市净率" width="85" align="right">
          <template #default="{ row }">{{ fmt(row.pbMrq, 2) }}</template>
        </el-table-column>
        <el-table-column width="90" align="right">
          <template #header><span class="th-sort" @click="onSort('roe')">ROE% {{ sortIcon('roe') }}</span></template>
          <template #default="{ row }">{{ fmt(row.roe, 1) }}</template>
        </el-table-column>
        <el-table-column width="105" align="right">
          <template #header><span class="th-sort" @click="onSort('revenueYoy')">营收同比 {{ sortIcon('revenueYoy') }}</span></template>
          <template #default="{ row }">
            <span :class="pctClass(row.revenueYoY)">{{ row.revenueYoY == null ? '—' : (row.revenueYoY >= 0 ? '+' : '') + row.revenueYoY.toFixed(1) + '%' }}</span>
          </template>
        </el-table-column>
        <el-table-column width="105" align="right">
          <template #header><span class="th-sort" @click="onSort('changePct')">净利同比 {{ sortIcon('changePct') }}</span></template>
          <template #default="{ row }">
            <span :class="pctClass(row.netProfitYoY)">{{ row.netProfitYoY == null ? '—' : (row.netProfitYoY >= 0 ? '+' : '') + row.netProfitYoY.toFixed(1) + '%' }}</span>
          </template>
        </el-table-column>
        <el-table-column width="110" align="right">
          <template #header><span class="th-sort" @click="onSort('marketCap')">市值(亿) {{ sortIcon('marketCap') }}</span></template>
          <template #default="{ row }">{{ row.marketCap == null ? '—' : row.marketCap.toFixed(0) }}</template>
        </el-table-column>
        <template #empty>
          <el-empty description="暂无数据（基本面数据尚未采集完成时请稍后刷新）" :image-size="60" />
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
  </div>
</template>

<style scoped>
.board-tabs { display: flex; gap: 10px; flex-wrap: wrap; margin-bottom: 14px; }
.board-item {
  display: flex; align-items: baseline; gap: 6px; padding: 8px 18px;
  border: 1px solid #dcdfe6; border-radius: 18px; cursor: pointer;
  transition: all 0.15s; background: #fff;
}
.board-item:hover { border-color: #409eff; color: #409eff; }
.board-item.active { background: #409eff; border-color: #409eff; color: #fff; }
.board-name { font-size: 14px; }
.board-count { font-size: 12px; opacity: 0.75; }
.toolbar { display: flex; align-items: center; gap: 10px; }
.dim { color: #909399; font-size: 12px; }
.symbol-text { margin-left: 2px; }
.th-sort { cursor: pointer; user-select: none; }
.th-sort:hover { color: #409eff; }
.pager { display: flex; justify-content: flex-end; margin-top: 12px; }
</style>
