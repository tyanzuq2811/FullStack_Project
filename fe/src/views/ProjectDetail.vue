<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useMotion } from '@vueuse/motion'
import { api } from '../services/api'
import { useSignalR } from '../composables/useSignalR'
import { useAuthStore } from '../stores/auth'
import { useToast } from 'vue-toastification'
import type { Project, ProjectTask } from '../types'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const toast = useToast()
const { joinProjectGroup, leaveProjectGroup } = useSignalR()

const project = ref<Project | null>(null)
const tasks = ref<ProjectTask[]>([])
const isLoading = ref(true)
const activeTab = ref<'overview' | 'tasks' | 'budget' | 'timeline'>('overview')
const progressFileInput = ref<HTMLInputElement | null>(null)
const invoiceFileInput = ref<HTMLInputElement | null>(null)
const selectedTaskIdForAi = ref<number | null>(null)
const isAnalyzingProgress = ref(false)
const isAnalyzingInvoice = ref(false)

const projectId = computed(() => route.params.id as string)
const canUseAiProgress = computed(() => ['ProjectManager', 'Subcontractor'].includes(authStore.primaryRole))
const canUseAiInvoice = computed(() => ['ProjectManager', 'Accountant'].includes(authStore.primaryRole))
const currentMemberId = computed(() => authStore.user?.id || '')

const canAnalyzeProgressTask = (task: ProjectTask) => {
  if (!canUseAiProgress.value) return false
  if (authStore.primaryRole === 'ProjectManager') return true
  return authStore.primaryRole === 'Subcontractor' && !!task.subcontractorId && task.subcontractorId === currentMemberId.value
}

const statusColors: Record<number, string> = {
  0: 'badge-info',
  1: 'badge-warning',
  2: 'badge-primary',
  3: 'badge-success'
}

const statusTexts: Record<number, string> = {
  0: 'Lập kế hoạch',
  1: 'Đang thi công',
  2: 'Bàn giao',
  3: 'Hoàn thành'
}

const taskStatusColors: Record<number, string> = {
  0: 'bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300',
  1: 'bg-blue-100 text-blue-700 dark:bg-blue-900/50 dark:text-blue-300',
  2: 'bg-yellow-100 text-yellow-700 dark:bg-yellow-900/50 dark:text-yellow-300',
  3: 'bg-green-100 text-green-700 dark:bg-green-900/50 dark:text-green-300',
  4: 'bg-red-100 text-red-700 dark:bg-red-900/50 dark:text-red-300'
}

const taskStatusTexts: Record<number, string> = {
  0: 'Chờ',
  1: 'Đang làm',
  2: 'Chờ duyệt',
  3: 'Hoàn thành',
  4: 'Bị chặn'
}

const phaseTexts: Record<number, string> = {
  0: 'Tháo dỡ',
  1: 'Điện nước',
  2: 'Thạch cao',
  3: 'Sơn bả',
  4: 'Đồ gỗ'
}

const tasksByPhase = computed(() => {
  const grouped: Record<number, ProjectTask[]> = {}
  tasks.value.forEach(task => {
    if (!grouped[task.phaseType]) {
      grouped[task.phaseType] = []
    }
    grouped[task.phaseType].push(task)
  })
  return grouped
})

const budgetUsage = computed(() => {
  if (!project.value) return 0
  return project.value.spentBudget / project.value.totalBudget * 100
})

const fetchProject = async () => {
  try {
    const response = await api.get(`/projects/${projectId.value}`)
    if (response.data.success) {
      project.value = response.data.data
    }
  } catch (error: any) {
    if (error?.response?.status === 403) {
      toast.error('Bạn không có quyền truy cập dự án này')
    } else {
      toast.error('Không thể tải dự án')
    }
    router.push('/projects')
  }
}

const fetchTasks = async () => {
  try {
    const response = await api.get(`/tasks/project/${projectId.value}`)
    if (response.data.success) {
      tasks.value = response.data.data || []
    }
  } catch (error: any) {
    if (error?.response?.status === 403) {
      toast.error('Bạn không có quyền xem công việc của dự án này')
    } else {
      console.error('Error fetching tasks:', error)
    }
  }
}

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

const openAnalyzeProgress = (taskId: number) => {
  const task = tasks.value.find(t => t.id === taskId)
  if (!task || !canAnalyzeProgressTask(task)) {
    toast.error('Bạn chỉ được phân tích AI cho công việc được giao cho mình')
    return
  }

  selectedTaskIdForAi.value = taskId
  progressFileInput.value?.click()
}

const openAnalyzeInvoice = (taskId: number) => {
  if (!canUseAiInvoice.value) {
    toast.error('Vai trò hiện tại không được OCR hóa đơn AI')
    return
  }

  selectedTaskIdForAi.value = taskId
  invoiceFileInput.value?.click()
}

