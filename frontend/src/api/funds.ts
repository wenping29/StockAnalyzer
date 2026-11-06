import { ElNotification } from 'element-plus'
import http from './http'
import type { FundEstimate } from '../types'

/**
 * 东财被风控阻断时服务端会切到腾讯备用源，前端提示一次即可（各页面共用模块级标记），
 * 避免盘中的 60 秒自动刷新反复弹窗。
 */
let fallbackNotified = false

function notifyFallback(list: FundEstimate[]) {
  if (fallbackNotified || !list.some((e) => e.quoteSource === 'Tencent')) return
  fallbackNotified = true
  ElNotification({
    title: '已切换备用行情源',
    message: '东方财富实时行情接口不可用（服务端被阻断），基金估算已自动改用腾讯行情，数据可能有短暂延迟。',
    type: 'warning',
    duration: 10000,
  })
}

/**
 * 基金当日涨跌估算：按最新季报前十大重仓股的实时涨跌幅、以占净值比例加权。
 * 首次查看某只基金会现拉重仓股（约 1~3 秒），之后走服务端 7 天缓存。
 */
export async function fetchFundEstimates(symbols: string[]): Promise<FundEstimate[]> {
  if (symbols.length === 0) return []
  const { data } = await http.get('/funds/estimates', { params: { symbols: symbols.join(',') } })
  notifyFallback(data)
  return data
}
