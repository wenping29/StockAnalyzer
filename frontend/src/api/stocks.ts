import http from './http'
import type { StockListResponse, BoardStat } from '../types'

export async function fetchStocks(params: {
  board?: string
  keyword?: string
  industry?: string
  sort?: string
  desc?: boolean
  page?: number
  pageSize?: number
}): Promise<StockListResponse> {
  const { data } = await http.get('/stocks', { params })
  return data
}

export async function fetchBoardStats(): Promise<BoardStat[]> {
  const { data } = await http.get('/stocks/boards')
  return data
}
