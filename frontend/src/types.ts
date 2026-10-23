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