const handleProgressFileSelected = async (event: Event) => {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file || !selectedTaskIdForAi.value) return

  isAnalyzingProgress.value = true
  try {
    const imageBase64 = await readFileAsBase64(file)
    const taskId = selectedTaskIdForAi.value
    const response = await api.post(`/ai/analyze-progress/${taskId}`, {
      taskId,
      imageBase64
    })

    if (response.data?.success) {
      const updatedTask = tasks.value.find(t => t.id === taskId)
      if (updatedTask) {
        updatedTask.progressPct = response.data.data.progressPct
      }

      const anomalies = response.data.data.anomaliesDetected || []
      if (anomalies.length > 0) {
        toast.warning(`AI cảnh báo: ${anomalies.join(', ')}`)
      } else {
        toast.success(`AI cập nhật tiến độ: ${response.data.data.progressPct}%`)
      }
    }
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Không thể phân tích tiến độ AI')
  } finally {
    isAnalyzingProgress.value = false
    selectedTaskIdForAi.value = null
    input.value = ''
  }
}

const handleInvoiceFileSelected = async (event: Event) => {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file || !selectedTaskIdForAi.value) return

  isAnalyzingInvoice.value = true
  try {
    const imageBase64 = await readFileAsBase64(file)
    const response = await api.post('/ai/analyze-invoice', {
      projectId: projectId.value,
      taskId: selectedTaskIdForAi.value,
      imageBase64
    })

    if (response.data?.success) {
      const result = response.data.data
      const warningText = result.exceedsBudget ? ' - Vuot ngan sach' : ''
      toast.success(`OCR: ${result.vendor} - ${new Intl.NumberFormat('vi-VN').format(result.totalAmount)} VND${warningText}`)
    }
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Không thể OCR hóa đơn AI')
  } finally {
    isAnalyzingInvoice.value = false
    selectedTaskIdForAi.value = null
    input.value = ''
  }
}

const handleTaskStatusChanged = (event: CustomEvent) => {
  const { taskId, newStatus, progressPct } = event.detail
  const taskIndex = tasks.value.findIndex(t => t.id === taskId)
  if (taskIndex !== -1) {
    tasks.value[taskIndex].status = newStatus
    tasks.value[taskIndex].progressPct = progressPct
  }
}

onMounted(async () => {
  try {
    await Promise.all([fetchProject(), fetchTasks()])
    await joinProjectGroup(projectId.value)
    window.addEventListener('taskStatusChanged', handleTaskStatusChanged as EventListener)
  } finally {
    isLoading.value = false
  }
})

onUnmounted(async () => {
  await leaveProjectGroup(projectId.value)
  window.removeEventListener('taskStatusChanged', handleTaskStatusChanged as EventListener)
})

const formatCurrency = (value: number) => {
  return new Intl.NumberFormat('vi-VN').format(value) + ' VNĐ'
}

const formatDate = (date: string) => {
  return new Date(date).toLocaleDateString('vi-VN')
}
</script>

