import axios from 'axios'
import { ElMessage } from 'element-plus'

// API 基地址：运行时配置 public/config.js 的 apiBase（空 = 同域，走代理）
declare global {
  interface Window {
    __APP_CONFIG__?: { apiBase?: string }
  }
}
const API_BASE = (window.__APP_CONFIG__?.apiBase ?? '').replace(/\/+$/, '')

const http = axios.create({ baseURL: `${API_BASE}/api`, timeout: 60000 })

http.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// ---------- 自动续期 ----------
// 并发的多个 401 共享同一次刷新（避免重复用同一个 refreshToken 换新）
let refreshing: Promise<string | null> | null = null

async function refreshAccessToken(): Promise<string | null> {
  const refreshToken = localStorage.getItem('refreshToken')
  if (!refreshToken) return null
  try {
    // 用独立的 axios 实例（避免走本拦截器造成循环）
    const res = await axios.post('/api/auth/refresh', { refreshToken }, { timeout: 15000 })
    const data = res.data
    localStorage.setItem('token', data.accessToken)
    localStorage.setItem('refreshToken', data.refreshToken)
    localStorage.setItem('user', JSON.stringify(data.user))
    return data.accessToken as string
  } catch {
    return null
  }
}

function forceLogout() {
  localStorage.removeItem('token')
  localStorage.removeItem('refreshToken')
  localStorage.removeItem('user')
  if (location.pathname !== '/login') location.href = '/login'
}

http.interceptors.response.use(
  (res) => res,
  async (err) => {
    const config = err.config ?? {}
    const status = err.response?.status
    const isAuthUrl = typeof config.url === 'string' && config.url.includes('/auth/')

    if (status === 401 && !config._retried && !isAuthUrl) {
      // 访问令牌过期：尝试静默续期后重放原请求
      config._retried = true
      refreshing = refreshing ?? refreshAccessToken()
      const newToken = await refreshing
      refreshing = null

      if (newToken) {
        config.headers = { ...config.headers, Authorization: `Bearer ${newToken}` }
        return http(config)
      }
      // 刷新令牌也失效：清状态回登录页
      forceLogout()
      ElMessage.warning('登录已过期，请重新登录')
      return Promise.reject(err)
    }

    if (status === 401 && isAuthUrl) {
      // 登录/刷新接口本身的 401（密码错误、刷新令牌无效）
      if (typeof config.url === 'string' && config.url.includes('/auth/refresh')) forceLogout()
    }

    if (status !== 401) {
      const msg = err.response?.data?.message
      ElMessage.error(typeof msg === 'string' ? msg : err.message || '请求失败')
    }
    return Promise.reject(err)
  },
)

export default http
