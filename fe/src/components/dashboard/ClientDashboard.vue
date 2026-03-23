<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { api } from '../../services/api'
import { useWalletStore } from '../../stores/wallet'
import type { Project, ProjectTask } from '../../types'

const walletStore = useWalletStore()
const projects = ref<Project[]>([])
const timelineTasks = ref<ProjectTask[]>([])
const kanbanTasks = ref<ProjectTask[]>([])
const selectedKanbanProjectId = ref('')
const isKanbanLoading = ref(false)
const loading = ref(true)
const activeProjectId = ref('')
const activeSlideIndex = ref(0)

interface TimelineSlide {
  id: number
  projectId: string
  projectName: string
  title: string
  progress: number
  imageUrl: string
  time: string
  statusText: string
  approvedBy: string
}

const overallProgress = computed(() => {
  if (projects.value.length === 0) return 0
  const total = projects.value.reduce((sum, project) => sum + project.progressPercentage, 0)
  return Math.round(total / projects.value.length)
})

const ringStroke = computed(() => {
  const circumference = 2 * Math.PI * 54
  return circumference - (overallProgress.value / 100) * circumference
})

const formattedTimeline = computed<TimelineSlide[]>(() => {
  return timelineTasks.value
    .filter(task => !!task.imageUrl)
    .map(task => ({
      id: task.id,
      projectId: task.projectId,
      title: task.name,
      projectName: task.projectName,
      progress: task.progressPct,
      imageUrl: task.imageUrl || '',
      time: task.approvedAt || task.endTime || task.targetDate || task.startTime || new Date().toISOString(),
      statusText: task.status === 3 ? 'PM phê duyệt' : 'AI xác nhận',
      approvedBy: task.approvedBy || 'N/A'
    }))
    .sort((a, b) => new Date(b.time).getTime() - new Date(a.time).getTime())
    .slice(0, 24)
})

const timelineByProject = computed(() => {
  const grouped = new Map<string, { projectName: string; items: TimelineSlide[] }>()

  for (const item of formattedTimeline.value) {
    if (!grouped.has(item.projectId)) {
      grouped.set(item.projectId, { projectName: item.projectName, items: [] })
    }
    grouped.get(item.projectId)!.items.push(item)
  }

  return Array.from(grouped.entries())
    .map(([projectId, value]) => ({
      projectId,
      projectName: value.projectName,
      items: value.items.sort((a, b) => new Date(b.time).getTime() - new Date(a.time).getTime())
    }))
    .sort((a, b) => {
      const aTime = new Date(a.items[0]?.time || 0).getTime()
      const bTime = new Date(b.items[0]?.time || 0).getTime()
      return bTime - aTime
    })
})

const activeTimeline = computed(() => {
  return timelineByProject.value.find(group => group.projectId === activeProjectId.value) || null
})

const activeSlides = computed(() => activeTimeline.value?.items || [])
const activeSlidesAvgProgress = computed(() => {
  if (activeSlides.value.length === 0) return 0
  const total = activeSlides.value.reduce((sum, slide) => sum + slide.progress, 0)
  return Math.round(total / activeSlides.value.length)
})

const activeSlide = computed(() => {
  if (activeSlides.value.length === 0) return null
  const boundedIndex = Math.max(0, Math.min(activeSlideIndex.value, activeSlides.value.length - 1))
  return activeSlides.value[boundedIndex]
})

const previousSlide = () => {
  if (activeSlides.value.length === 0) return
  activeSlideIndex.value = (activeSlideIndex.value - 1 + activeSlides.value.length) % activeSlides.value.length
}

const nextSlide = () => {
  if (activeSlides.value.length === 0) return
  activeSlideIndex.value = (activeSlideIndex.value + 1) % activeSlides.value.length
}

watch(timelineByProject, (groups) => {
  if (groups.length === 0) {
    activeProjectId.value = ''
    activeSlideIndex.value = 0
    return
  }

  const hasActiveProject = groups.some(group => group.projectId === activeProjectId.value)
  if (!hasActiveProject) {
    activeProjectId.value = groups[0].projectId
    activeSlideIndex.value = 0
  }
}, { immediate: true })

watch(activeProjectId, () => {
  activeSlideIndex.value = 0
})

const formatTime = (value: string) => {
  return new Date(value).toLocaleString('vi-VN')
}

const kanbanColumns = [
  { id: 0, title: 'Chờ thực hiện', color: 'bg-gray-100 dark:bg-gray-700' },
  { id: 1, title: 'Đang thi công', color: 'bg-blue-50 dark:bg-blue-900/20' },
  { id: 2, title: 'Chờ nghiệm thu', color: 'bg-yellow-50 dark:bg-yellow-900/20' },
  { id: 3, title: 'Đã hoàn thành', color: 'bg-green-50 dark:bg-green-900/20' }
]

