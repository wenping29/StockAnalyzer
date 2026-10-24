<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import { triggerCollect, getCollectStatus, type CollectCategory } from '../api/collect'

// 分类"刷新"按钮：触发采集 → 轮询作业状态 → 完成后通知父组件重载数据。
// 采集在 Hangfire 后台执行，第三方调用不进入请求路径，接口保持可用。
const props = withDefaults(defineProps<{
  category: CollectCategory
  label?: string
  pollInterval?: number
  timeout?: number
}>(), {
  label: '刷新',
  pollInterval: 2000,
  timeout: 150000,
})

const emit = defineEmits<{
  (e: 'done'): void
  (e: 'error', message: string): void
}>()

const running = ref(false)
const ok = ref(false)

async function onClick() {
  if (running.value) return
  running.value = true
  ok.value = false
  const started = Date.now()
  try {
    const job = await triggerCollect(props.category)
    // 轮询直到成功 / 失败 / 超时
    while (Date.now() - started < props.timeout) {
      await new Promise((r) => setTimeout(r, props.pollInterval))
      let state = ''
      try {
        state = (await getCollectStatus(job.jobId)).state
      } catch {
        continue // 状态查询偶发失败，继续轮询
      }
      if (state === 'Succeeded') {
        ok.value = true
        ElMessage.success('数据已更新')
        emit('done')
        setTimeout(() => (ok.value = false), 2000)
        return
      }
      if (state === 'Failed') {
        ElMessage.error('采集失败，请稍后重试（详见 /hangfire 面板）')
        emit('error', '采集作业失败')
        return
      }
    }
    ElMessage.warning('采集超时，任务仍在后台执行，请稍后刷新查看')
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message ?? '触发采集失败')
    emit('error', '触发采集失败')
  } finally {
    running.value = false
  }
}
</script>

<template>
  <el-button size="small" :loading="running" :type="ok ? 'success' : 'default'" @click="onClick">
    {{ running ? '采集中…' : ok ? '已更新' : label }}
  </el-button>
</template>
