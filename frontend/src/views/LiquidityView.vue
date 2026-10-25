<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import type {
  LiquidityOverview, ShiborData, MoneySupplyData, ReserveRatioData, LprData, OmoItem,
} from '../types'
import {
  fetchLiquidityOverview, fetchShibor, fetchMoneySupply, fetchReserveRatios,
  fetchLpr, fetchOmo,
} from '../api/liquidity'
import RefreshButton from '../components/RefreshButton.vue'
import MultiLineChart from '../components/MultiLineChart.vue'
import OmoGanttChart from '../components/OmoGanttChart.vue'

const overview = ref<LiquidityOverview | null>(null)
const shibor = ref<ShiborData | null>(null)
const supply = ref<MoneySupplyData | null>(null)
const reserve = ref<ReserveRatioData | null>(null)
const lpr = ref<LprData | null>(null)
const omo = ref<OmoItem[]>([])
const loading = ref(false)
const shiborDays = ref(90)
const omoDays = ref(90)

const supplyView = ref<'yoy' | 'amount'>('yoy')

async function load() {
  loading.value = true
  try {
    const [ov, sb, sp, rr, lp, om] = await Promise.all([
      fetchLiquidityOverview(),
      fetchShibor(shiborDays.value),
      fetchMoneySupply(24),
      fetchReserveRatios(),
      fetchLpr(),
      fetchOmo(omoDays.value).catch(() => null),
    ])
    overview.value = ov
    shibor.value = sb
    supply.value = sp
    reserve.value = rr
    lpr.value = lp
    omo.value = om?.items ?? []
  } finally {
    loading.value = false
  }
}

async function reloadOmo() {
  const om = await fetchOmo(omoDays.value).catch(() => null)
  omo.value = om?.items ?? []
}

async function reloadShibor() {
  shibor.value = null
  shibor.value = await fetchShibor(shiborDays.value)
}

// Shibor 折线序列
const shiborTimes = computed(() => shibor.value?.points.map((p) => p.date) ?? [])
const shiborSeries = computed(() => [
  { name: '隔夜', data: shibor.value?.points.map((p) => p.on) ?? [] },
  { name: '1周', data: shibor.value?.points.map((p) => p.w1) ?? [] },
  { name: '2周', data: shibor.value?.points.map((p) => p.w2) ?? [] },
  { name: '1个月', data: shibor.value?.points.map((p) => p.m1) ?? [] },
])

// 货币供应（同比增速 / 绝对量）
const supplyTimes = computed(() => supply.value?.points.map((p) => p.month) ?? [])
const supplySeries = computed(() =>
  supplyView.value === 'yoy'
    ? [
        { name: 'M2 同比', data: supply.value?.points.map((p) => p.m2YoY) ?? [], color: '#409eff' },
        { name: 'M1 同比', data: supply.value?.points.map((p) => p.m1YoY) ?? [], color: '#e6a23c' },
        { name: 'M0 同比', data: supply.value?.points.map((p) => p.m0YoY) ?? [], color: '#67c23a' },
      ]
    : [
        { name: 'M2（万亿元）', data: supply.value?.points.map((p) => +(p.m2 / 10000).toFixed(1)) ?? [], color: '#409eff' },
        { name: 'M1（万亿元）', data: supply.value?.points.map((p) => +(p.m1 / 10000).toFixed(1)) ?? [], color: '#e6a23c' },
        { name: 'M0（万亿元）', data: supply.value?.points.map((p) => +(p.m0 / 10000).toFixed(1)) ?? [], color: '#67c23a' },
      ])

// LPR 阶梯线
const lprTimes = computed(() => lpr.value?.items.map((i) => i.date.slice(0, 7)) ?? [])
const lprSeries = computed(() => [
  { name: 'LPR 1年', data: lpr.value?.items.map((i) => i.lpr1Y) ?? [], color: '#409eff' },
  { name: 'LPR 5年', data: lpr.value?.items.map((i) => i.lpr5Y) ?? [], color: '#e6a23c' },
])

// 存准率阶梯线（近 10 年）
const reserveRecent = computed(() => (reserve.value?.items ?? []).filter((i) => i.date >= '2015-01-01'))
const reserveTimes = computed(() => reserveRecent.value.map((i) => i.date.slice(0, 7)))
const reserveSeries = computed(() => [
  { name: '大型机构', data: reserveRecent.value.map((i) => i.large), color: '#409eff' },
  { name: '中小型机构', data: reserveRecent.value.map((i) => i.small), color: '#67c23a' },
])

