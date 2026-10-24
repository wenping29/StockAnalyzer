<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { WatchlistItem } from '../types'
import { addToWatchlist, fetchWatchlist } from '../api/quote'

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
  } finally {
    loading.value = false
  }
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

    <div class="page-card">
      <el-table :data="funds" v-loading="loading" :row-class-name="rowClass" row-class-name="row-click"
        @row-click="(r: any) => router.push(`/analysis/${r.symbol}`)">
        <el-table-column label="代码" width="110">
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
        <el-table-column label="净值日期" width="120" align="right">
          <template #default="{ row }">{{ row.navDate ? row.navDate.slice(0, 10) : '—' }}</template>
        </el-table-column>
        <el-table-column label="操作" width="100" align="center">
          <template #default="{ row }">
            <el-button link size="small" type="primary" :disabled="watchSymbols.has(row.symbol)"
              @click.stop="addSelf(row)">{{ watchSymbols.has(row.symbol) ? '✓自选' : '＋自选' }}</el-button>
          </template>
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
  </div>
</template>

<style scoped>
.toolbar { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.dim { color: #c0c4cc; font-size: 12px; }
.pager { display: flex; justify-content: flex-end; margin-top: 12px; }
/* 已加入自选的行：淡绿背景 */
:deep(tr.in-watchlist > td.el-table__cell) { background: #f0f9eb; }
:deep(tr.in-watchlist:hover > td.el-table__cell) { background: #e1f3d8; }
</style>
