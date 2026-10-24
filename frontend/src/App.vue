<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useAuthStore } from './stores/auth'
import { changePassword } from './api/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const showLayout = computed(() => route.path !== '/login')

// 手机端抽屉导航
const isMobile = ref(false)
const drawerOpen = ref(false)
let mq: MediaQueryList | null = null
function onMqChange(e: MediaQueryListEvent | MediaQueryList) {
  isMobile.value = e.matches
  if (!e.matches) drawerOpen.value = false
}
onMounted(() => {
  mq = window.matchMedia('(max-width: 768px)')
  onMqChange(mq)
  mq.addEventListener('change', onMqChange)
})
onBeforeUnmount(() => mq?.removeEventListener('change', onMqChange))

const menuItems = computed(() => {
  const list = [
    { path: '/', label: '🏠 首页' },
    { path: '/indices', label: '📊 指数' },
    { path: '/stocks', label: '💼 个股' },
    { path: '/funds', label: '📈 基金' },
    { path: '/watchlist', label: '⭐ 自选' },
    { path: '/screener', label: '🔍 选股' },
    { path: '/liquidity', label: '🏦 流动性' },
  ]
  if (auth.isAdmin) list.push({ path: '/admin', label: '⚙️ 管理后台' })
  return list
})

function goMenu(path: string) {
  drawerOpen.value = false
  if (route.path !== path) router.push(path)
}

// 修改密码弹窗
const pwdVisible = ref(false)
const pwdForm = ref({ oldPassword: '', newPassword: '', confirm: '' })
const pwdLoading = ref(false)

async function submitPwd() {
  const f = pwdForm.value
  if (!f.oldPassword || !f.newPassword) return ElMessage.warning('请填写完整')
  if (f.newPassword.length < 6) return ElMessage.warning('新密码至少 6 位')
  if (f.newPassword !== f.confirm) return ElMessage.warning('两次输入的新密码不一致')

  pwdLoading.value = true
  try {
    await changePassword(f.oldPassword, f.newPassword)
    ElMessage.success('密码已修改')
    pwdVisible.value = false
    pwdForm.value = { oldPassword: '', newPassword: '', confirm: '' }
  } finally {
    pwdLoading.value = false
  }
}

function logout() {
  auth.logout()
  drawerOpen.value = false
  router.push('/login')
}
</script>

<template>
  <el-container v-if="showLayout" style="height: 100%">
    <el-header class="layout-header">
      <span v-if="isMobile" class="hamburger" @click="drawerOpen = true">☰</span>
      <div class="brand" @click="goMenu('/')">📈 资金流分析系统</div>

      <!-- 桌面横向导航 -->
      <div class="nav" v-if="!isMobile">
        <router-link v-for="m in menuItems" :key="m.path" :to="m.path">{{ m.label.replace(/^\S+\s/, '') }}</router-link>
      </div>
      <div class="spacer" />

      <!-- 桌面用户下拉 -->
      <el-dropdown v-if="!isMobile">
        <span class="user-chip">
          {{ auth.user?.userName }}
          <el-tag size="small" :type="auth.isAdmin ? 'danger' : auth.isVip ? 'warning' : 'info'">
            {{ auth.isAdmin ? '管理员' : auth.isVip ? 'VIP' : '普通用户' }}
          </el-tag>
        </span>
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item @click="pwdVisible = true">修改密码</el-dropdown-item>
            <el-dropdown-item divided @click="logout">退出登录</el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>

      <!-- 手机用户徽标（点击开抽屉） -->
      <span v-if="isMobile" class="user-chip mobile" @click="drawerOpen = true">
        {{ auth.user?.userName }}
      </span>
    </el-header>

    <el-main style="padding: 16px 20px">
      <router-view />
    </el-main>

    <!-- 手机抽屉导航 -->
    <el-drawer v-model="drawerOpen" direction="ltr" size="240px" :with-header="false">
      <div class="drawer-user">
        <div class="drawer-user-name">{{ auth.user?.userName }}</div>
        <el-tag size="small" :type="auth.isAdmin ? 'danger' : auth.isVip ? 'warning' : 'info'">
          {{ auth.isAdmin ? '管理员' : auth.isVip ? 'VIP' : '普通用户' }}
        </el-tag>
      </div>
      <div class="drawer-menu">
        <div
          v-for="m in menuItems"
          :key="m.path"
          class="drawer-item"
          :class="{ active: route.path === m.path }"
          @click="goMenu(m.path)"
        >{{ m.label }}</div>
        <div class="drawer-divider" />
        <div class="drawer-item" @click="pwdVisible = true; drawerOpen = false">🔑 修改密码</div>
        <div class="drawer-item danger" @click="logout">🚪 退出登录</div>
      </div>
    </el-drawer>

    <el-dialog v-model="pwdVisible" title="修改密码" width="420px">
      <el-form label-width="90px">
        <el-form-item label="原密码">
          <el-input v-model="pwdForm.oldPassword" type="password" show-password />
        </el-form-item>
        <el-form-item label="新密码">
          <el-input v-model="pwdForm.newPassword" type="password" show-password />
        </el-form-item>
        <el-form-item label="确认新密码">
          <el-input v-model="pwdForm.confirm" type="password" show-password />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="pwdVisible = false">取消</el-button>
        <el-button type="primary" :loading="pwdLoading" @click="submitPwd">确定</el-button>
      </template>
    </el-dialog>
  </el-container>

  <router-view v-else />
</template>

<style scoped>
.layout-header {
  display: flex;
  align-items: center;
  gap: 28px;
  background: #1f2d3d;
  color: #fff;
  padding: 0 20px;
}
.brand { font-size: 17px; font-weight: 600; cursor: pointer; white-space: nowrap; }
.nav { display: flex; gap: 18px; }
.nav a { color: #c0c4cc; text-decoration: none; font-size: 15px; }
.nav a.router-link-exact-active { color: #409eff; font-weight: 600; }
.spacer { flex: 1; }
.user-chip { color: #fff; cursor: pointer; display: flex; align-items: center; gap: 6px; }

/* 手机端：汉堡按钮 + 抽屉导航（导航链接隐藏） */
.hamburger {
  font-size: 22px;
  cursor: pointer;
  line-height: 1;
  user-select: none;
}
.user-chip.mobile { font-size: 13px; }
.drawer-user {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 4px 16px;
  border-bottom: 1px solid #ebeef5;
  margin-bottom: 8px;
}
.drawer-user-name { font-size: 16px; font-weight: 600; flex: 1; }
.drawer-menu { display: flex; flex-direction: column; }
.drawer-item {
  padding: 13px 8px;
  font-size: 15px;
  color: #303133;
  cursor: pointer;
  border-radius: 6px;
  user-select: none;
}
.drawer-item:hover { background: #f5f7fa; }
.drawer-item.active { background: #ecf5ff; color: #409eff; font-weight: 600; }
.drawer-item.danger { color: #f56c6c; }
.drawer-divider { height: 1px; background: #ebeef5; margin: 8px 0; }

/* 手机端：导航可横向滑动，紧凑布局 */
@media (max-width: 768px) {
  .layout-header { gap: 12px; padding: 0 12px; height: 48px; }
  .brand { font-size: 13px; }
  .nav { gap: 10px; overflow-x: auto; -webkit-overflow-scrolling: touch; scrollbar-width: none; }
  .nav::-webkit-scrollbar { display: none; }
  .nav a { font-size: 13px; white-space: nowrap; }
  .layout-header :deep(.el-header) { padding: 0 12px; }
}
@media (max-width: 420px) {
  .brand { font-size: 12px; }
  .nav a { font-size: 12px; }
}
</style>
