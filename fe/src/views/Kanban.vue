<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, watch } from 'vue'
import { useMotion } from '@vueuse/motion'
import { api } from '../services/api'
import { useSignalR } from '../composables/useSignalR'
import { useAuthStore } from '../stores/auth'
import { useToast } from 'vue-toastification'
import type { ProjectTask, TaskStatus } from '../types'

const authStore = useAuthStore()
const toast = useToast()
const tasks = ref<ProjectTask[]>([])
const selectedProjectId = ref<string>('')
const availableProjects = ref<Array<{ id: string; name: string }>>([])
const subcontractors = ref<Array<{ id: string; fullName: string }>>([])
const isLoading = ref(true)
const isSubmittingTask = ref(false)
const draggedTask = ref<ProjectTask | null>(null)
const showCreateTaskModal = ref(false)
const joinedProjectIds = ref<Set<string>>(new Set())
const { joinProjectGroup, leaveProjectGroup } = useSignalR()
const isPM = computed(() => authStore.primaryRole === 'ProjectManager')
const isSubcontractor = computed(() => authStore.primaryRole === 'Subcontractor')
const isClient = computed(() => authStore.primaryRole === 'Client')
const canCreateTask = computed(() => isPM.value)
const canDragDrop = computed(() => isPM.value || isSubcontractor.value)
const isReadOnlyBoard = computed(() => isClient.value || (!isPM.value && !isSubcontractor.value))
const selectedSubcontractorFilter = ref('all')
const selectedPhaseFilter = ref('all')
const PM_KANBAN_FILTERS_KEY = 'pmKanbanFiltersByProject'

const createTaskForm = ref({
  projectId: '',
  name: '',
  phaseType: 0,
  subcontractorId: '',
  targetDate: '',
  estimatedCost: 0
})

const fullColumns = [
  { id: 0, title: 'Chờ thực hiện', color: 'bg-gray-100 dark:bg-gray-700', borderColor: 'border-gray-300 dark:border-gray-600' },
  { id: 1, title: 'Đang thi công', color: 'bg-blue-50 dark:bg-blue-900/20', borderColor: 'border-blue-300 dark:border-blue-700' },
  { id: 2, title: 'Chờ nghiệm thu', color: 'bg-yellow-50 dark:bg-yellow-900/20', borderColor: 'border-yellow-300 dark:border-yellow-700' },
  { id: 3, title: 'Đã hoàn thành', color: 'bg-green-50 dark:bg-green-900/20', borderColor: 'border-green-300 dark:border-green-700' },
  { id: 4, title: 'Bị chặn', color: 'bg-red-50 dark:bg-red-900/20', borderColor: 'border-red-300 dark:border-red-700' },
]

const subcontractorColumns = [
  { id: 0, title: 'Chờ thực hiện', color: 'bg-gray-100 dark:bg-gray-700', borderColor: 'border-gray-300 dark:border-gray-600' },
  { id: 1, title: 'Đang thi công', color: 'bg-blue-50 dark:bg-blue-900/20', borderColor: 'border-blue-300 dark:border-blue-700' },
  { id: 2, title: 'Chờ nghiệm thu', color: 'bg-yellow-50 dark:bg-yellow-900/20', borderColor: 'border-yellow-300 dark:border-yellow-700' },
  { id: 3, title: 'Đã hoàn thành', color: 'bg-green-50 dark:bg-green-900/20', borderColor: 'border-green-300 dark:border-green-700' }
]

const columns = computed(() => isSubcontractor.value ? subcontractorColumns : fullColumns)

const pmSubcontractorFilterOptions = computed(() => {
  const map = new Map<string, string>()
  for (const task of tasks.value) {
    if (task.subcontractorId && task.subcontractorName) {
      map.set(task.subcontractorId, task.subcontractorName)
    }
  }

  return Array.from(map.entries()).map(([id, fullName]) => ({ id, fullName }))
})

const phaseFilterOptions = [
  { value: '0', label: 'Thao do' },
  { value: '1', label: 'Dien nuoc' },
  { value: '2', label: 'Thach cao' },
  { value: '3', label: 'Son ba' },
  { value: '4', label: 'Do go' }
]

