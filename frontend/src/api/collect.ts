import http from './http'

/** 采集分类（与后端 POST /api/collect/{category} 对应）。 */
export type CollectCategory =
  | 'indices' | 'indicators' | 'intraday' | 'watchlist' | 'minutes'
  | 'stock-list' | 'liquidity' | 'fundamentals' | 'fund-list' | 'all'

/** 采集作业。state 取 Hangfire：Enqueued / Processing / Succeeded / Failed 等。 */
export interface CollectJob {
  jobId: string
  category: string
  state: string
  enqueuedAt?: string
}

/** 触发某分类采集，返回作业（含 jobId 供轮询）。 */
export async function triggerCollect(category: CollectCategory): Promise<CollectJob> {
  const { data } = await http.post<CollectJob>(`/collect/${category}`)
  return data
}

/** 查询采集作业状态。 */
export async function getCollectStatus(jobId: string): Promise<CollectJob> {
  const { data } = await http.get<CollectJob>(`/collect/status/${jobId}`)
  return data
}
