import http from './http'
import type {
  LiquidityOverview, ShiborData, MoneySupplyData, ReserveRatioData, LprData,
} from '../types'

export async function fetchLiquidityOverview(): Promise<LiquidityOverview> {
  const { data } = await http.get('/liquidity/overview')
  return data
}

export async function fetchShibor(days = 90): Promise<ShiborData> {
  const { data } = await http.get('/liquidity/shibor', { params: { days } })
  return data
}

export async function fetchMoneySupply(months = 24): Promise<MoneySupplyData> {
  const { data } = await http.get('/liquidity/money-supply', { params: { months } })
  return data
}

export async function fetchReserveRatios(): Promise<ReserveRatioData> {
  const { data } = await http.get('/liquidity/reserve-ratio')
  return data
}

export async function fetchLpr(): Promise<LprData> {
  const { data } = await http.get('/liquidity/lpr')
  return data
}

export async function triggerLiquiditySync(): Promise<void> {
  await http.post('/liquidity/sync')
}
