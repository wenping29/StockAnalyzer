<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { ScreenerItem } from '../types'
import { screenStocks, fetchIndustries, triggerScreenerSync } from '../api/screener'

const router = useRouter()

const industries = ref<string[]>([])
const items = ref<ScreenerItem[]>([])
const total = ref(0)
const loading = ref(false)
const page = ref(1)
const pageSize = 20

// 筛选条件（空 = 不限）
const filters = reactive({
  keyword: '',
  industry: '',
  minRoe: undefined as number | undefined,
  maxPe: undefined as number | undefined,
  maxPb: undefined as number | undefined,
  minNetMargin: undefined as number | undefined,
  minGrossMargin: undefined as number | undefined,
  minRevenueYoy: undefined as number | undefined,
  minNetProfitYoY: undefined as number | undefined,
  minMarketCap: undefined as number | undefined,
})

const sort = ref('marketCap')
const desc = ref(true)

// 预设策略
const presets: { label: string; apply: () => void }[] = [
  {
    label: '高ROE低估',
    apply: () => { Object.assign(filters, { minRoe: 15, maxPe: 20, maxPb: 3, minNetMargin: undefined, minRevenueYoy: undefined, minNetProfitYoY: undefined, minMarketCap: 50 }) },
  },
  {
    label: '高成长',
    apply: () => { Object.assign(filters, { minRevenueYoy: 30, minNetProfitYoY: 30, minRoe: undefined, maxPe: undefined, maxPb: undefined, minNetMargin: undefined, minMarketCap: 50 }) },
  },
  {
    label: '消费白马',
    apply: () => { Object.assign(filters, { minRoe: 15, minNetMargin: 15, minGrossMargin: 30, maxPe: undefined, maxPb: undefined, minRevenueYoy: undefined, minNetProfitYoY: undefined, minMarketCap: 200 }) },
  },
  {
    label: '低估值蓝筹',
    apply: () => { Object.assign(filters, { maxPe: 10, maxPb: 1, minMarketCap: 300, minRoe: undefined, minNetMargin: undefined, minGrossMargin: undefined, minRevenueYoy: undefined, minNetProfitYoY: undefined }) },
  },
]