const boardTasks = computed(() => {
  if (!isPM.value) return tasks.value

  return tasks.value.filter(task => {
    const subcontractorMatch =
      selectedSubcontractorFilter.value === 'all' ||
      task.subcontractorId === selectedSubcontractorFilter.value

    const phaseMatch =
      selectedPhaseFilter.value === 'all' ||
      task.phaseType === Number(selectedPhaseFilter.value)

    return subcontractorMatch && phaseMatch
  })
})

const pmFilteredCountLabel = computed(() => {
  if (!isPM.value) return ''
  return `Filtered ${boardTasks.value.length}/${tasks.value.length} tasks`
})

const isPmFilterActive = computed(() => {
  if (!isPM.value) return false
  return selectedSubcontractorFilter.value !== 'all' || selectedPhaseFilter.value !== 'all'
})

const tasksByStatus = computed(() => {
  const grouped: Record<number, ProjectTask[]> = {}
  columns.value.forEach(col => grouped[col.id] = [])
  boardTasks.value.forEach(task => {
    if (grouped[task.status] !== undefined) {
      grouped[task.status].push(task)
    }
  })
  return grouped
})

const canDragTask = (task: ProjectTask) => {
  if (isPM.value) return true
  if (isSubcontractor.value) {
    return task.subcontractorId === authStore.user?.id
  }
  return false
}

const canSubcontractorTransition = (fromStatus: number, toStatus: number) => {
  return (fromStatus === 0 && toStatus === 1) || (fromStatus === 1 && toStatus === 2)
}

const syncProjectGroups = async () => {
  const projectIds = [...new Set(tasks.value.map(task => task.projectId))]
  for (const projectId of projectIds) {
    if (!joinedProjectIds.value.has(projectId)) {
      await joinProjectGroup(projectId)
      joinedProjectIds.value.add(projectId)
    }
  }
}

const getPmFiltersStore = (): Record<string, { subcontractorId: string; phase: string }> => {
  try {
    const raw = sessionStorage.getItem(PM_KANBAN_FILTERS_KEY)
    if (!raw) return {}
    const parsed = JSON.parse(raw)
    return parsed && typeof parsed === 'object' ? parsed : {}
  } catch {
    return {}
  }
}

const savePmFiltersStore = (store: Record<string, { subcontractorId: string; phase: string }>) => {
  sessionStorage.setItem(PM_KANBAN_FILTERS_KEY, JSON.stringify(store))
}

const persistPmFiltersForCurrentProject = () => {
  if (!isPM.value || !selectedProjectId.value) return

  const store = getPmFiltersStore()
  store[selectedProjectId.value] = {
    subcontractorId: selectedSubcontractorFilter.value,
    phase: selectedPhaseFilter.value
  }
  savePmFiltersStore(store)
}

const restorePmFiltersForCurrentProject = () => {
  if (!isPM.value || !selectedProjectId.value) return

  const store = getPmFiltersStore()
  const projectFilters = store[selectedProjectId.value]
  if (!projectFilters) {
    selectedSubcontractorFilter.value = 'all'
    selectedPhaseFilter.value = 'all'
    return
  }

  selectedSubcontractorFilter.value = projectFilters.subcontractorId || 'all'
  selectedPhaseFilter.value = projectFilters.phase || 'all'
}

const loadProjectsForScope = async () => {
  const endpoint = authStore.primaryRole === 'Admin' ? '/projects' : '/projects/my'
  const response = await api.get(endpoint)
  if (response.data.success) {
    availableProjects.value = (response.data.data || []).map((p: any) => ({ id: p.id, name: p.name }))
    if (!selectedProjectId.value && availableProjects.value.length > 0) {
      selectedProjectId.value = availableProjects.value[0].id
    }
  }
}

const loadSubcontractors = async () => {
  if (!isPM.value) return

  const response = await api.get('/users/subcontractors')
  if (response.data?.success) {
    subcontractors.value = (response.data.data || []).map((user: any) => ({
      id: user.id,
      fullName: user.fullName
    }))
  }
}

