import http from './http'
import type { TokenResponse, UserInfo } from '../types'

export async function login(userName: string, password: string): Promise<TokenResponse> {
  const { data } = await http.post('/auth/login', { userName, password })
  return data
}

export async function register(userName: string, password: string, email?: string): Promise<TokenResponse> {
  const { data } = await http.post('/auth/register', { userName, password, email: email || null })
  return data
}

export async function fetchMe(): Promise<UserInfo> {
  const { data } = await http.get('/auth/me')
  return data
}

export async function changePassword(oldPassword: string, newPassword: string): Promise<void> {
  await http.post('/auth/change-password', { oldPassword, newPassword })
}
