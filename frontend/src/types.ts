// 与后端 DTO 对应的类型定义

export interface UserInfo {
  id: string
  userName: string
  email: string | null
  roles: string[]
}

export interface TokenResponse {
  accessToken: string
  refreshToken: string
  expiresInMinutes: number
  user: UserInfo
}

export interface SearchItem {
  symbol: string
  name: string
  type: 'Stock' | 'Etf' | 'Fund'
  marketName: string
}

export interface KLinePoint {
  time: string
  open: number
  high: number
  low: number
  close: number
  volume: number
  changePct: number
}

export interface KLineResponse {
  symbol: string
  name: string
  type: string
  period: string
  points: KLinePoint[]
}

export interface PerfWindow {
  key: string
  label: string
  returnPct: number | null
}

export interface PerformanceData {
  symbol: string
  name: string
  type: string
  latestPrice: number
  latestDate: string
  windows: PerfWindow[]
  maxDrawdownPct: number
}

export interface MoneyFlowItem {
  date: string
  mainNet: number
  largeNet: number
  midNet: number
  smallNet: number
}

export interface MoneyFlowData {
  symbol: string
  items: MoneyFlowItem[]
  cumulativeMainNet: number
}

export interface WatchlistItem {
  symbol: string
  name: string
  type: string
  latestPrice: number | null
  changePct: number | null
  monthReturnPct: number | null
  mainNetIn5d: number | null
}

export interface AdminUser {
  id: string
  userName: string
  email: string | null
  role: string
  enabled: boolean
  createdAt: string
}

// ---------- 央行流动性 ----------

export interface MarketIndex {
  code: string
  name: string
  market: string
  price: number
  changePct: number
  trend: number[]
}

export interface MarketVolumePoint {
  date: string
  sh: number
  sz: number
  total: number
}

export interface MarketVolume {
  todayAmount: number
  yesterdayAmount: number
  changePct: number | null
  points: MarketVolumePoint[]
}

export interface HsgtPoint {
  date: string
  northAmount: number
  southNetInflow: number
  southAmount: number
}

export interface HsgtFlow {
  northTodayAmount: number
  northYesterdayAmount: number
  northAmountChangePct: number | null
  southTodayNetInflow: number
  southYesterdayNetInflow: number
  points: HsgtPoint[]
}

export interface FxPoint {
  date: string
  rate: number
}

export interface FxRate {
  latest: number
  latestDate: string
  dayChangePct: number
  monthChangePct: number | null
  points: FxPoint[]
}

export interface Us10y {
  latest: number
  latestDate: string
  dayChangeBp: number
  monthChangeBp: number | null
  points: FxPoint[]
}

export interface CnBondPoint {
  date: string
  y5: number
  y10: number
  y30: number
}

export interface CnBond {
  latest10y: number
  latestDate: string
  dayChangeBp: number
  monthChangeBp: number | null
  points: CnBondPoint[]
}

export interface IndexItem {
  code: string
  name: string
  group: string
  price: number
  changePct: number
  amount: number
  trend: number[]
}

export interface IndicesData {
  items: IndexItem[]
}

/** 指数日内分时点。time 为 HH:mm；avg 为当日累计均价；volume 单位：手。 */
export interface IndexTrendPoint {
  time: string
  price: number
  volume: number
  avg: number
}

/** 指数日内分时走势。prevClose 为上一交易日收盘价（0 = 库内无历史数据）。 */
export interface IndexTrendData {
  code: string
  name: string
  date: string
  prevClose: number
  points: IndexTrendPoint[]
}

// ---------- 选股 ----------

export interface ScreenerItem {
  symbol: string
  name: string
  industry: string | null
  closePrice: number
  peTtm: number | null
  pbMrq: number | null
  marketCap: number | null
  reportDate: string | null
  roe: number | null
  netMargin: number | null
  grossMargin: number | null
  revenueYoY: number | null
  netProfitYoY: number | null
}

export interface ScreenerResponse {
  total: number
  page: number
  pageSize: number
  items: ScreenerItem[]
}

// ---------- 个股浏览 ----------

export interface StockListItem {
  symbol: string
  name: string
  industry: string | null
  board: string
  closePrice: number
  changePct: number | null
  peTtm: number | null
  pbMrq: number | null
  marketCap: number | null
  roe: number | null
  revenueYoY: number | null
  netProfitYoY: number | null
}

export interface StockListResponse {
  total: number
  page: number
  pageSize: number
  items: StockListItem[]
}

export interface BoardStat {
  key: string
  name: string
  count: number
}

// ---------- 自选提醒 ----------

export interface AlertRuleDto {
  id: number
  symbol: string
  name: string | null
  ruleType: 'PriceAbove' | 'PriceBelow' | 'ChangePctUp' | 'ChangePctDown'
  threshold: number
  enabled: boolean
  createdAt: string
  lastTriggeredAt: string | null
  lastTriggeredPrice: number | null
}

export interface ScreenerQuery {
  keyword?: string
  industry?: string
  minRoe?: number
  maxRoe?: number
  minPe?: number
  maxPe?: number
  maxPb?: number
  minNetMargin?: number
  minGrossMargin?: number
  minRevenueYoy?: number
  minNetProfitYoY?: number
  minMarketCap?: number
  sort?: string
  desc?: boolean
  page?: number
  pageSize?: number
}

export interface LiquidityOverview {
  latestShiborDate: string
  shiborOn: number
  shiborOnChangeBp: number
  shibor1W: number
  shibor1WChangeBp: number
  latestSupplyMonth: string
  m2YoY: number
  m1YoY: number
  m0YoY: number
  lpr1Y: number
  lpr5Y: number
  latestLprDate: string
  reserveLarge: number
  reserveSmall: number
  latestReserveDate: string
}

export interface ShiborPoint {
  date: string
  on: number
  w1: number
  w2: number
  m1: number
}

export interface ShiborData {
  points: ShiborPoint[]
}

export interface MoneySupplyPoint {
  month: string
  m2: number
  m1: number
  m0: number
  m2YoY: number
  m1YoY: number
  m0YoY: number
}

export interface MoneySupplyData {
  points: MoneySupplyPoint[]
}

export interface ReserveRatioItem {
  date: string
  large: number
  small: number
  change: number | null
}

export interface ReserveRatioData {
  items: ReserveRatioItem[]
}

export interface LprItem {
  date: string
  lpr1Y: number
  lpr5Y: number
}

export interface LprData {
  items: LprItem[]
}

export interface OmoItem {
  date: string
  maturityDate: string
  term: string
  termDays: number
  amount: number
  rate: number | null
  announcement: string
}

export interface OmoData {
  items: OmoItem[]
}
