import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', component: () => import('../views/LoginView.vue'), meta: { public: true } },
    { path: '/', component: () => import('../views/DashboardView.vue') },
    { path: '/indices', component: () => import('../views/IndicesView.vue') },
    { path: '/stocks', component: () => import('../views/StocksView.vue') },
    { path: '/funds', component: () => import('../views/FundsView.vue') },
    { path: '/volume', component: () => import('../views/VolumeView.vue') },
    { path: '/watchlist', component: () => import('../views/WatchlistView.vue') },
    { path: '/screener', component: () => import('../views/ScreenerView.vue') },
    { path: '/liquidity', component: () => import('../views/LiquidityView.vue') },
    { path: '/analysis/:symbol', component: () => import('../views/AnalysisView.vue'), props: true },
    { path: '/admin', component: () => import('../views/AdminView.vue'), meta: { admin: true } },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (!to.meta.public && !auth.isLogin) return '/login'
  if (to.meta.admin && !auth.isAdmin) return '/'
})

export default router