const kanbanByStatus = computed(() => {
  const grouped: Record<number, ProjectTask[]> = { 0: [], 1: [], 2: [], 3: [] }
  for (const task of kanbanTasks.value) {
    if (task.status >= 0 && task.status <= 3) {
      grouped[task.status].push(task)
    }
  }
  return grouped
})

const loadClientKanban = async () => {
  if (!selectedKanbanProjectId.value) {
    kanbanTasks.value = []
    return
  }

  isKanbanLoading.value = true
  try {
    const response = await api.get(`/tasks/project/${selectedKanbanProjectId.value}`)
    if (response.data?.success) {
      kanbanTasks.value = response.data.data || []
    }
  } finally {
    isKanbanLoading.value = false
  }
}

onMounted(async () => {
  try {
    const [projectsRes] = await Promise.all([
      api.get('/projects/my'),
      walletStore.fetchSummary()
    ])

    if (projectsRes.data?.success) {
      projects.value = projectsRes.data.data || []
      selectedKanbanProjectId.value = projects.value[0]?.id || ''
      const taskResponses = await Promise.all(
        projects.value.slice(0, 3).map(project => api.get(`/tasks/project/${project.id}`))
      )
      timelineTasks.value = taskResponses
        .filter(response => response.data?.success)
        .flatMap(response => response.data.data || [])
        .filter((task: ProjectTask) => task.status >= 1)

      await loadClientKanban()
    }
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-6">
    <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
      <div class="card md:col-span-2">
        <p class="text-sm text-gray-500 dark:text-gray-400">Tiến độ tổng thể dự án của bạn</p>
        <div class="mt-3 flex items-center gap-6">
          <svg width="130" height="130" viewBox="0 0 130 130" class="shrink-0">
            <circle cx="65" cy="65" r="54" stroke="currentColor" class="text-gray-200 dark:text-gray-700" stroke-width="12" fill="none" />
            <circle
              cx="65"
              cy="65"
              r="54"
              stroke="currentColor"
              class="text-primary-500"
              stroke-width="12"
              fill="none"
              stroke-linecap="round"
              transform="rotate(-90 65 65)"
              :stroke-dasharray="2 * Math.PI * 54"
              :stroke-dashoffset="ringStroke"
            />
            <text x="65" y="70" text-anchor="middle" class="fill-gray-900 dark:fill-white text-xl font-bold">{{ overallProgress }}%</text>
          </svg>
          <div>
            <p class="text-sm text-gray-600 dark:text-gray-400">Cập nhật từ các mốc đã được PM/AI xác nhận</p>
            <p class="mt-2 text-xs text-gray-500 dark:text-gray-400">Chế độ chỉ xem, chỉ theo dõi tiến độ và nhật ký thi công.</p>
          </div>
        </div>
      </div>

      <div class="card">
        <p class="text-sm text-gray-500 dark:text-gray-400">Số dư ví hiện tại</p>
        <p class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">{{ walletStore.summary?.balance?.toLocaleString('vi-VN') || 0 }} VND</p>
        <RouterLink to="/wallet" class="mt-3 inline-block text-sm font-medium text-primary-600 hover:text-primary-700">Xem ví chi tiết</RouterLink>
      </div>
    </div>

    <div class="card">
      <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div>
          <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Kanban theo dõi tiến độ (chỉ xem)</h2>
          <p class="text-sm text-gray-500 dark:text-gray-400">Khách hàng chỉ xem, không được thao tác thay đổi công việc.</p>
        </div>
        <select v-model="selectedKanbanProjectId" class="input md:w-80" @change="loadClientKanban">
          <option v-for="project in projects" :key="project.id" :value="project.id">{{ project.name }}</option>
        </select>
      </div>

      <div v-if="isKanbanLoading" class="mt-4 text-sm text-gray-500 dark:text-gray-400">Đang tải Kanban...</div>
      <div v-else class="mt-4 flex gap-4 overflow-x-auto pb-2 -mx-2 px-2">
        <div v-for="column in kanbanColumns" :key="column.id" class="flex-shrink-0 w-72">
          <div class="flex items-center justify-between mb-3 px-1">
            <h3 class="text-sm font-semibold text-gray-900 dark:text-white">{{ column.title }}</h3>
            <span class="text-xs px-2 py-0.5 rounded-full bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-200">
              {{ kanbanByStatus[column.id].length }}
            </span>
          </div>
          <div :class="['rounded-xl border border-gray-200 dark:border-gray-700 p-3 min-h-[220px] space-y-2', column.color]">
            <div
              v-for="task in kanbanByStatus[column.id].slice(0, 6)"
              :key="task.id"
              class="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 p-3"
            >
              <p class="text-sm font-medium text-gray-900 dark:text-white line-clamp-2">{{ task.name }}</p>
              <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">{{ task.progressPct }}%</p>
              <div class="mt-2 h-1.5 rounded-full bg-gray-200 dark:bg-gray-700 overflow-hidden">
                <div class="h-full bg-primary-500 rounded-full" :style="{ width: `${task.progressPct}%` }"></div>
              </div>
            </div>
            <div v-if="kanbanByStatus[column.id].length === 0" class="text-xs text-gray-500 dark:text-gray-400 py-8 text-center">Không có công việc</div>
          </div>
        </div>
      </div>
    </div>

    <div class="card">
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Timeline cập nhật thi công mới nhất</h2>
        <RouterLink to="/projects" class="text-sm font-medium text-primary-600 hover:text-primary-700">Xem dự án</RouterLink>
      </div>

      <div v-if="activeSlides.length" class="mt-3 rounded-lg border border-gray-200 dark:border-gray-700 p-3">
        <div class="flex items-center justify-between text-sm">
          <p class="text-gray-600 dark:text-gray-300">Tiến độ trung bình timeline đang chọn</p>
          <p class="font-semibold text-gray-900 dark:text-white">{{ activeSlidesAvgProgress }}%</p>
        </div>
        <div class="mt-2 h-2.5 rounded-full bg-gray-200 dark:bg-gray-700 overflow-hidden">
          <div class="h-full rounded-full bg-primary-500" :style="{ width: `${activeSlidesAvgProgress}%` }"></div>
        </div>
      </div>

      <div v-if="loading" class="mt-4 text-sm text-gray-500 dark:text-gray-400">Đang tải dữ liệu...</div>
      <div v-else-if="timelineByProject.length === 0" class="mt-4 text-sm text-gray-500 dark:text-gray-400">Chưa có cập nhật timeline nào.</div>
      <div v-else class="mt-4 space-y-4">
        <div class="flex flex-wrap gap-2">
          <button
            v-for="project in timelineByProject"
            :key="project.projectId"
            @click="activeProjectId = project.projectId"
            :class="[
              'px-3 py-1.5 rounded-full text-sm border transition-colors',
              activeProjectId === project.projectId
                ? 'bg-primary-600 text-white border-primary-600'
                : 'bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-300 border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700'
            ]"
          >
            {{ project.projectName }} ({{ project.items.length }})
          </button>
        </div>

        <div v-if="activeSlide" class="rounded-xl border border-gray-200 dark:border-gray-700 overflow-hidden">
          <div class="relative bg-black/80">
            <img
              :src="activeSlide.imageUrl"
              alt="Timeline image"
              class="h-72 md:h-96 w-full object-cover"
              loading="lazy"
            />

            <button
              class="absolute left-3 top-1/2 -translate-y-1/2 h-9 w-9 rounded-full bg-black/50 text-white hover:bg-black/70"
              @click="previousSlide"
            >
              ‹
            </button>
            <button
              class="absolute right-3 top-1/2 -translate-y-1/2 h-9 w-9 rounded-full bg-black/50 text-white hover:bg-black/70"
              @click="nextSlide"
            >
              ›
            </button>

            <div class="absolute inset-x-0 bottom-0 p-4 bg-gradient-to-t from-black/80 to-transparent text-white">
              <div class="flex items-start justify-between gap-4">
                <div>
                  <p class="text-sm font-semibold">{{ activeSlide.title }}</p>
                  <p class="text-xs opacity-90">{{ activeSlide.statusText }} - Duyệt bởi {{ activeSlide.approvedBy }}</p>
                  <p class="text-xs opacity-80 mt-1">{{ formatTime(activeSlide.time) }}</p>
                </div>
                <p class="text-sm font-semibold">{{ activeSlide.progress }}%</p>
              </div>
            </div>
          </div>

          <div class="p-3 border-t border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900">
            <div class="flex items-center justify-between">
              <p class="text-xs text-gray-500 dark:text-gray-400">Mốc {{ activeSlideIndex + 1 }} / {{ activeSlides.length }} của dự án</p>
              <div class="flex gap-1">
                <button
                  v-for="(slide, index) in activeSlides"
                  :key="slide.id"
                  class="h-2.5 w-2.5 rounded-full"
                  :class="index === activeSlideIndex ? 'bg-primary-600' : 'bg-gray-300 dark:bg-gray-600'"
                  @click="activeSlideIndex = index"
                ></button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
