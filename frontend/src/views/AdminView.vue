<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { AdminUser } from '../types'
import { fetchUsers, updateUserRole, updateUserEnabled, triggerCollect } from '../api/quote'

const users = ref<AdminUser[]>([])
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    users.value = await fetchUsers()
  } finally {
    loading.value = false
  }
}

async function changeRole(row: AdminUser, role: string) {
  await updateUserRole(row.id, role)
  ElMessage.success(`已将 ${row.userName} 设为 ${role}`)
  load()
}

async function toggleEnabled(row: AdminUser, enabled: boolean) {
  await updateUserEnabled(row.id, enabled)
  ElMessage.success(`${enabled ? '已启用' : '已禁用'} ${row.userName}`)
  load()
}

async function collect(what: 'initial' | 'daily' | 'minutes') {
  await triggerCollect(what)
  ElMessage.success('已提交后台采集任务，稍后刷新数据即可')
}

const roleTag: Record<string, 'danger' | 'warning' | 'info'> = { Admin: 'danger', VIP: 'warning', User: 'info' }

onMounted(load)
</script>

<template>
  <div class="page-card">
    <div class="toolbar">
      <h3>用户与权限管理</h3>
      <div style="flex: 1" />
      <el-button size="small" @click="collect('daily')">立即采集日线</el-button>
      <el-button size="small" @click="collect('minutes')">采集分钟线</el-button>
      <el-button size="small" @click="collect('initial')">初始化示例数据</el-button>
      <el-button size="small" :loading="loading" @click="load">刷新</el-button>
    </div>

    <el-table :data="users" v-loading="loading">
      <el-table-column prop="userName" label="用户名" width="140" />
      <el-table-column prop="email" label="邮箱" min-width="180">
        <template #default="{ row }">{{ row.email ?? '—' }}</template>
      </el-table-column>
      <el-table-column label="角色" width="160">
        <template #default="{ row }">
          <el-select :model-value="row.role" size="small" @change="(r: string) => changeRole(row, r)">
            <el-option value="Admin" label="Admin（管理员）" />
            <el-option value="VIP" label="VIP（小时线+资金流）" />
            <el-option value="User" label="User（日线）" />
          </el-select>
        </template>
      </el-table-column>
      <el-table-column label="当前角色" width="100">
        <template #default="{ row }">
          <el-tag size="small" :type="roleTag[row.role] ?? 'info'">{{ row.role }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="启用" width="90">
        <template #default="{ row }">
          <el-switch :model-value="row.enabled" @change="(v: boolean) => toggleEnabled(row, v)" />
        </template>
      </el-table-column>
      <el-table-column label="注册时间" width="180">
        <template #default="{ row }">{{ new Date(row.createdAt).toLocaleString() }}</template>
      </el-table-column>
    </el-table>
  </div>
</template>

<style scoped>
.toolbar { display: flex; align-items: center; gap: 8px; margin-bottom: 14px; }
</style>
