<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import * as echarts from 'echarts'
import type { SearchItem, WatchlistItem, AlertRuleDto } from '../types'
import {
  fetchWatchlist, addToWatchlist, removeFromWatchlist, searchSymbol,
} from '../api/quote'
import { fetchAlerts, createAlert, toggleAlert, deleteAlert } from '../api/alerts'

const router = useRouter()
const items = ref<WatchlistItem[]>([])
const loading = ref(false)

// ---------- 提醒 ----------
const alerts = ref<AlertRuleDto[]>([])
const alertDialog = ref(false)
const alertTarget = ref<WatchlistItem | null>(null)
const alertForm = ref({ ruleType: 'PriceAbove' as AlertRuleDto['ruleType'], threshold: undefined as number | undefined })

const RULE_LABEL: Record<string, string> = {
  PriceAbove: '价格高于', PriceBelow: '价格低于', ChangePctUp: '日涨幅超%', ChangePctDown: '日跌幅超%',
}

async function loadAlerts() {
  try {
    alerts.value = await fetchAlerts()
  } catch {
    alerts.value = []
  }
}

function openAlertDialog(row: WatchlistItem) {
  alertTarget.value = row
  alertForm.value = { ruleType: 'PriceAbove', threshold: row.latestPrice ? +row.latestPrice.toFixed(2) : undefined }
  alertDialog.value = true
}

async function submitAlert() {
  if (!alertTarget.value || alertForm.value.threshold == null) return ElMessage.warning('请填写阈值')
  await createAlert(alertTarget.value.symbol, alertForm.value.ruleType, alertForm.value.threshold)
  ElMessage.success('提醒已创建')
  alertDialog.value = false
  loadAlerts()
}

async function onToggleAlert(rule: AlertRuleDto) {
  await toggleAlert(rule.id)
  loadAlerts()
}

async function onDeleteAlert(rule: AlertRuleDto) {
  await deleteAlert(rule.id)
  ElMessage.success('已删除')
  loadAlerts()
}

// 远程搜索（el-autocomplete：fetch-suggestions 回调模式）
const searchKeyword = ref("")
let searchTimer: ReturnType<typeof setTimeout> | null = null

function querySearch(keyword: string, cb: (results: (SearchItem & { title: string })[]) => void) {
  if (searchTimer) clearTimeout(searchTimer)
  if (!keyword.trim()) {
    cb([])
    return
  }
  searchTimer = setTimeout(async () => {
    try {
      const items = await searchSymbol(keyword.trim())
      cb(items.map((i) => ({ ...i, title: `${i.name} (${i.symbol})` })))
    } catch {
      cb([])
    }
  }, 300)
}

async function add(item: SearchItem & { title?: string }) {
  const res = await addToWatchlist(item.symbol)
  ElMessage.success(res.message)
  searchKeyword.value = ""
  load()
}

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
  window.addEventListener('resize', resize)
  load()
  loadAlerts()
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', resize)
  rankChart?.dispose()
})

// 收益排行图：容器在 v-if 内，等数据渲染出 DOM 后再懒初始化
watch(items, async () => {
  if (!items.value.length) return
  await nextTick()
  if (!rankChart && rankEl.value) rankChart = echarts.init(rankEl.value!)
  renderRank()
})
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

    <!-- 提醒规则 -->
    <div class="page-card" v-if="alerts.length">
      <div class="chart-toolbar"><span class="chart-title">价格提醒（触发后自动停用，可重新启用）</span></div>
      <el-table :data="alerts" size="small">
        <el-table-column label="标的" width="150">
          <template #default="{ row }">{{ row.name ?? row.symbol }} <span class="dim">{{ row.symbol }}</span></template>
        </el-table-column>
        <el-table-column label="规则" width="160">
          <template #default="{ row }">{{ RULE_LABEL[row.ruleType] }} {{ row.threshold }}</template>
        </el-table-column>
        <el-table-column label="状态" width="90">
          <template #default="{ row }">
            <el-tag size="small" :type="row.enabled ? 'success' : 'info'">{{ row.enabled ? '监控中' : '已触发/停用' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="最近触发" min-width="170">
          <template #default="{ row }">
            <span v-if="row.lastTriggeredAt">
              {{ new Date(row.lastTriggeredAt).toLocaleString() }} · 价格 {{ row.lastTriggeredPrice }}
            </span>
            <span v-else class="dim">未触发</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="130" align="center">
          <template #default="{ row }">
            <el-button link size="small" @click="onToggleAlert(row)">{{ row.enabled ? '停用' : '重启' }}</el-button>
            <el-button link size="small" type="danger" @click="onDeleteAlert(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 自选列表 -->
    <div class="page-card">
      <div class="toolbar">
        <el-autocomplete
          v-model="searchKeyword"
          :fetch-suggestions="querySearch"
          clearable
          placeholder="搜索代码 / 拼音 / 名称（如 600519、maotai、茅台）"
          style="width: 420px"
          value-key="title"
          @select="add"
        >
          <template #default="{ item }">
            <span>{{ item.name }}</span>
            <span style="float: right; color: #909399; font-size: 12px">
              {{ item.symbol }} · {{ typeLabel[item.type] ?? item.type }}
            </span>
          </template>
        </el-autocomplete>
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
        <el-table-column label="操作" width="130" align="center">
          <template #default="{ row }">
            <el-button link type="primary" @click.stop="openAlertDialog(row)">提醒</el-button>
            <el-button link type="danger" @click.stop="remove(row)">移除</el-button>
          </template>
        </el-table-column>
        <template #empty>
          <el-empty description="自选为空，搜索添加标的，或等待首次自动采集（示例标的）" />
        </template>
      </el-table>
    </div>

    <!-- 创建提醒对话框 -->
    <el-dialog v-model="alertDialog" :title="`创建提醒 - ${alertTarget?.name ?? ''} (${alertTarget?.symbol ?? ''})`" width="400px">
      <el-form label-width="90px">
        <el-form-item label="规则">
          <el-select v-model="alertForm.ruleType">
            <el-option value="PriceAbove" label="价格高于（元）" />
            <el-option value="PriceBelow" label="价格低于（元）" />
            <el-option value="ChangePctUp" label="日涨幅超过（%）" />
            <el-option value="ChangePctDown" label="日跌幅超过（%）" />
          </el-select>
        </el-form-item>
        <el-form-item label="阈值">
          <el-input-number v-model="alertForm.threshold" :step="alertForm.ruleType.startsWith('Price') ? 0.1 : 1"
            :precision="2" controls-position="right" style="width: 180px" />
        </el-form-item>
        <el-form-item>
          <span class="dim">交易时段每 5 分钟检查一次，触发后自动停用</span>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="alertDialog = false">取消</el-button>
        <el-button type="primary" @click="submitAlert">创建</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.toolbar { display: flex; align-items: center; gap: 12px; margin-bottom: 14px; }
.hint { color: #909399; font-size: 12px; }
.chart-toolbar { display: flex; align-items: center; margin-bottom: 8px; }
.chart-title { color: #606266; font-size: 13px; }
.dim { color: #c0c4cc; font-size: 12px; }
</style>