const loadTasksByRole = async () => {
  if (isSubcontractor.value) {
    const response = await api.get('/tasks/my')
    if (response.data.success) {
      tasks.value = response.data.data || []
    }
    return
  }

  await loadProjectsForScope()
  if (!selectedProjectId.value) {
    tasks.value = []
    return
  }

  const response = await api.get(`/tasks/project/${selectedProjectId.value}`)
  if (response.data.success) {
    tasks.value = response.data.data || []
    restorePmFiltersForCurrentProject()
  }
}

const handleDragStart = (e: DragEvent, task: ProjectTask) => {
  if (!canDragTask(task)) {
    toast.error('Ban khong duoc thao tac task nay.')
    return
  }

  draggedTask.value = task
  if (e.dataTransfer) {
    e.dataTransfer.effectAllowed = 'move'
    e.dataTransfer.setData('text/plain', task.id.toString())
  }
  // Add visual feedback
  const target = e.target as HTMLElement
  target.classList.add('opacity-50', 'scale-95')
}

const handleDragEnd = (e: DragEvent) => {
  const target = e.target as HTMLElement
  target.classList.remove('opacity-50', 'scale-95')
  draggedTask.value = null
}

const handleDragOver = (e: DragEvent) => {
  e.preventDefault()
  if (e.dataTransfer) {
    e.dataTransfer.dropEffect = 'move'
  }
}

const handleDragEnter = (e: DragEvent, columnId: number) => {
  e.preventDefault()
  const target = e.currentTarget as HTMLElement
  target.classList.add('ring-2', 'ring-primary-400', 'ring-inset')
}

const handleDragLeave = (e: DragEvent) => {
  const target = e.currentTarget as HTMLElement
  target.classList.remove('ring-2', 'ring-primary-400', 'ring-inset')
}

const handleDrop = async (e: DragEvent, newStatus: number) => {
  e.preventDefault()
  const target = e.currentTarget as HTMLElement
  target.classList.remove('ring-2', 'ring-primary-400', 'ring-inset')

  if (!draggedTask.value || draggedTask.value.status === newStatus) {
    draggedTask.value = null
    return
  }

  if (!canDragDrop.value || !canDragTask(draggedTask.value)) {
    draggedTask.value = null
    toast.error('Ban khong co quyen cap nhat task nay.')
    return
  }

  if (isSubcontractor.value && !canSubcontractorTransition(draggedTask.value.status, newStatus)) {
    toast.error('Subcontractor chi duoc chuyen To Do -> In Progress hoac In Progress -> Review.')
    draggedTask.value = null
    return
  }

  const taskId = draggedTask.value.id
  const oldStatus = draggedTask.value.status

  if (isPM.value && newStatus === 3) {
    if (oldStatus !== 2) {
      toast.error('PM chi co the nghiem thu task dang o cot Cho nghiem thu.')
      draggedTask.value = null
      return
    }

    try {
      await api.post(`/tasks/${taskId}/approve`, { approved: true, notes: null })
      const taskIndex = tasks.value.findIndex(t => t.id === taskId)
      if (taskIndex !== -1) {
        tasks.value[taskIndex].status = 3 as TaskStatus
      }
      toast.success('Nghiem thu va giai ngan thanh cong.')
    } catch (error: any) {
      const message = error?.response?.data?.message || 'Khong the nghiem thu task.'
      toast.error(message)
      console.error('Failed to approve task:', error)
    }

    draggedTask.value = null
    return
  }

  // Optimistic update
  const taskIndex = tasks.value.findIndex(t => t.id === taskId)
  if (taskIndex !== -1) {
    tasks.value[taskIndex].status = newStatus as TaskStatus
  }

  try {
    await api.put(`/tasks/${taskId}/status`, { newStatus })
  } catch (error: any) {
    // Rollback on error
    if (taskIndex !== -1) {
      tasks.value[taskIndex].status = oldStatus
    }
    const message = error?.response?.data?.message || 'Khong the cap nhat trang thai task.'
    toast.error(message)
    console.error('Failed to update task status:', error)
  }

  draggedTask.value = null
}

// Handle real-time updates from SignalR
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
    await Promise.all([loadTasksByRole(), loadSubcontractors()])
    await syncProjectGroups()
  } finally {
    isLoading.value = false
  }

  window.addEventListener('taskStatusChanged', handleTaskStatusChanged as EventListener)
})

