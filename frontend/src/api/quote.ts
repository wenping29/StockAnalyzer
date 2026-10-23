import http from './http'
import type {
  SearchItem, KLineResponse, PerformanceData, MoneyFlowData, WatchlistItem, AdminUser, MarketIndex, MarketVolume,
} from '../types'

export async function fetchMarketOverview(): Promise<MarketIndex[]> {
  const { data } = await http.get('/quote/market-overview')
  return data
}

export async function fetchMarketVolume(days = 30): Promise<MarketVolume> {
  const { data } = await http.get('/quote/market-volume', { params: { days } })
  return data
}

export async function searchSymbol(keyword: string): Promise<SearchItem[]> {
  const { data } = await http.get('/quote/search', { params: { keyword } })
  return data
}

export async function fetchKLine(symbol: string, period: 'hour' | 'day' | 'week' | 'month', limit = 120): Promise<KLineResponse> {
  const { data } = await http.get('/quote/kline', { params: { symbol, period, limit } })
  return data
}

export async function fetchPerformance(symbol: string): Promise<PerformanceData> {
  const { data } = await http.get('/quote/performance', { params: { symbol } })
  return data
}

export async function fetchMoneyFlow(symbol: string, days = 30): Promise<MoneyFlowData> {
  const { data } = await http.get('/quote/moneyflow', { params: { symbol, days } })
  return data
}

export async function fetchWatchlist(): Promise<WatchlistItem[]> {
  const { data } = await http.get('/watchlist')
  return data
}

export async function addToWatchlist(symbol: string): Promise<{ message: string }> {
  const { data } = await http.post('/watchlist', { symbol })
  return data
}

export async function removeFromWatchlist(symbol: string): Promise<void> {
  await http.delete(`/watchlist/${symbol}`)
}

// ---------- 管理后台 ----------
export async function fetchUsers(): Promise<AdminUser[]> {
  const { data } = await http.get('/admin/users')
  return data
}

export async function updateUserRole(id: string, role: string): Promise<void> {
  await http.put(`/admin/users/${id}/role`, { role })
}

export async function updateUserEnabled(id: string, enabled: boolean): Promise<void> {
  await http.put(`/admin/users/${id}/enabled`, { enabled })
}

export async function triggerCollect(what: 'initial' | 'daily' | 'minutes'): Promise<void> {
  await http.post(`/admin/collect/${what}`)
}