<template>
  <div class="space-y-6">
    <!-- Loading State -->
    <div v-if="isLoading" class="space-y-6">
      <div class="skeleton h-10 w-1/3 rounded"></div>
      <div class="card">
        <div class="skeleton h-40 rounded"></div>
      </div>
    </div>

    <!-- Content -->
    <template v-else-if="project">
      <!-- Header -->
      <div
        v-motion
        :initial="{ opacity: 0, y: -20 }"
        :enter="{ opacity: 1, y: 0 }"
        class="flex flex-col md:flex-row md:items-center md:justify-between gap-4"
      >
        <div class="flex items-center gap-4">
          <button
            @click="router.push('/projects')"
            class="p-2 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
          >
            <svg class="w-5 h-5 text-gray-600 dark:text-gray-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
            </svg>
          </button>
          <div>
            <h1 class="text-2xl font-bold text-gray-900 dark:text-white">{{ project.name }}</h1>
            <p class="text-sm text-gray-500 dark:text-gray-400">Quản lý: {{ project.managerName }}</p>
          </div>
        </div>
        <span :class="['badge', statusColors[project.status]]">
          {{ statusTexts[project.status] }}
        </span>
      </div>

      <!-- Tabs -->
      <div class="border-b border-gray-200 dark:border-gray-700">
        <nav class="flex gap-4">
          <button
            v-for="tab in [
              { id: 'overview', label: 'Tổng quan' },
              { id: 'tasks', label: 'Công việc' },
              { id: 'budget', label: 'Ngân sách' },
              { id: 'timeline', label: 'Timeline' }
            ]"
            :key="tab.id"
            @click="activeTab = tab.id as any"
            :class="[
              'pb-3 px-1 text-sm font-medium border-b-2 transition-colors',
              activeTab === tab.id
                ? 'border-primary-500 text-primary-600 dark:text-primary-400'
                : 'border-transparent text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200'
            ]"
          >
            {{ tab.label }}
          </button>
        </nav>
      </div>

      <!-- Overview Tab -->
      <div v-if="activeTab === 'overview'" class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Progress Card -->
        <div
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :enter="{ opacity: 1, y: 0, transition: { delay: 100 } }"
          class="card"
        >
          <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Tiến độ</h3>
          <div class="relative pt-1">
            <div class="flex items-center justify-between mb-2">
              <span class="text-sm font-medium text-gray-700 dark:text-gray-300">Hoàn thành</span>
              <span class="text-sm font-semibold text-primary-600 dark:text-primary-400">{{ Math.round(project.progressPercentage) }}%</span>
            </div>
            <div class="w-full h-3 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
              <div
                class="h-full bg-gradient-to-r from-primary-400 to-primary-600 rounded-full transition-all duration-500"
                :style="{ width: `${project.progressPercentage}%` }"
              ></div>
            </div>
          </div>
          <div class="mt-4 grid grid-cols-2 gap-4 text-center">
            <div>
              <p class="text-2xl font-bold text-gray-900 dark:text-white">{{ project.completedTaskCount }}</p>
              <p class="text-xs text-gray-500 dark:text-gray-400">Hoàn thành</p>
            </div>
            <div>
              <p class="text-2xl font-bold text-gray-900 dark:text-white">{{ project.taskCount }}</p>
              <p class="text-xs text-gray-500 dark:text-gray-400">Tổng công việc</p>
            </div>
          </div>
        </div>

        <!-- Info Card -->
        <div
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :enter="{ opacity: 1, y: 0, transition: { delay: 200 } }"
          class="card"
        >
          <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Thông tin</h3>
          <dl class="space-y-3">
            <div class="flex justify-between">
              <dt class="text-sm text-gray-500 dark:text-gray-400">Khách hàng</dt>
              <dd class="text-sm font-medium text-gray-900 dark:text-white">{{ project.clientName }}</dd>
            </div>
            <div class="flex justify-between">
              <dt class="text-sm text-gray-500 dark:text-gray-400">Ngày bắt đầu</dt>
              <dd class="text-sm font-medium text-gray-900 dark:text-white">{{ formatDate(project.startDate) }}</dd>
            </div>
            <div class="flex justify-between">
              <dt class="text-sm text-gray-500 dark:text-gray-400">Ngày dự kiến</dt>
              <dd class="text-sm font-medium text-gray-900 dark:text-white">{{ project.targetDate ? formatDate(project.targetDate) : 'Chưa xác định' }}</dd>
            </div>
          </dl>
        </div>

        <!-- Budget Card -->
        <div
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :enter="{ opacity: 1, y: 0, transition: { delay: 300 } }"
          class="card"
        >
          <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Ngân sách</h3>
          <div class="space-y-3">
            <div class="flex justify-between">
              <span class="text-sm text-gray-500 dark:text-gray-400">Tổng ngân sách</span>
              <span class="text-sm font-semibold text-gray-900 dark:text-white">{{ formatCurrency(project.totalBudget) }}</span>
            </div>
            <div class="flex justify-between">
              <span class="text-sm text-gray-500 dark:text-gray-400">Đã chi</span>
              <span class="text-sm font-semibold text-orange-600 dark:text-orange-400">{{ formatCurrency(project.spentBudget) }}</span>
            </div>
            <div class="w-full h-2 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
              <div
                :class="[
                  'h-full rounded-full transition-all duration-500',
                  budgetUsage > 90 ? 'bg-red-500' : budgetUsage > 70 ? 'bg-yellow-500' : 'bg-green-500'
                ]"
                :style="{ width: `${Math.min(budgetUsage, 100)}%` }"
              ></div>
            </div>
            <div class="flex justify-between">
              <span class="text-sm text-gray-500 dark:text-gray-400">Còn lại</span>
              <span class="text-sm font-semibold text-green-600 dark:text-green-400">{{ formatCurrency(project.totalBudget - project.spentBudget) }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Tasks Tab -->
      <div v-else-if="activeTab === 'tasks'" class="space-y-6">
        <div v-for="(phaseTasks, phaseType) in tasksByPhase" :key="phaseType" class="card">
          <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">
            {{ phaseTexts[Number(phaseType)] || 'Khác' }}
            <span class="text-sm font-normal text-gray-500 dark:text-gray-400 ml-2">({{ phaseTasks.length }})</span>
          </h3>
          <div class="space-y-3">
            <div
              v-for="task in phaseTasks"
              :key="task.id"
              v-motion
              :initial="{ opacity: 0, x: -20 }"
              :enter="{ opacity: 1, x: 0 }"
              class="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-700/50 rounded-lg"
            >
              <div class="flex-1">
                <p class="font-medium text-gray-900 dark:text-white">{{ task.name }}</p>
                <p class="text-sm text-gray-500 dark:text-gray-400">{{ task.subcontractorName || 'Chưa phân công' }}</p>
                <div class="mt-2 flex flex-wrap gap-2">
                  <button
                    v-if="canAnalyzeProgressTask(task)"
                    class="btn-secondary text-xs"
                    :disabled="isAnalyzingProgress"
                    @click="openAnalyzeProgress(task.id)"
                  >
                    {{ isAnalyzingProgress ? 'Đang AI tiến độ...' : 'AI Tiến độ' }}
                  </button>
                  <button
                    v-if="canUseAiInvoice"
                    class="btn-secondary text-xs"
                    :disabled="isAnalyzingInvoice"
                    @click="openAnalyzeInvoice(task.id)"
                  >
                    {{ isAnalyzingInvoice ? 'Đang OCR...' : 'AI OCR Hóa đơn' }}
                  </button>
                </div>
              </div>
              <div class="flex items-center gap-4">
                <div class="text-right">
                  <p class="text-sm font-medium text-gray-900 dark:text-white">{{ task.progressPct }}%</p>
                  <div class="w-20 h-1.5 bg-gray-200 dark:bg-gray-600 rounded-full overflow-hidden">
                    <div
                      class="h-full bg-primary-500 rounded-full"
                      :style="{ width: `${task.progressPct}%` }"
                    ></div>
                  </div>
                </div>
                <span :class="['px-2 py-1 text-xs font-medium rounded-md', taskStatusColors[task.status]]">
                  {{ taskStatusTexts[task.status] }}
                </span>
              </div>
            </div>
          </div>
        </div>

        <div v-if="tasks.length === 0" class="text-center py-12 text-gray-500 dark:text-gray-400">
          Chưa có công việc nào
        </div>
      </div>

      <!-- Budget Tab -->
      <div v-else-if="activeTab === 'budget'" class="card">
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-6">Chi tiết Ngân sách</h3>
        <div class="space-y-4">
          <div v-for="task in tasks" :key="task.id" class="flex items-center justify-between p-3 border-b border-gray-200 dark:border-gray-700 last:border-0">
            <div>
              <p class="font-medium text-gray-900 dark:text-white">{{ task.name }}</p>
              <p class="text-sm text-gray-500 dark:text-gray-400">{{ phaseTexts[task.phaseType] }}</p>
            </div>
            <p class="font-semibold text-gray-900 dark:text-white">{{ formatCurrency(task.estimatedCost) }}</p>
          </div>
        </div>
        <div class="mt-6 pt-6 border-t border-gray-200 dark:border-gray-700 flex justify-between">
          <span class="text-lg font-semibold text-gray-900 dark:text-white">Tổng cộng</span>
          <span class="text-lg font-bold text-primary-600 dark:text-primary-400">{{ formatCurrency(project.totalBudget) }}</span>
        </div>
      </div>

      <!-- Timeline Tab -->
      <div v-else-if="activeTab === 'timeline'" class="card">
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-6">Timeline Dự án</h3>
        <div class="relative">
          <div class="absolute left-4 top-0 bottom-0 w-0.5 bg-gray-200 dark:bg-gray-700"></div>
          <div class="space-y-6">
            <div
              v-for="task in tasks"
              :key="task.id"
              v-motion
              :initial="{ opacity: 0, x: -20 }"
              :enter="{ opacity: 1, x: 0 }"
              class="relative pl-10"
            >
              <div
                :class="[
                  'absolute left-2 top-1 w-4 h-4 rounded-full border-2 border-white dark:border-gray-800',
                  task.status === 3 ? 'bg-green-500' : task.status === 1 ? 'bg-blue-500' : 'bg-gray-300 dark:bg-gray-600'
                ]"
              ></div>
              <div class="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-4">
                <div class="flex items-center justify-between">
                  <p class="font-medium text-gray-900 dark:text-white">{{ task.name }}</p>
                  <span :class="['px-2 py-1 text-xs font-medium rounded-md', taskStatusColors[task.status]]">
                    {{ taskStatusTexts[task.status] }}
                  </span>
                </div>
                <div class="mt-2 flex items-center gap-4 text-sm text-gray-500 dark:text-gray-400">
                  <span v-if="task.startTime">Bắt đầu: {{ formatDate(task.startTime) }}</span>
                  <span v-if="task.targetDate">Deadline: {{ formatDate(task.targetDate) }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <input
        ref="progressFileInput"
        type="file"
        accept="image/*"
        class="hidden"
        @change="handleProgressFileSelected"
      />
      <input
        ref="invoiceFileInput"
        type="file"
        accept="image/*"
        class="hidden"
        @change="handleInvoiceFileSelected"
      />
    </template>
  </div>
</template>
