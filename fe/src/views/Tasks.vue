<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useMotion } from '@vueuse/motion'
import { api } from '../services/api'
import { useSignalR } from '../composables/useSignalR'
import { usePagination } from '../composables/usePagination'
import { useToast } from 'vue-toastification'
import { useAuthStore } from '../stores/auth'
import type { ProjectTask } from '../types'

const toast = useToast()
const { joinProjectGroup, leaveProjectGroup } = useSignalR()
const authStore = useAuthStore()

const tasks = ref<ProjectTask[]>([])
const isLoading = ref(true)
const filter = ref<'all' | 'todo' | 'inprogress' | 'review' | 'completed'>('all')
const sortBy = ref<'status' | 'date' | 'progress'>('status')
const searchQuery = ref('')

const isProjectManager = computed(() => authStore.primaryRole === 'ProjectManager')

const pageTitle = computed(() =>
  isProjectManager.value ? 'Công việc theo dự án phụ trách' : 'Công việc của tôi'
)

const pageSubtitle = computed(() =>
  isProjectManager.value
    ? 'Theo dõi tiến độ công việc trong các dự án bạn quản lý'
    : 'Quản lý và theo dõi tiến độ công việc'
)

const statusColors: Record<number, string> = {
  0: 'bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300',
  1: 'bg-blue-100 text-blue-700 dark:bg-blue-900/50 dark:text-blue-300',
  2: 'bg-yellow-100 text-yellow-700 dark:bg-yellow-900/50 dark:text-yellow-300',
  3: 'bg-green-100 text-green-700 dark:bg-green-900/50 dark:text-green-300',
  4: 'bg-red-100 text-red-700 dark:bg-red-900/50 dark:text-red-300'
}

const statusTexts: Record<number, string> = {
  0: 'Chờ thực hiện',
  1: 'Đang thi công',
  2: 'Chờ nghiệm thu',
  3: 'Đã hoàn thành',
  4: 'Bị chặn'
}

const phaseTexts: Record<number, string> = {
  0: 'Tháo dỡ',
  1: 'Điện nước',
  2: 'Thạch cao',
  3: 'Sơn bả',
  4: 'Đồ gỗ'
}

const phaseColors: Record<number, string> = {
  0: 'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400',
  1: 'bg-blue-100 text-blue-600 dark:bg-blue-900/50 dark:text-blue-400',
  2: 'bg-purple-100 text-purple-600 dark:bg-purple-900/50 dark:text-purple-400',
  3: 'bg-orange-100 text-orange-600 dark:bg-orange-900/50 dark:text-orange-400',
  4: 'bg-amber-100 text-amber-600 dark:bg-amber-900/50 dark:text-amber-400'
}

const filteredTasks = computed(() => {
  let result = [...tasks.value]

  // Filter by search
  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase()
    result = result.filter(t =>
      t.name.toLowerCase().includes(query) ||
      t.projectName.toLowerCase().includes(query) ||
      (t.subcontractorName && t.subcontractorName.toLowerCase().includes(query))
    )
  }

  // Filter by status
  switch (filter.value) {
    case 'todo':
      result = result.filter(t => t.status === 0)
      break
    case 'inprogress':
      result = result.filter(t => t.status === 1)
      break
    case 'review':
      result = result.filter(t => t.status === 2)
      break
    case 'completed':
      result = result.filter(t => t.status === 3)
      break
  }

  // Sort
  switch (sortBy.value) {
    case 'status':
      result.sort((a, b) => a.status - b.status)
      break
    case 'date':
      result.sort((a, b) => {
        const dateA = a.targetDate ? new Date(a.targetDate).getTime() : Infinity
        const dateB = b.targetDate ? new Date(b.targetDate).getTime() : Infinity
        return dateA - dateB
      })
      break
    case 'progress':
      result.sort((a, b) => b.progressPct - a.progressPct)
      break
  }

  return result
})

const {
  pageSize,
  currentPage,
  totalPages,
  paginatedItems: paginatedTasks,
  pageNumbers,
  setPage
} = usePagination(filteredTasks, {
  pageSize: 10,
  resetOn: [searchQuery, filter, sortBy]
})

const taskStats = computed(() => {
  return {
    total: tasks.value.length,
    todo: tasks.value.filter(t => t.status === 0).length,
    inProgress: tasks.value.filter(t => t.status === 1).length,
    review: tasks.value.filter(t => t.status === 2).length,
    completed: tasks.value.filter(t => t.status === 3).length
  }
})

const fetchTasks = async () => {
  try {
    if (isProjectManager.value) {
      const projectsResponse = await api.get('/projects/my')
      const projects = projectsResponse.data?.data || []

      if (projects.length === 0) {
        tasks.value = []
        return
      }

      const taskRequests = projects.map((project: { id: string }) =>
        api.get(`/tasks/project/${project.id}`)
      )

      const settled = await Promise.allSettled(taskRequests)
      const aggregatedTasks = settled
        .filter((result): result is PromiseFulfilledResult<any> => result.status === 'fulfilled')
        .flatMap(result => (result.value.data?.success ? (result.value.data.data || []) : []))

      const uniqueById = new Map<number, ProjectTask>()
      for (const task of aggregatedTasks) {
        uniqueById.set(task.id, task)
      }
      tasks.value = Array.from(uniqueById.values())
      return
    }

    const response = await api.get('/tasks/my')
    if (response.data.success) {
      tasks.value = response.data.data || []
    }
  } catch (error) {
    toast.error('Không thể tải danh sách công việc')
  }
}

