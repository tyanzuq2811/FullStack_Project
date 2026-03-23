<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api } from '../../services/api'
import { useAuthStore } from '../../stores/auth'
import { useWalletStore } from '../../stores/wallet'
import type { ProjectTask } from '../../types'

const authStore = useAuthStore()
const walletStore = useWalletStore()

const tasks = ref<ProjectTask[]>([])
const loading = ref(true)
const isUploading = ref(false)
const selectedTaskId = ref<number | null>(null)
const fileInputRef = ref<HTMLInputElement | null>(null)

const inProgressCount = computed(() => tasks.value.filter(task => task.status === 1).length)
const deadlineRisk = computed(() => {
  const now = new Date()
  const todayKey = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
  let overdue = 0
  let dueToday = 0

  for (const task of tasks.value) {
    if (!task.targetDate || task.status === 3) continue
    const d = new Date(task.targetDate)
    const taskKey = new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()
    if (taskKey < todayKey) overdue += 1
    if (taskKey === todayKey) dueToday += 1
  }

  const activeTotal = tasks.value.filter(task => task.status !== 3).length
  const onTrack = Math.max(activeTotal - overdue - dueToday, 0)

  return { overdue, dueToday, onTrack }
})

const todayTasks = computed(() => {
  const today = new Date()
  const todayKey = new Date(today.getFullYear(), today.getMonth(), today.getDate()).getTime()

  return tasks.value
    .filter(task => {
      if (!task.targetDate || task.status === 3) return false
      const d = new Date(task.targetDate)
      const taskKey = new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()
      return taskKey <= todayKey
    })
    .sort((a, b) => (a.targetDate || '').localeCompare(b.targetDate || ''))
    .slice(0, 8)
})

const readFileAsBase64 = (file: File): Promise<string> => {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => {
      const result = typeof reader.result === 'string' ? reader.result : ''
      const base64 = result.includes(',') ? result.split(',')[1] : result
      resolve(base64)
    }
    reader.onerror = reject
    reader.readAsDataURL(file)
  })
}

const openQuickUpload = (taskId?: number) => {
  selectedTaskId.value = taskId || todayTasks.value[0]?.id || tasks.value[0]?.id || null
  if (!selectedTaskId.value) return
  fileInputRef.value?.click()
}

const onFileSelected = async (event: Event) => {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file || !selectedTaskId.value) return

  isUploading.value = true
  try {
    const imageBase64 = await readFileAsBase64(file)
    const taskId = selectedTaskId.value
    const response = await api.post(`/ai/analyze-progress/${taskId}`, {
      taskId,
      imageBase64
    })

    if (response.data?.success) {
      const task = tasks.value.find(item => item.id === taskId)
      if (task) {
        task.progressPct = response.data.data.progressPct
      }
    }
  } finally {
    isUploading.value = false
    selectedTaskId.value = null
    input.value = ''
  }
}

onMounted(async () => {
  try {
    const [tasksRes] = await Promise.all([
      api.get('/tasks/my'),
      walletStore.fetchSummary()
    ])
    if (tasksRes.data?.success) tasks.value = tasksRes.data.data || []
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-6">
    <input
      ref="fileInputRef"
      type="file"
      accept="image/*"
      class="hidden"
      @change="onFileSelected"
    />

    <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
      <div class="card">
        <p class="text-sm text-gray-500 dark:text-gray-400">Điểm năng lực ELO</p>
        <p class="mt-1 text-3xl font-bold text-gray-900 dark:text-white">{{ authStore.user?.rankELO || 0 }}</p>
      </div>
      <div class="card">
        <p class="text-sm text-gray-500 dark:text-gray-400">Số dư ví cá nhân</p>
        <p class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">{{ walletStore.summary?.balance?.toLocaleString('vi-VN') || 0 }} VND</p>
      </div>
      <div class="card">
        <p class="text-sm text-gray-500 dark:text-gray-400">Đang thi công</p>
        <p class="mt-1 text-3xl font-bold text-gray-900 dark:text-white">{{ inProgressCount }}</p>
      </div>
    </div>

    <div class="card border-primary-200 dark:border-primary-800 bg-primary-50/50 dark:bg-primary-950/20">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Upload nhanh ảnh hiện trường</h2>
          <p class="text-sm text-gray-600 dark:text-gray-400">Gửi ảnh cho AI đánh giá tiến độ công việc hiện tại</p>
        </div>
        <button class="btn-primary" :disabled="isUploading || tasks.length === 0" @click="openQuickUpload()">
          {{ isUploading ? 'Đang upload...' : 'Upload nhanh' }}
        </button>
      </div>

      <div class="mt-4 grid grid-cols-1 sm:grid-cols-3 gap-3">
        <div class="rounded-lg border border-red-300 dark:border-red-900 bg-red-50/80 dark:bg-red-950/20 p-3">
          <p class="text-xs uppercase tracking-wide text-red-700 dark:text-red-300">Rủi ro cao</p>
          <p class="mt-1 text-sm text-red-700 dark:text-red-300">Công việc quá hạn</p>
          <p class="text-2xl font-bold text-red-700 dark:text-red-300">{{ deadlineRisk.overdue }}</p>
        </div>
        <div class="rounded-lg border border-amber-300 dark:border-amber-900 bg-amber-50/80 dark:bg-amber-950/20 p-3">
          <p class="text-xs uppercase tracking-wide text-amber-700 dark:text-amber-300">Cảnh báo</p>
          <p class="mt-1 text-sm text-amber-700 dark:text-amber-300">Công việc đến hạn hôm nay</p>
          <p class="text-2xl font-bold text-amber-700 dark:text-amber-300">{{ deadlineRisk.dueToday }}</p>
        </div>
        <div class="rounded-lg border border-emerald-300 dark:border-emerald-900 bg-emerald-50/80 dark:bg-emerald-950/20 p-3">
          <p class="text-xs uppercase tracking-wide text-emerald-700 dark:text-emerald-300">An toàn</p>
          <p class="mt-1 text-sm text-emerald-700 dark:text-emerald-300">Công việc đúng tiến độ</p>
          <p class="text-2xl font-bold text-emerald-700 dark:text-emerald-300">{{ deadlineRisk.onTrack }}</p>
        </div>
      </div>
    </div>

    <div class="card">
      <h2 class="mb-4 text-lg font-semibold text-gray-900 dark:text-white">Công việc cần làm hôm nay</h2>
      <div v-if="loading" class="text-sm text-gray-500 dark:text-gray-400">Đang tải dữ liệu...</div>
      <div v-else-if="todayTasks.length === 0" class="text-sm text-gray-500 dark:text-gray-400">Hôm nay không có công việc gấp.</div>
      <div v-else class="space-y-2">
        <div
          v-for="task in todayTasks"
          :key="task.id"
          class="rounded-lg border border-gray-200 dark:border-gray-700 px-3 py-2"
        >
          <div class="flex items-center justify-between gap-2">
            <div>
              <p class="text-sm font-medium text-gray-900 dark:text-white">{{ task.name }}</p>
              <p class="text-xs text-gray-500 dark:text-gray-400">{{ task.projectName }}</p>
            </div>
            <div class="flex items-center gap-2">
              <span class="text-xs text-gray-500 dark:text-gray-400">{{ task.progressPct }}%</span>
              <button class="btn-secondary text-xs" @click="openQuickUpload(task.id)">Upload</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
