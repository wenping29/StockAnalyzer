import http from './http'
import type { AlertRuleDto } from '../types'

export async function fetchAlerts(): Promise<AlertRuleDto[]> {
  const { data } = await http.get('/alerts')
  return data
}

export async function createAlert(symbol: string, ruleType: string, threshold: number): Promise<void> {
  await http.post('/alerts', { symbol, ruleType, threshold })
}

export async function toggleAlert(id: number): Promise<void> {
  await http.put(`/alerts/${id}/toggle`)
}

export async function deleteAlert(id: number): Promise<void> {
  await http.delete(`/alerts/${id}`)
}