async function load() {
  loading.value = true
  try {
    const res = await screenStocks({
      keyword: filters.keyword || undefined,
      industry: filters.industry || undefined,
      minRoe: filters.minRoe,
      maxPe: filters.maxPe,
      maxPb: filters.maxPb,
      minNetMargin: filters.minNetMargin,
      minGrossMargin: filters.minGrossMargin,
      minRevenueYoy: filters.minRevenueYoy,
      minNetProfitYoY: filters.minNetProfitYoY,
      minMarketCap: filters.minMarketCap,
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

function search() {
  page.value = 1
  load()
}

function reset() {
  Object.assign(filters, {
    keyword: '', industry: '', minRoe: undefined, maxPe: undefined, maxPb: undefined,
    minNetMargin: undefined, minGrossMargin: undefined, minRevenueYoy: undefined,
    minNetProfitYoY: undefined, minMarketCap: undefined,
  })
  search()
}

// 点击表头排序
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

async function syncNow() {
  try {
    await triggerScreenerSync()
    ElMessage.success('已提交同步（约 1-2 分钟），完成后刷新查看')
  } catch {
    /* 非管理员会 403，由拦截器提示 */
  }
}

onMounted(async () => {
  load()
  fetchIndustries().then((r) => (industries.value = r)).catch(() => {})
})
</script>

<template>
  <div>
    <!-- 筛选条件 -->
    <div class="page-card">
      <div class="preset-row">
        <span class="preset-label">快速策略：</span>
        <el-button v-for="p in presets" :key="p.label" size="small" round @click="p.apply(); search()">{{ p.label }}</el-button>
        <div style="flex: 1" />
        <el-button size="small" @click="syncNow">同步基本面数据</el-button>
      </div>

      <el-form inline class="filter-form">
        <el-form-item label="关键字">
          <el-input v-model="filters.keyword" placeholder="代码 / 名称" clearable style="width: 140px" @keyup.enter="search" />
        </el-form-item>
        <el-form-item label="行业">
          <el-select v-model="filters.industry" clearable filterable placeholder="全部" style="width: 140px">
            <el-option v-for="i in industries" :key="i" :label="i" :value="i" />
          </el-select>
        </el-form-item>
        <el-form-item label="ROE ≥">
          <el-input-number v-model="filters.minRoe" :min="0" :step="5" controls-position="right" style="width: 100px" placeholder="%" />
        </el-form-item>
        <el-form-item label="市盈率 ≤">
          <el-input-number v-model="filters.maxPe" :min="0" :step="5" controls-position="right" style="width: 100px" />
        </el-form-item>
        <el-form-item label="市净率 ≤">
          <el-input-number v-model="filters.maxPb" :min="0" :step="0.5" controls-position="right" style="width: 100px" />
        </el-form-item>
        <el-form-item label="净利率 ≥%">
          <el-input-number v-model="filters.minNetMargin" :min="0" :step="5" controls-position="right" style="width: 100px" />
        </el-form-item>
        <el-form-item label="毛利率 ≥%">
          <el-input-number v-model="filters.minGrossMargin" :min="0" :step="5" controls-position="right" style="width: 100px" />
        </el-form-item>
        <el-form-item label="营收同比 ≥%">
          <el-input-number v-model="filters.minRevenueYoy" :step="10" controls-position="right" style="width: 110px" />
        </el-form-item>
        <el-form-item label="净利同比 ≥%">
          <el-input-number v-model="filters.minNetProfitYoY" :step="10" controls-position="right" style="width: 110px" />
        </el-form-item>
        <el-form-item label="市值 ≥亿">
          <el-input-number v-model="filters.minMarketCap" :min="0" :step="50" controls-position="right" style="width: 110px" />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="search">筛 选</el-button>
          <el-button @click="reset">重 置</el-button>
        </el-form-item>
      </el-form>
    </div>

    <!-- 结果 -->
    <div class="page-card">
      <div class="result-head">
        <span>共 <b>{{ total }}</b> 只符合条件</span>
        <span class="dim">估值 = 最新交易日 · 业绩 = 最新报告期</span>
      </div>
      <el-table :data="items" v-loading="loading" row-class-name="row-click"
        @row-click="(r: ScreenerItem) => router.push(`/analysis/${r.symbol}`)">
        <el-table-column label="代码" width="90">
          <template #default="{ row }">{{ row.symbol }}</template>
        </el-table-column>
        <el-table-column label="名称" min-width="110">
          <template #default="{ row }">
            {{ row.name }}
            <el-tag v-if="row.industry" size="small" type="info">{{ row.industry }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="最新价" width="90" align="right">
          <template #default="{ row }">{{ row.closePrice.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column width="100" align="right">
          <template #header><span class="th-sort" @click="onSort('pe')">市盈率 {{ sortIcon('pe') }}</span></template>
          <template #default="{ row }">{{ fmt(row.peTtm, 1) }}</template>
        </el-table-column>
        <el-table-column prop="pbMrq" label="市净率" width="85" align="right">
          <template #default="{ row }">{{ fmt(row.pbMrq, 2) }}</template>
        </el-table-column>
        <el-table-column width="90" align="right">
          <template #header><span class="th-sort" @click="onSort('roe')">ROE% {{ sortIcon('roe') }}</span></template>
          <template #default="{ row }"><b>{{ fmt(row.roe, 1) }}</b></template>
        </el-table-column>
        <el-table-column width="95" align="right">
          <template #header><span class="th-sort" @click="onSort('netMargin')">净利率% {{ sortIcon('netMargin') }}</span></template>
          <template #default="{ row }">{{ fmt(row.netMargin, 1) }}</template>
        </el-table-column>
        <el-table-column label="毛利率%" width="90" align="right">
          <template #default="{ row }">{{ fmt(row.grossMargin, 1) }}</template>
        </el-table-column>
        <el-table-column width="105" align="right">
          <template #header><span class="th-sort" @click="onSort('revenueYoy')">营收同比 {{ sortIcon('revenueYoy') }}</span></template>
          <template #default="{ row }">
            <span :class="pctClass(row.revenueYoY)">{{ row.revenueYoY == null ? '—' : (row.revenueYoY >= 0 ? '+' : '') + row.revenueYoY.toFixed(1) + '%' }}</span>
          </template>
        </el-table-column>
        <el-table-column width="105" align="right">
          <template #header><span class="th-sort" @click="onSort('netProfitYoy')">净利同比 {{ sortIcon('netProfitYoy') }}</span></template>
          <template #default="{ row }">
            <span :class="pctClass(row.netProfitYoY)">{{ row.netProfitYoY == null ? '—' : (row.netProfitYoY >= 0 ? '+' : '') + row.netProfitYoY.toFixed(1) + '%' }}</span>
          </template>
        </el-table-column>
        <el-table-column width="110" align="right">
          <template #header><span class="th-sort" @click="onSort('marketCap')">市值(亿) {{ sortIcon('marketCap') }}</span></template>
          <template #default="{ row }">{{ row.marketCap == null ? '—' : row.marketCap.toFixed(0) }}</template>
        </el-table-column>
        <template #empty>
          <el-empty description="无符合条件的数据（或基本面数据尚未采集完成，可点上方“同步基本面数据”）" :image-size="60" />
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
.preset-row { display: flex; align-items: center; gap: 8px; margin-bottom: 14px; flex-wrap: wrap; }
.preset-label { color: #909399; font-size: 13px; }
.filter-form :deep(.el-form-item) { margin-bottom: 8px; margin-right: 14px; }
.result-head { display: flex; justify-content: space-between; margin-bottom: 10px; font-size: 13px; color: #606266; }
.dim { color: #c0c4cc; }
.th-sort { cursor: pointer; user-select: none; }
.th-sort:hover { color: #409eff; }
.pager { display: flex; justify-content: flex-end; margin-top: 12px; }
</style>
