<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()
</script>

<template>
  <div class="page-card" style="text-align: center; padding: 60px 20px">
    <h2 style="font-size: 64px; color: #dcdfe6; margin-bottom: 8px">404</h2>
    <p style="color: #909399; margin-bottom: 24px">页面不存在或已被移动</p>
    <el-button type="primary" @click="router.push('/')">返回首页</el-button>
  </div>
</template>
