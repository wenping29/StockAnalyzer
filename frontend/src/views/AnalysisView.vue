<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { KLinePoint, PerformanceData, MoneyFlowData } from '../types'
import { fetchKLine, fetchPerformance, fetchMoneyFlow } from '../api/quote'
import { useAuthStore } from '../stores/auth'
import KLineChart from '../components/KLineChart.vue'
import TrendChart from '../components/TrendChart.vue'
import MoneyFlowChart from '../components/MoneyFlowChart.vue'

const props = defineProps<{ symbol: string }>()
const router = useRouter()
const auth = useAuthStore()

const perf = ref<PerformanceData | null>(null)
const points = ref<KLinePoint[]>([])
const flow = ref<MoneyFlowData | null>(null)
const loading = ref(false)
const flowDays = ref(20)
const flowDenied = ref(false)

const period = ref<'hour' | 'day' | 'week' | 'month'>('day')

const isFund = computed(() => perf.value?.type === 'Fund')
const typeLabel = computed(() =>
  ({ Stock: '股票', Etf: 'ETF', Fund: '场外基金' })[perf.value?.type ?? ''] ?? '')

const periods = computed(() => {
  const list: { key: 'hour' | 'day' | 'week' | 'month'; label: string; locked?: boolean }[] = []
  if (!isFund.value) {
    // 场外基金没有小时数据；小时线需要 VIP
    list.push({ key: 'hour', label: '小时', locked: !auth.isVip })
  }
  list.push({ key: 'day', label: '日' }, { key: 'week', label: '周' }, { key: 'month', label: '月' })
  return list
})

async function loadPerf() {
  perf.value = await fetchPerformance(props.symbol)
}

async function loadKLine() {
  loading.value = true
  try {
    const res = await fetchKLine(props.symbol, period.value, 120)
    points.value = res.points
  } catch (e: any) {
    if (e?.response?.status === 403) {
      ElMessage.warning('小时线为 VIP 功能，请联系管理员升级角色')
      period.value = 'day'
    }
  } finally {
    loading.value = false
  }
}

async function loadFlow() {
  flowDenied.value = false
  flow.value = null
  if (isFund.value) return
  try {
    flow.value = await fetchMoneyFlow(props.symbol, flowDays.value)
  } catch (e: any) {
    if (e?.response?.status === 403) flowDenied.value = true
  }
}

function pickPeriod(p: 'hour' | 'day' | 'week' | 'month') {
  if (p === 'hour' && !auth.isVip) {
    ElMessage.warning('小时线为 VIP 功能，请联系管理员升级角色')
    return
  }
  period.value = p
  loadKLine()
}

function pctClass(v: number | null | undefined) {
  return v == null ? '' : v >= 0 ? 'up' : 'down'
}
function fmtPct(v: number | null | undefined) {
  return v == null ? '—' : `${v >= 0 ? '+' : ''}${v.toFixed(2)}%`
}

onMounted(async () => {
  try {
    await loadPerf()
  } catch {
    ElMessage.error('未找到该标的')
    router.push('/')
    return
  }
  await Promise.all([loadKLine(), loadFlow()])
})

watch(flowDays, loadFlow)
</script>

<template>
  <div v-if="perf">
    <!-- 标的信息 + 收益统计卡片 -->
    <div class="page-card head-card">
      <div class="title-row">
        <el-button link @click="router.push('/')">← 返回自选</el-button>
        <h3>{{ perf.name }} <span class="code">{{ perf.symbol }}</span></h3>
        <el-tag>{{ typeLabel }}</el-tag>
        <span class="latest">最新：{{ perf.latestPrice }}（{{ perf.latestDate }}）</span>
      </div>
    </div>

    <div class="stat-cards" style="margin-bottom: 16px">
      <div v-for="w in perf.windows" :key="w.key" class="stat-card">
        <div class="label">{{ w.label }}</div>
        <div class="value" :class="pctClass(w.returnPct)">{{ fmtPct(w.returnPct) }}</div>
      </div>
      <div class="stat-card">
        <div class="label">最大回撤</div>
        <div class="value down">-{{ perf.maxDrawdownPct.toFixed(2) }}%</div>
        <div class="sub">历史区间</div>
      </div>
    </div>

    <!-- K 线 / 趋势 -->
    <div class="page-card">
      <div class="chart-toolbar">
        <div class="period-tabs">
          <span
            v-for="p in periods"
            :key="p.key"
            class="period-item"
            :class="{ active: period === p.key, locked: p.locked }"
            @click="pickPeriod(p.key)"
          >
            {{ p.label }}{{ p.locked ? ' 🔒' : '' }}
          </span>
        </div>
        <span v-if="!isFund" class="chart-title">K线走势（涨红跌绿，下方面量为成交量）</span>
        <span v-else class="chart-title">单位净值走势</span>
      </div>
      <KLineChart v-if="!isFund" :points="points" />
      <TrendChart v-else :points="points" value-label="单位净值" />
    </div>

    <!-- 资金流 -->
    <div class="page-card" v-if="!isFund">
      <div class="chart-toolbar">
        <span class="chart-title">每日资金流向（净流入，单位：亿元）</span>
        <div style="flex: 1" />
        <el-radio-group v-model="flowDays" size="small">
          <el-radio-button :value="5">近5日</el-radio-button>
          <el-radio-button :value="20">近20日</el-radio-button>
          <el-radio-button :value="60">近60日</el-radio-button>
        </el-radio-group>
      </div>
      <template v-if="flowDenied">
        <el-alert type="warning" :closable="false" show-icon
          title="资金流明细为 VIP 功能"
          description="普通用户可在自选列表查看近5日主力净流入汇总；升级 VIP 后可查看分类资金流图表。" />
      </template>
      <template v-else-if="flow">
        <div v-if="flow.items.length" style="margin-bottom: 8px">
          区间主力净流入合计：
          <b :class="pctClass(flow.cumulativeMainNet)">
            {{ (flow.cumulativeMainNet / 1e8).toFixed(2) }} 亿
          </b>
        </div>
        <MoneyFlowChart v-if="flow.items.length" :items="flow.items" />
        <el-empty v-else description="暂无资金流数据" :image-size="60" />
      </template>
    </div>
  </div>
</template>

<style scoped>
.head-card .title-row { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
.head-card h3 { font-size: 18px; }
.code { color: #909399; font-size: 14px; font-weight: normal; }
.latest { color: #606266; font-size: 13px; }

.chart-toolbar { display: flex; align-items: center; gap: 16px; margin-bottom: 10px; }
.chart-title { color: #606266; font-size: 13px; }
.period-tabs { display: flex; border: 1px solid #dcdfe6; border-radius: 6px; overflow: hidden; }
.period-item {
  padding: 6px 18px;
  cursor: pointer;
  font-size: 14px;
  color: #606266;
  background: #fff;
  user-select: none;
}
.period-item + .period-item { border-left: 1px solid #dcdfe6; }
.period-item.active { background: #409eff; color: #fff; }
.period-item.locked { color: #c0c4cc; }
</style>
