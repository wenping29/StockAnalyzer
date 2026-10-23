<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import type { MarketIndex, MarketVolume, HsgtFlow, FxRate, Us10y, CnBond, WatchlistItem, LiquidityOverview } from '../types'
import { fetchMarketOverview, fetchMarketVolume, fetchHsgtFlow, fetchFxRate, fetchUs10y, fetchCnBond, fetchWatchlist } from '../api/quote'
import { fetchLiquidityOverview } from '../api/liquidity'
import SparkLine from '../components/SparkLine.vue'
import VolumeBarChart from '../components/VolumeBarChart.vue'
import HsgtChart from '../components/HsgtChart.vue'
import MultiLineChart from '../components/MultiLineChart.vue'

const router = useRouter()

const indices = ref<MarketIndex[]>([])
const volume = ref<MarketVolume | null>(null)
const hsgt = ref<HsgtFlow | null>(null)
const fx = ref<FxRate | null>(null)
const us10y = ref<Us10y | null>(null)
const cnBond = ref<CnBond | null>(null)
const watchlist = ref<WatchlistItem[]>([])
const liquidity = ref<LiquidityOverview | null>(null)
const loading = ref(true)

// 自选统计
const upCount = computed(() => watchlist.value.filter((w) => (w.changePct ?? 0) > 0).length)
const downCount = computed(() => watchlist.value.filter((w) => (w.changePct ?? 0) < 0).length)
const avgChange = computed(() => {
  const items = watchlist.value.filter((w) => w.changePct != null)
  if (!items.length) return null
  return items.reduce((s, w) => s + (w.changePct ?? 0), 0) / items.length
})
const avgMonth = computed(() => {
  const items = watchlist.value.filter((w) => w.monthReturnPct != null)
  if (!items.length) return null
  return items.reduce((s, w) => s + (w.monthReturnPct ?? 0), 0) / items.length
})

// 今日涨跌榜（各取前 5）
const topGainers = computed(() =>
  [...watchlist.value].sort((a, b) => (b.changePct ?? -99) - (a.changePct ?? -99)).slice(0, 5))
const topLosers = computed(() =>
  [...watchlist.value].sort((a, b) => (a.changePct ?? 99) - (b.changePct ?? 99)).slice(0, 5))

function pctClass(v: number | null | undefined) {
  return v == null ? '' : v >= 0 ? 'up' : 'down'
}
function fmtPct(v: number | null | undefined) {
  return v == null ? '—' : `${v >= 0 ? '+' : ''}${v.toFixed(2)}%`
}

