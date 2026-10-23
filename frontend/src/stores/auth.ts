import { defineStore } from 'pinia'
import type { UserInfo } from '../types'
import * as authApi from '../api/auth'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('token'),
    user: JSON.parse(localStorage.getItem('user') || 'null') as UserInfo | null,
  }),

  getters: {
    isLogin: (s) => !!s.token,
    isAdmin: (s) => s.user?.roles?.includes('Admin') ?? false,
    isVip: (s) => {
      const roles = s.user?.roles ?? []
      return roles.includes('VIP') || roles.includes('Admin')
    },
  },

  actions: {
    setSession(token: string, user: UserInfo, refreshToken?: string) {
      this.token = token
      this.user = user
      localStorage.setItem('token', token)
      localStorage.setItem('user', JSON.stringify(user))
      if (refreshToken) localStorage.setItem('refreshToken', refreshToken)
    },

    async login(userName: string, password: string) {
      const res = await authApi.login(userName, password)
      this.setSession(res.accessToken, res.user, res.refreshToken)
    },

    async register(userName: string, password: string, email?: string) {
      const res = await authApi.register(userName, password, email)
      this.setSession(res.accessToken, res.user, res.refreshToken)
    },

    logout() {
      this.token = null
      this.user = null
      localStorage.clear()
    },
  },
})
