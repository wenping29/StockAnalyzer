import http from './http'
import type { ScreenerResponse, ScreenerQuery } from '../types'

export async function screenStocks(query: ScreenerQuery): Promise<ScreenerResponse> {
  // 去掉空值参数，避免发送空字符串
  const params: Record<string, unknown> = {}
  for (const [k, v] of Object.entries(query)) {
    if (v !== undefined && v !== null && v !== '') params[k] = v
  }
  const { data } = await http.get('/screener', { params })
  return data
}

export async function fetchIndustries(): Promise<string[]> {
  const { data } = await http.get('/screener/industries')
  return data
}

export async function triggerScreenerSync(): Promise<void> {
  await http.post('/screener/sync')
}