watch([selectedSubcontractorFilter, selectedPhaseFilter], () => {
  persistPmFiltersForCurrentProject()
})

onUnmounted(async () => {
  window.removeEventListener('taskStatusChanged', handleTaskStatusChanged as EventListener)

  for (const projectId of joinedProjectIds.value) {
    await leaveProjectGroup(projectId)
  }
})

const getPhaseTypeText = (phase: number) => {
  const phases = ['Tháo dỡ', 'Điện nước', 'Thạch cao', 'Sơn bả', 'Đồ gỗ']
  return phases[phase] || 'N/A'
}

const getPhaseTypeColor = (phase: number) => {
  const colors = [
    'bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300',
    'bg-blue-100 text-blue-700 dark:bg-blue-900/50 dark:text-blue-300',
    'bg-purple-100 text-purple-700 dark:bg-purple-900/50 dark:text-purple-300',
    'bg-orange-100 text-orange-700 dark:bg-orange-900/50 dark:text-orange-300',
    'bg-amber-100 text-amber-700 dark:bg-amber-900/50 dark:text-amber-300'
  ]
  return colors[phase] || colors[0]
}

const getProgressColor = (progress: number) => {
  if (progress >= 80) return 'bg-green-500'
  if (progress >= 50) return 'bg-yellow-500'
  if (progress >= 20) return 'bg-orange-500'
  return 'bg-red-500'
}

const loadProjectTasks = async () => {
  if (!selectedProjectId.value || isSubcontractor.value) return

  isLoading.value = true
  try {
    const response = await api.get(`/tasks/project/${selectedProjectId.value}`)
    if (response.data.success) {
      tasks.value = response.data.data || []
      restorePmFiltersForCurrentProject()
      await syncProjectGroups()
    }
  } finally {
    isLoading.value = false
  }
}

const openCreateTaskModal = () => {
  if (!isPM.value) return

  createTaskForm.value = {
    projectId: selectedProjectId.value,
    name: '',
    phaseType: 0,
    subcontractorId: '',
    targetDate: '',
    estimatedCost: 0
  }
  showCreateTaskModal.value = true
}

