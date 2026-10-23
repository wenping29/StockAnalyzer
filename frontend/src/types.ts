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