const updateTaskStatus = async (taskId: number, newStatus: number) => {
  try {
    await api.put(`/tasks/${taskId}/status`, { newStatus })
    const taskIndex = tasks.value.findIndex(t => t.id === taskId)
    if (taskIndex !== -1) {
      tasks.value[taskIndex].status = newStatus
    }
    toast.success('Cập nhật trạng thái thành công')
  } catch (error) {
    toast.error('Không thể cập nhật trạng thái')
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
    await fetchTasks()

    // Join project groups for real-time updates
    const projectIds = [...new Set(tasks.value.map(t => t.projectId))]
    for (const projectId of projectIds) {
      await joinProjectGroup(projectId)
    }

    window.addEventListener('taskStatusChanged', handleTaskStatusChanged as EventListener)
  } finally {
    isLoading.value = false
  }
})

onUnmounted(async () => {
  window.removeEventListener('taskStatusChanged', handleTaskStatusChanged as EventListener)

  const projectIds = [...new Set(tasks.value.map(t => t.projectId))]
  for (const projectId of projectIds) {
    await leaveProjectGroup(projectId)
  }
})

const formatDate = (date: string) => {
  return new Date(date).toLocaleDateString('vi-VN')
}

const getProgressColor = (progress: number) => {
  if (progress >= 80) return 'bg-green-500'
  if (progress >= 50) return 'bg-yellow-500'
  if (progress >= 20) return 'bg-orange-500'
  return 'bg-red-500'
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div
      v-motion
      :initial="{ opacity: 0, y: -20 }"
      :enter="{ opacity: 1, y: 0 }"
      class="flex flex-col md:flex-row md:items-center md:justify-between gap-4"
    >
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">{{ pageTitle }}</h1>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">{{ pageSubtitle }}</p>
      </div>
      <div class="flex items-center gap-2">
        <span class="flex items-center gap-1 text-sm text-gray-500 dark:text-gray-400">
          <span class="w-2 h-2 rounded-full bg-green-500 animate-pulse"></span>
          Real-time
        </span>
      </div>
    </div>

    <!-- Stats Cards -->
    <div
      v-motion
      :initial="{ opacity: 0, y: 20 }"
      :enter="{ opacity: 1, y: 0, transition: { delay: 100 } }"
      class="grid grid-cols-2 md:grid-cols-5 gap-4"
    >
      <div class="card text-center cursor-pointer hover:ring-2 hover:ring-primary-300 transition-all" @click="filter = 'all'">
        <p class="text-2xl font-bold text-gray-900 dark:text-white">{{ taskStats.total }}</p>
        <p class="text-xs text-gray-500 dark:text-gray-400">Tổng cộng</p>
      </div>
      <div class="card text-center cursor-pointer hover:ring-2 hover:ring-gray-300 transition-all" @click="filter = 'todo'">
        <p class="text-2xl font-bold text-gray-600 dark:text-gray-300">{{ taskStats.todo }}</p>
        <p class="text-xs text-gray-500 dark:text-gray-400">Chờ thực hiện</p>
      </div>
      <div class="card text-center cursor-pointer hover:ring-2 hover:ring-blue-300 transition-all" @click="filter = 'inprogress'">
        <p class="text-2xl font-bold text-blue-600 dark:text-blue-400">{{ taskStats.inProgress }}</p>
        <p class="text-xs text-gray-500 dark:text-gray-400">Đang làm</p>
      </div>
      <div class="card text-center cursor-pointer hover:ring-2 hover:ring-yellow-300 transition-all" @click="filter = 'review'">
        <p class="text-2xl font-bold text-yellow-600 dark:text-yellow-400">{{ taskStats.review }}</p>
        <p class="text-xs text-gray-500 dark:text-gray-400">Chờ nghiệm thu</p>
      </div>
      <div class="card text-center cursor-pointer hover:ring-2 hover:ring-green-300 transition-all" @click="filter = 'completed'">
        <p class="text-2xl font-bold text-green-600 dark:text-green-400">{{ taskStats.completed }}</p>
        <p class="text-xs text-gray-500 dark:text-gray-400">Hoàn thành</p>
      </div>
    </div>

    <!-- Filters -->
    <div
      v-motion
      :initial="{ opacity: 0, y: 20 }"
      :enter="{ opacity: 1, y: 0, transition: { delay: 200 } }"
      class="card"
    >
      <div class="flex flex-col md:flex-row gap-4">
        <!-- Search -->
        <div class="flex-1">
          <div class="relative">
            <svg class="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
            <input
              v-model="searchQuery"
              type="text"
              placeholder="Tìm kiếm công việc..."
              class="input pl-10"
            />
          </div>
        </div>

        <!-- Sort -->
        <div class="flex items-center gap-2">
          <span class="text-sm text-gray-500 dark:text-gray-400">Sắp xếp:</span>
          <select v-model="sortBy" class="input w-auto">
            <option value="status">Trạng thái</option>
            <option value="date">Deadline</option>
            <option value="progress">Tiến độ</option>
          </select>
        </div>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="isLoading" class="space-y-4">
      <div v-for="i in 5" :key="i" class="card">
        <div class="flex items-center gap-4">
          <div class="skeleton w-12 h-12 rounded-lg"></div>
          <div class="flex-1 space-y-2">
            <div class="skeleton h-4 w-1/3"></div>
            <div class="skeleton h-3 w-1/4"></div>
          </div>
          <div class="skeleton h-6 w-20 rounded-full"></div>
        </div>
      </div>
    </div>

    <!-- Task List -->
    <div v-else-if="filteredTasks.length > 0" class="space-y-4">
      <div
        v-for="task in paginatedTasks"
        :key="task.id"
        v-motion
        :initial="{ opacity: 0, x: -20 }"
        :enter="{ opacity: 1, x: 0 }"
        class="card hover:shadow-md transition-shadow"
      >
        <div class="flex flex-col md:flex-row md:items-center gap-4">
          <!-- Task Info -->
          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-3 mb-2">
              <span :class="['px-2 py-1 text-xs font-medium rounded-md', phaseColors[task.phaseType]]">
                {{ phaseTexts[task.phaseType] }}
              </span>
              <span :class="['px-2 py-1 text-xs font-medium rounded-md', statusColors[task.status]]">
                {{ statusTexts[task.status] }}
              </span>
            </div>
            <h3 class="text-lg font-semibold text-gray-900 dark:text-white truncate">{{ task.name }}</h3>
            <p class="text-sm text-gray-500 dark:text-gray-400">{{ task.projectName }}</p>
          </div>

          <!-- Progress -->
          <div class="flex items-center gap-4">
            <div class="text-right">
              <p class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ task.progressPct }}%</p>
              <div class="w-24 h-2 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
                <div
                  :class="['h-full rounded-full transition-all duration-300', getProgressColor(task.progressPct)]"
                  :style="{ width: `${task.progressPct}%` }"
                ></div>
              </div>
            </div>

            <!-- Target Date -->
            <div v-if="task.targetDate" class="text-right">
              <p class="text-xs text-gray-500 dark:text-gray-400">Deadline</p>
              <p class="text-sm font-medium text-gray-900 dark:text-white">{{ formatDate(task.targetDate) }}</p>
            </div>

            <!-- Actions -->
            <div class="flex items-center gap-2">
              <button
                v-if="task.status === 0"
                @click="updateTaskStatus(task.id, 1)"
                class="px-3 py-1.5 text-sm bg-blue-500 hover:bg-blue-600 text-white rounded-lg transition-colors"
              >
                Bắt đầu
              </button>
              <button
                v-if="task.status === 1"
                @click="updateTaskStatus(task.id, 2)"
                class="px-3 py-1.5 text-sm bg-yellow-500 hover:bg-yellow-600 text-white rounded-lg transition-colors"
              >
                Hoàn thành
              </button>
            </div>
          </div>
        </div>
      </div>

      <div v-if="filteredTasks.length > pageSize" class="card">
        <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <p class="text-sm text-gray-500 dark:text-gray-400">
            Hiển thị {{ (currentPage - 1) * pageSize + 1 }}-{{ Math.min(currentPage * pageSize, filteredTasks.length) }} / {{ filteredTasks.length }} công việc
          </p>
          <div class="flex items-center gap-2 flex-wrap">
            <button
              class="btn-secondary px-3 py-1.5 text-sm"
              :disabled="currentPage === 1"
              @click="setPage(currentPage - 1)"
            >
              Trước
            </button>
            <button
              v-for="page in pageNumbers"
              :key="page"
              class="px-3 py-1.5 text-sm rounded-lg border transition-colors"
              :class="page === currentPage
                ? 'bg-primary-600 text-white border-primary-600'
                : 'border-gray-300 text-gray-700 hover:bg-gray-100 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-700'"
              @click="setPage(page)"
            >
              {{ page }}
            </button>
            <button
              class="btn-secondary px-3 py-1.5 text-sm"
              :disabled="currentPage === totalPages"
              @click="setPage(currentPage + 1)"
            >
              Sau
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div v-else class="card text-center py-12">
      <svg class="w-16 h-16 mx-auto mb-4 text-gray-300 dark:text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
      </svg>
      <p class="text-gray-500 dark:text-gray-400">
        {{ searchQuery || filter !== 'all' ? 'Không tìm thấy công việc phù hợp' : (isProjectManager ? 'Chưa có công việc trong các dự án bạn quản lý' : 'Bạn chưa được phân công công việc nào') }}
      </p>
    </div>
  </div>
</template>