const submitCreateTask = async () => {
  if (!isPM.value || !createTaskForm.value.projectId || !createTaskForm.value.name.trim()) return

  isSubmittingTask.value = true
  try {
    const payload = {
      projectId: createTaskForm.value.projectId,
      phaseType: Number(createTaskForm.value.phaseType),
      name: createTaskForm.value.name.trim(),
      subcontractorId: createTaskForm.value.subcontractorId || null,
      targetDate: createTaskForm.value.targetDate ? new Date(createTaskForm.value.targetDate).toISOString() : null,
      estimatedCost: Number(createTaskForm.value.estimatedCost) || 0
    }

    const response = await api.post('/tasks', payload)
    if (response.data?.success) {
      showCreateTaskModal.value = false
      if (selectedProjectId.value !== payload.projectId) {
        selectedProjectId.value = payload.projectId
      }
      await loadProjectTasks()
    }
  } finally {
    isSubmittingTask.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Kanban Board</h1>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          {{
            isPM
              ? 'PM co toan quyen tao, phan cong va dieu phoi keo-tha'
              : isSubcontractor
                ? 'Ban chi thay task duoc giao va chi duoc cap nhat trang thai cho task cua minh'
                : 'Che do read-only: chi theo doi tien do, khong duoc thay doi task'
          }}
        </p>
      </div>
      <div class="flex items-center gap-2">
        <button v-if="canCreateTask" class="btn-primary" @click="openCreateTaskModal">
          Tao Task
        </button>
        <span
          v-if="isPM"
          :class="[
            'text-xs px-2 py-1 rounded-full border',
            isPmFilterActive
              ? 'bg-primary-50 text-primary-700 border-primary-200 dark:bg-primary-900/40 dark:text-primary-300 dark:border-primary-800'
              : 'bg-gray-50 text-gray-600 border-gray-200 dark:bg-gray-800 dark:text-gray-300 dark:border-gray-700'
          ]"
        >
          {{ pmFilteredCountLabel }}
        </span>
        <span class="flex items-center gap-1 text-sm text-gray-500 dark:text-gray-400">
          <span class="w-2 h-2 rounded-full bg-green-500 animate-pulse"></span>
          Real-time
        </span>
      </div>
    </div>

    <div v-if="!isSubcontractor" class="card">
      <div class="flex flex-col gap-3">
        <div class="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
        <p class="text-sm text-gray-600 dark:text-gray-400">
          {{ isPM ? 'Chon du an de dieu phoi task va phan cong doi thau.' : 'Ban chi duoc xem Kanban cua du an trong pham vi cua minh.' }}
        </p>
        <select v-model="selectedProjectId" class="input md:w-80" @change="loadProjectTasks">
          <option v-for="project in availableProjects" :key="project.id" :value="project.id">
            {{ project.name }}
          </option>
        </select>
        </div>

        <div v-if="isPM" class="grid grid-cols-1 md:grid-cols-3 gap-3">
          <div>
            <label class="block text-xs text-gray-500 dark:text-gray-400 mb-1">Loc theo thau phu</label>
            <select v-model="selectedSubcontractorFilter" class="input">
              <option value="all">Tat ca</option>
              <option v-for="sub in pmSubcontractorFilterOptions" :key="sub.id" :value="sub.id">
                {{ sub.fullName }}
              </option>
            </select>
          </div>
          <div>
            <label class="block text-xs text-gray-500 dark:text-gray-400 mb-1">Loc theo phase</label>
            <select v-model="selectedPhaseFilter" class="input">
              <option value="all">Tat ca</option>
              <option v-for="phase in phaseFilterOptions" :key="phase.value" :value="phase.value">
                {{ phase.label }}
              </option>
            </select>
          </div>
          <div class="flex items-end">
            <button class="btn-secondary w-full" @click="selectedSubcontractorFilter = 'all'; selectedPhaseFilter = 'all'">
              Xoa bo loc
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="isLoading" class="flex gap-4 overflow-x-auto pb-4">
      <div v-for="i in 5" :key="i" class="flex-shrink-0 w-72">
        <div class="skeleton h-8 w-32 mb-4 rounded"></div>
        <div class="space-y-3">
          <div v-for="j in 2" :key="j" class="skeleton h-28 rounded-lg"></div>
        </div>
      </div>
    </div>

    <!-- Kanban Board -->
    <div v-else class="flex gap-4 overflow-x-auto pb-4 -mx-2 px-2">
      <div
        v-for="column in columns"
        :key="column.id"
        class="flex-shrink-0 w-72"
      >
        <!-- Column Header -->
        <div class="flex items-center gap-2 mb-4 px-2">
          <div :class="['w-3 h-3 rounded-full', column.color.includes('gray') ? 'bg-gray-400' : column.color.includes('blue') ? 'bg-blue-500' : column.color.includes('yellow') ? 'bg-yellow-500' : column.color.includes('green') ? 'bg-green-500' : 'bg-red-500']"></div>
          <h3 class="font-semibold text-gray-900 dark:text-white">{{ column.title }}</h3>
          <span class="ml-auto px-2 py-0.5 text-xs font-medium rounded-full bg-gray-200 dark:bg-gray-700 text-gray-600 dark:text-gray-300">
            {{ tasksByStatus[column.id]?.length || 0 }}
          </span>
        </div>

        <!-- Column Drop Zone -->
        <div
          :class="[
            'min-h-[400px] rounded-xl p-3 space-y-3 transition-all duration-200 border-2 border-dashed',
            column.color,
            column.borderColor
          ]"
          @dragover="handleDragOver"
          @dragenter="handleDragEnter($event, column.id)"
          @dragleave="handleDragLeave"
          @drop="handleDrop($event, column.id)"
        >
          <!-- Task Cards -->
          <div
            v-for="task in tasksByStatus[column.id]"
            :key="task.id"
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :enter="{ opacity: 1, y: 0, transition: { delay: 50 } }"
            :draggable="canDragDrop && canDragTask(task)"
            @dragstart="handleDragStart($event, task)"
            @dragend="handleDragEnd"
            :class="[
              'bg-white dark:bg-gray-800 rounded-lg p-4 shadow-sm border border-gray-200 dark:border-gray-700 transition-all duration-200 transform hover:-translate-y-0.5',
              canDragDrop && canDragTask(task)
                ? 'cursor-grab active:cursor-grabbing hover:shadow-md hover:border-primary-300 dark:hover:border-primary-600'
                : 'cursor-default opacity-95'
            ]"
          >
            <!-- Task Name -->
            <h4 class="font-medium text-gray-900 dark:text-white mb-3 line-clamp-2">{{ task.name }}</h4>

            <!-- Phase Badge -->
            <div class="flex items-center justify-between mb-3">
              <span :class="['px-2 py-1 text-xs font-medium rounded-md', getPhaseTypeColor(task.phaseType)]">
                {{ getPhaseTypeText(task.phaseType) }}
              </span>
              <span class="text-sm font-semibold text-gray-700 dark:text-gray-300">{{ task.progressPct }}%</span>
            </div>

            <!-- Progress Bar -->
            <div class="w-full h-2 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden mb-3">
              <div
                :class="['h-full rounded-full transition-all duration-500', getProgressColor(task.progressPct)]"
                :style="{ width: `${task.progressPct}%` }"
              ></div>
            </div>

            <!-- Subcontractor -->
            <div v-if="task.subcontractorName" class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
              </svg>
              <span class="truncate">{{ task.subcontractorName }}</span>
            </div>

            <!-- Target Date -->
            <div v-if="task.targetDate" class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400 mt-2">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
              </svg>
              <span>{{ new Date(task.targetDate).toLocaleDateString('vi-VN') }}</span>
            </div>
          </div>

          <!-- Empty State -->
          <div v-if="!tasksByStatus[column.id]?.length" class="flex flex-col items-center justify-center py-12 text-gray-400 dark:text-gray-500">
            <svg class="w-12 h-12 mb-2 opacity-50" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
            </svg>
            <span class="text-sm">Không có công việc</span>
          </div>
        </div>
      </div>
    </div>

    <div v-if="showCreateTaskModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div class="w-full max-w-xl rounded-2xl bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 p-5">
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white">Tao task va phan cong doi thau</h3>
        <div class="mt-4 grid grid-cols-1 md:grid-cols-2 gap-4">
          <div class="md:col-span-2">
            <label class="block text-sm text-gray-600 dark:text-gray-300 mb-1">Du an</label>
            <select v-model="createTaskForm.projectId" class="input">
              <option v-for="project in availableProjects" :key="project.id" :value="project.id">{{ project.name }}</option>
            </select>
          </div>
          <div class="md:col-span-2">
            <label class="block text-sm text-gray-600 dark:text-gray-300 mb-1">Ten task</label>
            <input v-model="createTaskForm.name" class="input" placeholder="Nhap ten task" />
          </div>
          <div>
            <label class="block text-sm text-gray-600 dark:text-gray-300 mb-1">Giai doan</label>
            <select v-model="createTaskForm.phaseType" class="input">
              <option :value="0">Thao do</option>
              <option :value="1">Dien nuoc</option>
              <option :value="2">Thach cao</option>
              <option :value="3">Son ba</option>
              <option :value="4">Do go</option>
            </select>
          </div>
          <div>
            <label class="block text-sm text-gray-600 dark:text-gray-300 mb-1">Doi thau</label>
            <select v-model="createTaskForm.subcontractorId" class="input">
              <option value="">Chua phan cong</option>
              <option v-for="sub in subcontractors" :key="sub.id" :value="sub.id">{{ sub.fullName }}</option>
            </select>
          </div>
          <div>
            <label class="block text-sm text-gray-600 dark:text-gray-300 mb-1">Han muc du toan (VND)</label>
            <input v-model.number="createTaskForm.estimatedCost" type="number" min="0" class="input" />
          </div>
          <div>
            <label class="block text-sm text-gray-600 dark:text-gray-300 mb-1">Han hoan thanh</label>
            <input v-model="createTaskForm.targetDate" type="datetime-local" class="input" />
          </div>
        </div>
        <div class="mt-5 flex justify-end gap-2">
          <button class="btn-secondary" @click="showCreateTaskModal = false">Huy</button>
          <button class="btn-primary" :disabled="isSubmittingTask" @click="submitCreateTask">
            {{ isSubmittingTask ? 'Dang tao...' : 'Tao task' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.line-clamp-2 {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
</style>