// 存准率最近调整记录（倒序 6 条）
const reserveRecentChanges = computed(() =>
  [...(reserve.value?.items ?? [])].reverse().slice(0, 6))

// 央行逆回购到期统计（甘特图上方摘要）
function localDate(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}
function addDays(d: Date, n: number) {
  const r = new Date(d)
  r.setDate(r.getDate() + n)
  return r
}
const todayStr = localDate(new Date())
const omoSummary = computed(() => {
  const live = omo.value.filter((i) => i.maturityDate >= todayStr)
  const sum = (list: OmoItem[]) => list.reduce((s, i) => s + i.amount, 0)
  const between = (from: Date, to: Date) =>
    live.filter((i) => i.maturityDate >= localDate(from) && i.maturityDate < localDate(to))
  const now = new Date()
  return {
    live: sum(live),
    week: sum(between(now, addDays(now, 7))),
    nextWeek: sum(between(addDays(now, 7), addDays(now, 14))),
    month: sum(between(now, addDays(now, 30))),
  }
})

function pctClass(v: number) {
  return v >= 0 ? 'up' : 'down'
}

onMounted(load)
</script>

<template>
  <div v-loading="loading">
    <div class="page-card head">
      <div class="title-row">
        <h3>🏦 央行流动性分析</h3>
        <span class="sub">Shibor 资金面 · 货币供应 M0/M1/M2 · 政策利率（LPR / 存准率）</span>
        <div style="flex: 1" />
        <RefreshButton category="liquidity" label="同步最新数据" @done="load" />
        <el-button size="small" :loading="loading" @click="load">刷新</el-button>
      </div>
    </div>

    <!-- 概览卡片 -->
    <div class="stat-cards" style="margin-bottom: 16px" v-if="overview">
      <div class="stat-card">
        <div class="label">Shibor 隔夜 <span class="dim">{{ overview.latestShiborDate }}</span></div>
        <div class="value">{{ overview.shiborOn.toFixed(3) }}%</div>
        <div class="sub" :class="pctClass(overview.shiborOnChangeBp)">
          较上日 {{ overview.shiborOnChangeBp >= 0 ? '+' : '' }}{{ overview.shiborOnChangeBp.toFixed(2) }} bp
        </div>
      </div>
      <div class="stat-card">
        <div class="label">Shibor 1周</div>
        <div class="value">{{ overview.shibor1W.toFixed(3) }}%</div>
        <div class="sub" :class="pctClass(overview.shibor1WChangeBp)">
          较上日 {{ overview.shibor1WChangeBp >= 0 ? '+' : '' }}{{ overview.shibor1WChangeBp.toFixed(2) }} bp
        </div>
      </div>
      <div class="stat-card">
        <div class="label">M2 同比 <span class="dim">{{ overview.latestSupplyMonth }}</span></div>
        <div class="value" :class="pctClass(overview.m2YoY)">{{ overview.m2YoY.toFixed(1) }}%</div>
        <div class="sub">M1 {{ overview.m1YoY.toFixed(1) }}% · M0 {{ overview.m0YoY.toFixed(1) }}%</div>
      </div>
      <div class="stat-card">
        <div class="label">LPR <span class="dim">{{ overview.latestLprDate }}</span></div>
        <div class="value">{{ overview.lpr1Y.toFixed(2) }}<span class="unit">%</span> / {{ overview.lpr5Y.toFixed(2) }}<span class="unit">%</span></div>
        <div class="sub">1年期 / 5年期以上</div>
      </div>
      <div class="stat-card">
        <div class="label">存款准备金率 <span class="dim">{{ overview.latestReserveDate }}</span></div>
        <div class="value">{{ overview.reserveLarge.toFixed(1) }}%<span class="unit"> / {{ overview.reserveSmall.toFixed(1) }}%</span></div>
        <div class="sub">大型 / 中小型机构（现行）</div>
      </div>
    </div>

    <!-- 央行逆回购投放与到期 -->
    <div class="page-card">
      <div class="chart-toolbar">
        <span class="chart-title">央行逆回购投放与到期 —— 柱高=投放量（亿元），柱宽=存续期（操作日 → 到期日），蓝虚线右侧为尚未到期</span>
        <div style="flex: 1" />
        <el-radio-group v-model="omoDays" size="small" @change="reloadOmo">
          <el-radio-button :value="30">近1月</el-radio-button>
          <el-radio-button :value="90">近3月</el-radio-button>
          <el-radio-button :value="180">近半年</el-radio-button>
        </el-radio-group>
      </div>
      <div class="omo-summary" v-if="omo.length">
        <span>未到期存续 <b>{{ omoSummary.live.toLocaleString() }}</b> 亿</span>
        <span>本周到期 <b>{{ omoSummary.week.toLocaleString() }}</b> 亿</span>
        <span>下周到期 <b>{{ omoSummary.nextWeek.toLocaleString() }}</b> 亿</span>
        <span>未来30天到期 <b>{{ omoSummary.month.toLocaleString() }}</b> 亿</span>
      </div>
      <OmoGanttChart :items="omo" />
    </div>

    <!-- Shibor 走势 -->
    <div class="page-card">
      <div class="chart-toolbar">
        <span class="chart-title">Shibor 走势（%）——银行间资金面松紧的直接指标，利率走高代表资金趋紧</span>
        <div style="flex: 1" />
        <el-radio-group v-model="shiborDays" size="small" @change="reloadShibor">
          <el-radio-button :value="30">近1月</el-radio-button>
          <el-radio-button :value="90">近3月</el-radio-button>
          <el-radio-button :value="180">近半年</el-radio-button>
        </el-radio-group>
      </div>
      <MultiLineChart :times="shiborTimes" :series="shiborSeries" unit="%" />
    </div>

    <!-- 货币供应 -->
    <div class="page-card">
      <div class="chart-toolbar">
        <span class="chart-title">货币供应量 M0 / M1 / M2</span>
        <div style="flex: 1" />
        <el-radio-group v-model="supplyView" size="small">
          <el-radio-button value="yoy">同比增速</el-radio-button>
          <el-radio-button value="amount">总量</el-radio-button>
        </el-radio-group>
      </div>
      <MultiLineChart
        :times="supplyTimes"
        :series="supplySeries"
        :unit="supplyView === 'yoy' ? '%' : '万亿'" />
    </div>

    <!-- LPR 与存准率 -->
    <div class="two-col">
      <div class="page-card">
        <div class="chart-toolbar"><span class="chart-title">LPR 报价历史（%）</span></div>
        <MultiLineChart :times="lprTimes" :series="lprSeries" step unit="%" />
      </div>
      <div class="page-card">
        <div class="chart-toolbar"><span class="chart-title">存款准备金率（2015 年以来，%）</span></div>
        <MultiLineChart :times="reserveTimes" :series="reserveSeries" step unit="%" />
      </div>
    </div>

    <!-- 存准率调整记录 -->
    <div class="page-card">
      <div class="chart-toolbar"><span class="chart-title">存准率最近调整记录</span></div>
      <el-table :data="reserveRecentChanges" size="small">
        <el-table-column prop="date" label="生效日期" width="140" />
        <el-table-column label="大型机构" align="center">
          <template #default="{ row }">{{ row.large.toFixed(1) }}%</template>
        </el-table-column>
        <el-table-column label="中小型机构" align="center">
          <template #default="{ row }">{{ row.small.toFixed(1) }}%</template>
        </el-table-column>
        <el-table-column label="变动" align="center">
          <template #default="{ row }">
            <span v-if="row.change != null" :class="pctClass(row.change)">
              {{ row.change > 0 ? '+' : '' }}{{ row.change.toFixed(1) }} pct
            </span>
            <span v-else>—</span>
          </template>
        </el-table-column>
      </el-table>
    </div>
  </div>
</template>

<style scoped>
.head .title-row { display: flex; align-items: center; gap: 12px; }
.head h3 { font-size: 17px; }
.sub { color: #909399; font-size: 13px; }
.dim { color: #c0c4cc; font-size: 12px; font-weight: normal; }
.unit { font-size: 13px; color: #909399; margin-left: 2px; }
.chart-toolbar { display: flex; align-items: center; gap: 12px; margin-bottom: 10px; }
.chart-title { color: #606266; font-size: 13px; }
.omo-summary { display: flex; flex-wrap: wrap; gap: 20px; margin-bottom: 8px; color: #909399; font-size: 13px; }
.omo-summary b { color: #303133; }
.two-col { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
@media (max-width: 1100px) { .two-col { grid-template-columns: 1fr; } }
</style>
