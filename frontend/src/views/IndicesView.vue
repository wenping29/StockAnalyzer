<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import type { IndexItem, KLinePoint } from '../types'
import { fetchIndices, fetchIndexKline } from '../api/quote'
import SparkLine from '../components/SparkLine.vue'
import KLineChart from '../components/KLineChart.vue'

const route = useRoute()

const items = ref<IndexItem[]>([])
const loading = ref(true)

const selected = ref<IndexItem | null>(null)
const period = ref<'day' | 'week' | 'month'>('day')
const points = ref<KLinePoint[]>([])
const klineLoading = ref(false)

const groups = computed(() => {
  const map = new Map<string, IndexItem[]>()
  // "辅助指标"分组（如深证综指）用于成交量统计，不在指数页展示
  for (const it of items.value.filter((i) => i.group !== '辅助指标')) {
    if (!map.has(it.group)) map.set(it.group, [])
    map.get(it.group)!.push(it)
  }
  return [...map.entries()].map(([group, list]) => ({ group, list }))
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
  loadKline()
}

async function loadKline() {
  if (!selected.value) return
  klineLoading.value = true
  try {
    const res = await fetchIndexKline(selected.value.code, period.value, 120)
    points.value = res.points
  } finally {
    klineLoading.value = false
  }
}

function pickPeriod(p: 'day' | 'week' | 'month') {
  period.value = p
  loadKline()
}

function pctClass(v: number) {
  return v >= 0 ? 'up' : 'down'
}
function fmtPct(v: number) {
  return `${v >= 0 ? '+' : ''}${v.toFixed(2)}%`
}

watch(period, () => {})

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

    <!-- 选中指数的 K 线 -->
    <div class="page-card" v-if="selected">
      <div class="kline-head">
        <h3>{{ selected.name }} <span class="code">{{ selected.code }}</span></h3>
        <span class="price" :class="pctClass(selected.changePct)">{{ selected.price.toFixed(2) }}</span>
        <span :class="pctClass(selected.changePct)">{{ fmtPct(selected.changePct) }}</span>
        <span v-if="selected.amount" class="amount">成交 {{ selected.amount >= 10000 ? (selected.amount / 10000).toFixed(2) + ' 万亿' : selected.amount.toFixed(0) + ' 亿' }}</span>
        <div style="flex: 1" />
        <div class="period-tabs">
          <span v-for="p in (['day', 'week', 'month'] as const)" :key="p"
            class="period-item" :class="{ active: period === p }" @click="pickPeriod(p)">
            {{ { day: '日', week: '周', month: '月' }[p] }}
          </span>
        </div>
      </div>
      <div v-loading="klineLoading">
        <KLineChart v-if="points.length" :points="points" />
        <el-empty v-else description="暂无数据" :image-size="60" />
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
