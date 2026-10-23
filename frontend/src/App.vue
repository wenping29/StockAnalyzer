<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useAuthStore } from './stores/auth'
import { changePassword } from './api/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const showLayout = computed(() => route.path !== '/login')

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
  router.push('/login')
}
</script>

<template>
  <el-container v-if="showLayout" style="height: 100%">
    <el-header class="layout-header">
      <div class="brand" @click="router.push('/')">📈 基金/股票资金流分析系统</div>
      <div class="nav">
        <router-link to="/">自选</router-link>
        <router-link v-if="auth.isAdmin" to="/admin">管理后台</router-link>
      </div>
      <div class="spacer" />
      <el-dropdown>
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
    </el-header>

    <el-main style="padding: 16px 20px">
      <router-view />
    </el-main>

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
}
.brand { font-size: 17px; font-weight: 600; cursor: pointer; white-space: nowrap; }
.nav { display: flex; gap: 18px; }
.nav a { color: #c0c4cc; text-decoration: none; font-size: 15px; }
.nav a.router-link-exact-active { color: #409eff; font-weight: 600; }
.spacer { flex: 1; }
.user-chip { color: #fff; cursor: pointer; display: flex; align-items: center; gap: 6px; }
</style>
