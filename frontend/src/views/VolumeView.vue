<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import type { MarketVolume } from '../types'
import { fetchMarketVolumeSeries } from '../api/quote'
import VolumeBarChart from '../components/VolumeBarChart.vue'

type Granularity = 'day' | 'week' | 'month' | 'quarter' | 'year'

const granularity = ref<Granularity>('day')
const data = ref<MarketVolume | null>(null)
const loading = ref(false)

const TABS: { key: Granularity; label: string; unit: string }[] = [
  { key: 'day', label: '日', unit: '最近一年' },
  { key: 'week', label: '周', unit: '最近五年' },
  { key: 'month', label: '月', unit: '最近七年' },
  { key: 'quarter', label: '季度', unit: '最近七年' },
  { key: 'year', label: '年', unit: '全部历史' },
]

const currentTab = computed(() => TABS.find((t) => t.key === granularity.value)!)

// x 轴标签：日/周用短日期，月/季度/年用粒度 key 原样
const labels = computed(() => {
  if (!data.value) return []
  return data.value.points.map((p) =>
    granularity.value === 'day' || granularity.value === 'week' ? p.date.slice(5) : p.date)
})

async function load() {
  loading.value = true
  try {
    data.value = await fetchMarketVolumeSeries(granularity.value)
  } finally {
    loading.value = false
  }
}

function pick(g: Granularity) {
  granularity.value = g
  load()
}

function pctClass(v: number | null | undefined) {
  return v == null ? '' : v >= 0 ? 'up' : 'down'
}

onMounted(load)
</script>

<template>
  <div>
    <!-- 顶部统计 -->
    <div class="page-card head-card">
      <div class="title-row">
        <el-button link @click="$router.push('/')">← 返回首页</el-button>
        <h3>两市成交量分析</h3>
        <span class="sub">沪市 = 上证指数成交额 · 深市 = 深证综指成交额（覆盖全部个股）</span>
      </div>
    </div>

    <div class="stat-cards" style="margin-bottom: 16px" v-if="data && data.points.length">
      <div class="stat-card">
        <div class="label">最新{{ currentTab.unit === '全部历史' ? '年度' : '一期' }}合计</div>
        <div class="value">{{ (data.todayAmount / 10000).toFixed(2) }}<span class="u">万亿</span></div>
        <div class="sub">{{ data.points[data.points.length - 1].date }}</div>
      </div>
      <div class="stat-card">
        <div class="label">上一期合计</div>
        <div class="value">{{ (data.yesterdayAmount / 10000).toFixed(2) }}<span class="u">万亿</span></div>
        <div class="sub">{{ data.points.length > 1 ? data.points[data.points.length - 2].date : '—' }}</div>
      </div>
      <div class="stat-card">
        <div class="label">环比变化</div>
        <div class="value" :class="pctClass(data.changePct)">
          {{ data.changePct == null ? '—' : `${data.changePct >= 0 ? '+' : ''}${data.changePct.toFixed(2)}%` }}
        </div>
        <div :class="pctClass(data.changePct)">{{ data.changePct != null && data.changePct >= 0 ? '放量' : '缩量' }}</div>
      </div>
      <div class="stat-card">
        <div class="label">区间均值</div>
        <div class="value">{{ (data.points.reduce((s, p) => s + p.total, 0) / data.points.length / 10000).toFixed(2) }}<span class="u">万亿</span></div>
        <div class="sub">当前窗口 {{ data.points.length }} 期</div>
      </div>
    </div>

    <!-- 趋势图 -->
    <div class="page-card">
      <div class="chart-toolbar">
        <span class="chart-title">沪深两市成交量走势（{{ currentTab.unit }}）</span>
        <div style="flex: 1" />
        <div class="period-tabs">
          <span v-for="t in TABS" :key="t.key" class="period-item"
            :class="{ active: granularity === t.key }" @click="pick(t.key)">{{ t.label }}</span>
        </div>
      </div>
      <div v-loading="loading">
        <VolumeBarChart v-if="data && data.points.length" :points="data.points" :labels="labels" />
        <el-empty v-else-if="!loading" description="成交量数据尚未采集完成，请稍后刷新" :image-size="60">
          <el-button @click="load">刷新</el-button>
        </el-empty>
      </div>
    </div>
  </div>
</template>

<style scoped>
.head-card .title-row { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
.head-card h3 { font-size: 17px; }
.sub { color: #909399; font-size: 13px; }
.u { font-size: 13px; color: #909399; margin-left: 2px; }
.chart-toolbar { display: flex; align-items: center; gap: 12px; margin-bottom: 10px; }
.chart-title { color: #606266; font-size: 13px; }
.period-tabs { display: flex; border: 1px solid #dcdfe6; border-radius: 6px; overflow: hidden; }
.period-item { padding: 5px 18px; cursor: pointer; font-size: 14px; color: #606266; background: #fff; user-select: none; }
.period-item + .period-item { border-left: 1px solid #dcdfe6; }
.period-item.active { background: #409eff; color: #fff; }
</style>