async function load() {
  loading.value = true
  try {
    const [idx, vol, hf, fxr, u10, cnb, wl, liq] = await Promise.all([
      fetchMarketOverview(),
      fetchMarketVolume(30).catch(() => null),
      fetchHsgtFlow(30).catch(() => null),
      fetchFxRate(90).catch(() => null),
      fetchUs10y(90).catch(() => null),
      fetchCnBond(90).catch(() => null),
      fetchWatchlist().catch(() => [] as WatchlistItem[]),
      fetchLiquidityOverview().catch(() => null),
    ])
    indices.value = idx
    volume.value = vol
    hsgt.value = hf
    fx.value = fxr
    us10y.value = u10
    cnBond.value = cnb
    watchlist.value = wl
    liquidity.value = liq
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div v-loading="loading">
    <!-- 市场板块指数快照 -->
    <div class="stat-cards index-row">
      <div v-for="idx in indices" :key="idx.code" class="stat-card index-card">
        <div class="idx-head">
          <span class="label">
            <el-tag size="small" effect="plain" round>{{ idx.market }}</el-tag>
            {{ idx.name }}
          </span>
          <span class="value" :class="pctClass(idx.changePct)">{{ idx.price.toFixed(2) }}</span>
        </div>
        <SparkLine :data="idx.trend" :up="(idx.changePct ?? 0) >= 0" />
        <div class="sub" :class="pctClass(idx.changePct)">{{ fmtPct(idx.changePct) }}</div>
      </div>
    </div>

    <!-- 两市成交量 -->
    <div class="page-card" v-if="volume && volume.points.length">
      <div class="card-head">
        <h4>今日两市成交 <span class="vol-amount" :class="pctClass(volume.changePct)">{{ (volume.todayAmount / 10000).toFixed(2) }} 万亿</span></h4>
        <span class="vol-change">
          <template v-if="volume.changePct != null">
            较昨日
            <b :class="pctClass(volume.changePct)">
              {{ (volume.changePct ?? 0) >= 0 ? '放量' : '缩量' }} {{ Math.abs(volume.changePct ?? 0).toFixed(1) }}%
            </b>
            （昨日 {{ (volume.yesterdayAmount / 10000).toFixed(2) }} 万亿）
          </template>
        </span>
      </div>
      <VolumeBarChart :points="volume.points" />
    </div>

    <!-- 北向 / 南向资金 -->
    <div class="page-card" v-if="hsgt && hsgt.points.length">
      <div class="card-head hsgt-head">
        <div class="hsgt-item">
          <span class="hsgt-label">北向（沪深股通）今日成交</span>
          <span class="vol-amount" :class="pctClass(hsgt.northAmountChangePct)">{{ hsgt.northTodayAmount.toFixed(0) }} 亿</span>
          <span class="vol-change" v-if="hsgt.northAmountChangePct != null">
            较昨日 <b :class="pctClass(hsgt.northAmountChangePct)">+{{ hsgt.northAmountChangePct.toFixed(1) }}%</b>
          </span>
          <span class="hsgt-note">净流入自 2024-08 起停止披露，以成交额衡量活跃度</span>
        </div>
        <div class="hsgt-item">
          <span class="hsgt-label">南向（港股通）今日{{ hsgt.southTodayNetInflow >= 0 ? '净流入' : '净流出' }}</span>
          <span class="vol-amount" :class="pctClass(hsgt.southTodayNetInflow)">{{ Math.abs(hsgt.southTodayNetInflow).toFixed(1) }} 亿</span>
          <span class="vol-change">昨日 {{ hsgt.southYesterdayNetInflow >= 0 ? '净流入 ' : '净流出 ' }}{{ Math.abs(hsgt.southYesterdayNetInflow).toFixed(1) }} 亿</span>
        </div>
      </div>
      <HsgtChart :points="hsgt.points" />
    </div>

    <!-- 汇率与利率（三列） -->
    <div class="rate-row">
      <div class="page-card" v-if="fx && fx.points.length">
        <div class="card-head">
          <h4>人民币汇率（离岸）</h4>
          <span class="vol-amount" :class="fx.dayChangePct >= 0 ? 'down' : 'up'">{{ fx.latest.toFixed(4) }}</span>
          <span class="vol-change" :class="fx.dayChangePct >= 0 ? 'down' : 'up'">
            {{ fx.dayChangePct >= 0 ? '贬值' : '升值' }} {{ Math.abs(fx.dayChangePct).toFixed(2) }}%
          </span>
        </div>
        <MultiLineChart
          :times="fx.points.map((p) => p.date.slice(5))"
          :series="[{ name: 'USDCNH', data: fx.points.map((p) => p.rate), color: '#b88230' }]"
          unit="" />
        <div class="hsgt-note">美元兑人民币，上升=贬值（{{ fx.latestDate }}）</div>
      </div>

      <div class="page-card" v-if="us10y && us10y.points.length">
        <div class="card-head">
          <h4>十年期美债</h4>
          <span class="vol-amount" :class="us10y.dayChangeBp >= 0 ? 'up' : 'down'">{{ us10y.latest.toFixed(2) }}%</span>
          <span class="vol-change" :class="us10y.dayChangeBp >= 0 ? 'up' : 'down'">
            {{ us10y.dayChangeBp >= 0 ? '+' : '' }}{{ (us10y.dayChangeBp / 100).toFixed(2) }}bp
          </span>
        </div>
        <MultiLineChart
          :times="us10y.points.map((p) => p.date.slice(5))"
          :series="[{ name: 'US10Y', data: us10y.points.map((p) => p.rate), color: '#5470c6' }]"
          unit="%" />
        <div class="hsgt-note">
          全球资产定价之锚（{{ us10y.latestDate }}）· 近一月 {{ (us10y.monthChangeBp ?? 0) >= 0 ? '+' : '' }}{{ ((us10y.monthChangeBp ?? 0) / 100).toFixed(2) }}bp
        </div>
      </div>

      <div class="page-card" v-if="cnBond && cnBond.points.length">
        <div class="card-head">
          <h4>中国国债（10年期）</h4>
          <span class="vol-amount" :class="cnBond.dayChangeBp >= 0 ? 'up' : 'down'">{{ cnBond.latest10y.toFixed(2) }}%</span>
          <span class="vol-change" :class="cnBond.dayChangeBp >= 0 ? 'up' : 'down'">
            {{ cnBond.dayChangeBp >= 0 ? '+' : '' }}{{ (cnBond.dayChangeBp / 100).toFixed(2) }}bp
          </span>
        </div>
        <MultiLineChart
          :times="cnBond.points.map((p) => p.date.slice(5))"
          :series="[
            { name: '30年期', data: cnBond.points.map((p) => p.y30), color: '#8d4863' },
            { name: '10年期', data: cnBond.points.map((p) => p.y10), color: '#c23531' },
            { name: '5年期', data: cnBond.points.map((p) => p.y5), color: '#e8a0a8' },
          ]"
          unit="%" />
        <div class="hsgt-note">
          5/10/30年期国债收益率（{{ cnBond.latestDate }}）· 中美利差 {{ ((cnBond.latest10y - (us10y?.latest ?? 0)) * 100).toFixed(0) }}bp
        </div>
      </div>
    </div>

    <div class="two-col">
      <!-- 自选概览 -->
      <div class="page-card">
        <div class="card-head">
          <h4>我的自选</h4>
          <el-button link type="primary" @click="router.push('/watchlist')">进入自选分析 →</el-button>
        </div>
        <div class="mini-grid">
          <div class="mini-item">
            <div class="mini-value">{{ watchlist.length }}</div>
            <div class="mini-label">自选标的</div>
          </div>
          <div class="mini-item">
            <div class="mini-value up">{{ upCount }}</div>
            <div class="mini-label">今日上涨</div>
          </div>
          <div class="mini-item">
            <div class="mini-value down">{{ downCount }}</div>
            <div class="mini-label">今日下跌</div>
          </div>
          <div class="mini-item">
            <div class="mini-value" :class="pctClass(avgChange)">{{ fmtPct(avgChange) }}</div>
            <div class="mini-label">今日平均涨跌</div>
          </div>
          <div class="mini-item">
            <div class="mini-value" :class="pctClass(avgMonth)">{{ fmtPct(avgMonth) }}</div>
            <div class="mini-label">近1月平均收益</div>
          </div>
        </div>
        <el-empty v-if="!watchlist.length" description="自选为空，去自选页搜索添加" :image-size="60" />
      </div>

      <!-- 央行流动性速览 -->
      <div class="page-card">
        <div class="card-head">
          <h4>央行流动性速览</h4>
          <el-button link type="primary" @click="router.push('/liquidity')">流动性分析 →</el-button>
        </div>
        <div class="mini-grid" v-if="liquidity">
          <div class="mini-item">
            <div class="mini-value">{{ liquidity.shiborOn.toFixed(3) }}<span class="u">%</span></div>
            <div class="mini-label">Shibor 隔夜 <span :class="pctClass(liquidity.shiborOnChangeBp)">{{ liquidity.shiborOnChangeBp >= 0 ? '+' : '' }}{{ liquidity.shiborOnChangeBp.toFixed(2) }}bp</span></div>
          </div>
          <div class="mini-item">
            <div class="mini-value">{{ liquidity.shibor1W.toFixed(3) }}<span class="u">%</span></div>
            <div class="mini-label">Shibor 1周</div>
          </div>
          <div class="mini-item">
            <div class="mini-value" :class="pctClass(liquidity.m2YoY)">{{ liquidity.m2YoY.toFixed(1) }}%</div>
            <div class="mini-label">M2 同比（{{ liquidity.latestSupplyMonth }}）</div>
          </div>
          <div class="mini-item">
            <div class="mini-value">{{ liquidity.lpr1Y.toFixed(2) }}<span class="u">%</span></div>
            <div class="mini-label">LPR 1年期</div>
          </div>
          <div class="mini-item">
            <div class="mini-value">{{ liquidity.reserveLarge.toFixed(1) }}<span class="u">%</span></div>
            <div class="mini-label">存准率（大型）</div>
          </div>
        </div>
      </div>
    </div>

    <!-- 今日涨跌榜 -->
    <div class="two-col" v-if="watchlist.length">
      <div class="page-card">
        <div class="card-head"><h4>今日涨幅榜</h4></div>
        <div class="rank-list">
          <div v-for="w in topGainers" :key="w.symbol" class="rank-item" @click="router.push(`/analysis/${w.symbol}`)">
            <span class="rank-name">{{ w.name }}</span>
            <span class="rank-code">{{ w.symbol }}</span>
            <span class="rank-pct up">{{ fmtPct(w.changePct) }}</span>
          </div>
        </div>
      </div>
      <div class="page-card">
        <div class="card-head"><h4>今日跌幅榜</h4></div>
        <div class="rank-list">
          <div v-for="w in topLosers" :key="w.symbol" class="rank-item" @click="router.push(`/analysis/${w.symbol}`)">
            <span class="rank-name">{{ w.name }}</span>
            <span class="rank-code">{{ w.symbol }}</span>
            <span class="rank-pct down">{{ fmtPct(w.changePct) }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.index-card { min-width: 0; }
.index-row :deep(.stat-card) { flex: 1 1 190px; }
.idx-head { display: flex; align-items: baseline; justify-content: space-between; margin-bottom: 4px; gap: 4px; }
.idx-head .label { font-size: 13px; white-space: nowrap; display: flex; align-items: center; gap: 4px; }
.idx-head .value { font-size: 18px; }
.u { font-size: 12px; color: #909399; }
.two-col { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 16px; }
.rate-row { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; margin-bottom: 16px; }
@media (max-width: 1000px) { .two-col, .rate-row { grid-template-columns: 1fr; } }
@media (min-width: 1001px) and (max-width: 1300px) { .rate-row { grid-template-columns: 1fr 1fr; } }
.card-head { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
.card-head h4 { font-size: 15px; }
.vol-amount { font-size: 20px; margin-left: 6px; }
.vol-change { font-size: 13px; color: #909399; }
.hsgt-head { justify-content: flex-start; gap: 48px; }
.hsgt-item { display: flex; align-items: baseline; gap: 8px; flex-wrap: wrap; }
.hsgt-label { font-size: 14px; font-weight: 600; }
.hsgt-note { font-size: 12px; color: #c0c4cc; }
.mini-grid { display: flex; flex-wrap: wrap; }
.mini-item { flex: 1; min-width: 88px; text-align: center; padding: 8px 4px; }
.mini-value { font-size: 20px; font-weight: 600; }
.mini-label { font-size: 12px; color: #909399; margin-top: 4px; }
.rank-list { display: flex; flex-direction: column; }
.rank-item { display: flex; align-items: center; padding: 8px 4px; border-bottom: 1px solid #f5f7fa; cursor: pointer; }
.rank-item:last-child { border-bottom: none; }
.rank-item:hover { background: #f5f7fa; }
.rank-name { flex: 1; }
.rank-code { color: #909399; font-size: 12px; margin-right: 16px; }
.rank-pct { width: 80px; text-align: right; font-weight: 600; }
</style>
