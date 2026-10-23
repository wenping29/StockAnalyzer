<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const auth = useAuthStore()

const tab = ref<'login' | 'register'>('login')
const loading = ref(false)
const form = ref({ userName: '', password: '', email: '' })

const rules = {
  userName: [{ required: true, min: 3, message: '用户名至少 3 个字符', trigger: 'blur' }],
  password: [{ required: true, min: 6, message: '密码至少 6 位', trigger: 'blur' }],
}

async function submit() {
  if (!form.value.userName || !form.value.password) return ElMessage.warning('请填写用户名和密码')
  loading.value = true
  try {
    if (tab.value === 'login') {
      await auth.login(form.value.userName, form.value.password)
      ElMessage.success('登录成功')
    } else {
      await auth.register(form.value.userName, form.value.password, form.value.email || undefined)
      ElMessage.success('注册成功，已自动登录')
    }
    router.push('/')
  } catch {
    /* 错误提示由 http 拦截器统一处理 */
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-wrap">
    <div class="login-box">
      <h2>📈 基金/股票资金流分析系统</h2>
      <p class="tip">资金流入流出 · 多周期趋势 · 收益统计 · 权限分级</p>

      <el-tabs v-model="tab" stretch>
        <el-tab-pane label="登录" name="login" />
        <el-tab-pane label="注册" name="register" />
      </el-tabs>

      <el-form @keyup.enter="submit">
        <el-form-item prop="userName" :rules="rules.userName">
          <el-input v-model="form.userName" placeholder="用户名（默认管理员 admin）" size="large" />
        </el-form-item>
        <el-form-item prop="password" :rules="rules.password">
          <el-input v-model="form.password" type="password" show-password placeholder="密码" size="large" />
        </el-form-item>
        <el-form-item v-if="tab === 'register'">
          <el-input v-model="form.email" placeholder="邮箱（选填）" size="large" />
        </el-form-item>
        <el-button type="primary" size="large" style="width: 100%" :loading="loading" @click="submit">
          {{ tab === 'login' ? '登 录' : '注 册' }}
        </el-button>
      </el-form>

      <p class="default-account">初始管理员：admin / Admin@123（登录后请修改密码）</p>
    </div>
  </div>
</template>

<style scoped>
.login-wrap {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #1f2d3d 0%, #2d3a4b 100%);
}
.login-box {
  width: 400px;
  background: #fff;
  border-radius: 10px;
  padding: 32px 36px 24px;
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.25);
}
.login-box h2 { text-align: center; margin-bottom: 6px; font-size: 20px; }
.tip { text-align: center; color: #909399; font-size: 12px; margin-bottom: 18px; }
.default-account { margin-top: 14px; text-align: center; color: #c0c4cc; font-size: 12px; }
</style>
