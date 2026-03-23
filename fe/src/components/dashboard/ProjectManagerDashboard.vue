<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api } from '../../services/api'
import CalendarView from '../CalendarView.vue'
import type { ProjectTask, Resource } from '../../types'
import { useToast } from 'vue-toastification'

interface CalendarBooking {
  id: string
  resourceId: number
  resourceName: string
  memberName: string
  startTime: string
  endTime: string
  status: string
  price: number
}

const loading = ref(true)
const bookingsLoading = ref(true)
const toast = useToast()
const tasks = ref<ProjectTask[]>([])
const bookings = ref<CalendarBooking[]>([])
const resources = ref<Resource[]>([])
const draggedTask = ref<ProjectTask | null>(null)

const columns = [
  { id: 0, title: 'Chờ thực hiện', color: 'bg-gray-100 dark:bg-gray-700' },
  { id: 1, title: 'Đang thực hiện', color: 'bg-blue-50 dark:bg-blue-950/30' },
  { id: 2, title: 'Chờ nghiệm thu', color: 'bg-amber-50 dark:bg-amber-950/30' },
  { id: 3, title: 'Hoàn thành', color: 'bg-emerald-50 dark:bg-emerald-950/30' }
]

const tasksByStatus = computed(() => {
  const grouped: Record<number, ProjectTask[]> = { 0: [], 1: [], 2: [], 3: [] }
  for (const task of tasks.value) {
    if (grouped[task.status]) grouped[task.status].push(task)
  }
  return grouped
})

const reviewTasks = computed(() => tasks.value.filter(task => task.status === 2))

const busyResourcesNow = computed(() => {
  const now = Date.now()
  const activeBookings = bookings.value.filter(booking => {
    const start = new Date(booking.startTime).getTime()
    const end = new Date(booking.endTime).getTime()
    return start <= now && end >= now && (booking.status === 'Confirmed' || booking.status === 'Completed' || booking.status === 'InUse')
  })
  return new Set(activeBookings.map(booking => booking.resourceId)).size
})

const freeResourceNow = computed(() => {
  return Math.max(resources.value.length - busyResourcesNow.value, 0)
})

const taskFlowStats = computed(() => {
  const total = tasks.value.length || 1
  const todo = tasksByStatus.value[0]?.length || 0
  const inProgress = tasksByStatus.value[1]?.length || 0
  const review = tasksByStatus.value[2]?.length || 0
  const done = tasksByStatus.value[3]?.length || 0

  return [
    { key: 'todo', label: 'Chờ thực hiện', value: todo, pct: Math.round((todo / total) * 100), color: 'bg-slate-400' },
    { key: 'inProgress', label: 'Đang thực hiện', value: inProgress, pct: Math.round((inProgress / total) * 100), color: 'bg-blue-500' },
    { key: 'review', label: 'Chờ nghiệm thu', value: review, pct: Math.round((review / total) * 100), color: 'bg-amber-500' },
    { key: 'done', label: 'Hoàn thành', value: done, pct: Math.round((done / total) * 100), color: 'bg-emerald-500' }
  ]
})

const reviewRisk = computed(() => {
  const total = tasks.value.length || 1
  const ratio = reviewTasks.value.length / total

  if (ratio >= 0.4) {
    return {
      level: 'Cao',
      note: 'Nhiều công việc đang chờ nghiệm thu, dễ dồn giải ngân cuối kỳ.',
      boxClass: 'border-red-300 bg-red-50/80 dark:border-red-900 dark:bg-red-950/20',
      textClass: 'text-red-700 dark:text-red-300'
    }
  }

  if (ratio >= 0.2) {
    return {
      level: 'Cảnh báo',
      note: 'Cần phân bổ thời gian duyệt để tránh tắc nghẽn quy trình.',
      boxClass: 'border-amber-300 bg-amber-50/80 dark:border-amber-900 dark:bg-amber-950/20',
      textClass: 'text-amber-700 dark:text-amber-300'
    }
  }

  return {
    level: 'An toàn',
    note: 'Khối lượng chờ duyệt đang trong ngưỡng kiểm soát.',
    boxClass: 'border-emerald-300 bg-emerald-50/80 dark:border-emerald-900 dark:bg-emerald-950/20',
    textClass: 'text-emerald-700 dark:text-emerald-300'
  }
})

const handleDragStart = (event: DragEvent, task: ProjectTask) => {
  draggedTask.value = task
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData('text/plain', task.id.toString())
  }
}

const handleDragOver = (event: DragEvent) => {
  event.preventDefault()
}

const handleDrop = async (event: DragEvent, newStatus: number) => {
  event.preventDefault()
  if (!draggedTask.value || draggedTask.value.status === newStatus) return

  const taskId = draggedTask.value.id
  const previousStatus = draggedTask.value.status

  if (newStatus === 3) {
    if (previousStatus !== 2) {
      toast.error('Chỉ có thể nghiệm thu công việc đang ở cột Chờ nghiệm thu.')
      draggedTask.value = null
      return
    }

    try {
      await api.post(`/tasks/${taskId}/approve`, { approved: true, notes: null })
      const approveIndex = tasks.value.findIndex(task => task.id === taskId)
      if (approveIndex !== -1) {
        tasks.value[approveIndex].status = 3 as any
      }
      toast.success('Nghiệm thu công việc thành công.')
    } catch (error: any) {
      toast.error(error?.response?.data?.message || 'Không thể nghiệm thu công việc.')
    } finally {
      draggedTask.value = null
    }
    return
  }

  const taskIndex = tasks.value.findIndex(task => task.id === taskId)

  if (taskIndex !== -1) {
    tasks.value[taskIndex].status = newStatus as any
  }

  try {
    await api.put(`/tasks/${taskId}/status`, { newStatus })
  } catch {
    if (taskIndex !== -1) {
      tasks.value[taskIndex].status = previousStatus
    }
  } finally {
    draggedTask.value = null
  }
}

const fetchDashboardData = async () => {
  const bookingStatusMap: Record<number, string> = {
    0: 'PendingPayment',
    1: 'Confirmed',
    2: 'Cancelled',
    3: 'InUse'
  }

  try {
    const [projectsRes, bookingsRes, resourcesRes] = await Promise.all([
      api.get('/projects/my'),
      api.get('/bookings'),
      api.get('/bookings/resources')
    ])

    if (projectsRes.data?.success) {
      const myProjects = projectsRes.data.data || []
      if (myProjects.length > 0) {
        const taskResponses = await Promise.all(
          myProjects.map((project: any) => api.get(`/tasks/project/${project.id}`))
        )

        tasks.value = taskResponses
          .filter(response => response.data?.success)
          .flatMap(response => response.data.data || [])
      } else {
        tasks.value = []
      }
    }

    if (bookingsRes.data?.success) {
      bookings.value = (bookingsRes.data.data || []).map((item: any) => ({
        id: item.id,
        resourceId: item.resourceId,
        resourceName: item.resourceName,
        memberName: item.memberName,
        startTime: item.startTime,
        endTime: item.endTime,
        status: typeof item.status === 'number' ? bookingStatusMap[item.status] || 'PendingPayment' : item.status,
        price: item.price
      }))
    }
    if (resourcesRes.data?.success) resources.value = resourcesRes.data.data || []
  } finally {
    loading.value = false
    bookingsLoading.value = false
  }
}

onMounted(fetchDashboardData)
</script>

<template>
  <div class="space-y-6">
    <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
      <div class="card">
        <p class="text-sm text-gray-500 dark:text-gray-400">Công việc cần điều phối</p>
        <p class="mt-1 text-3xl font-bold text-gray-900 dark:text-white">{{ tasks.length }}</p>
      </div>
      <div class="card">
        <p class="text-sm text-gray-500 dark:text-gray-400">Chờ PM nghiệm thu</p>
        <p class="mt-1 text-3xl font-bold text-gray-900 dark:text-white">{{ reviewTasks.length }}</p>
      </div>
      <div class="card">
        <p class="text-sm text-gray-500 dark:text-gray-400">Tài nguyên đang rảnh</p>
        <p class="mt-1 text-3xl font-bold text-gray-900 dark:text-white">{{ freeResourceNow }}/{{ resources.length }}</p>
      </div>
    </div>

    <div class="card">
      <div class="mb-4 flex items-center justify-between">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Bảng điều khiển tiến độ (Kanban)</h2>
        <RouterLink to="/kanban" class="text-sm font-medium text-primary-600 hover:text-primary-700">Mở toàn màn hình</RouterLink>
      </div>

      <div class="mb-5 rounded-xl border border-gray-200 dark:border-gray-700 p-3">
        <div class="flex items-center justify-between text-sm">
          <p class="text-gray-600 dark:text-gray-300">Luồng tiến độ công việc</p>
          <p class="font-semibold text-gray-900 dark:text-white">{{ tasks.length }} công việc</p>
        </div>
        <div class="mt-2 h-2.5 rounded-full bg-gray-200 dark:bg-gray-700 overflow-hidden flex">
          <div
            v-for="item in taskFlowStats"
            :key="item.key"
            :class="item.color"
            :style="{ width: `${item.pct}%` }"
          ></div>
        </div>
        <div class="mt-3 grid grid-cols-2 lg:grid-cols-4 gap-2">
          <div v-for="item in taskFlowStats" :key="`${item.key}-legend`" class="rounded-md border border-gray-200 dark:border-gray-700 px-2.5 py-1.5 text-xs">
            <p class="text-gray-600 dark:text-gray-300">{{ item.label }}</p>
            <p class="font-semibold text-gray-900 dark:text-white">{{ item.value }} ({{ item.pct }}%)</p>
          </div>
        </div>
      </div>

      <div v-if="loading" class="text-sm text-gray-500 dark:text-gray-400">Đang tải Kanban...</div>
      <div v-else class="grid grid-cols-1 lg:grid-cols-4 gap-4">
        <div
          v-for="column in columns"
          :key="column.id"
          :class="['rounded-xl border border-gray-200 dark:border-gray-700 p-3 min-h-[280px]', column.color]"
          @dragover="handleDragOver"
          @drop="handleDrop($event, column.id)"
        >
          <div class="mb-3 flex items-center justify-between">
            <h3 class="font-semibold text-gray-900 dark:text-white">{{ column.title }}</h3>
            <span class="text-xs rounded-full bg-white/90 dark:bg-gray-900/70 px-2 py-0.5">{{ tasksByStatus[column.id]?.length || 0 }}</span>
          </div>
          <div class="space-y-2">
            <div
              v-for="task in tasksByStatus[column.id]"
              :key="task.id"
              draggable="true"
              @dragstart="handleDragStart($event, task)"
              class="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 p-3 cursor-grab"
            >
              <p class="text-sm font-medium text-gray-900 dark:text-white line-clamp-2">{{ task.name }}</p>
              <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">{{ task.projectName }}</p>
              <div class="mt-2 h-1.5 rounded-full bg-gray-200 dark:bg-gray-700 overflow-hidden">
                <div class="h-full rounded-full bg-primary-500" :style="{ width: `${task.progressPct}%` }"></div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="grid grid-cols-1 xl:grid-cols-3 gap-6">
      <div class="xl:col-span-2">
        <CalendarView v-if="!bookingsLoading" :bookings="bookings" @view-booking="() => {}" />
        <div v-else class="card text-sm text-gray-500 dark:text-gray-400">Đang tải lịch tài nguyên...</div>
      </div>

      <div class="card">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Công việc chờ nghiệm thu giải ngân</h2>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">Các công việc ở trạng thái Chờ nghiệm thu cần PM phê duyệt</p>

        <div :class="['mt-3 rounded-lg border p-3', reviewRisk.boxClass]">
          <p class="text-xs uppercase tracking-wide" :class="reviewRisk.textClass">Mức rủi ro tồn đọng</p>
          <p class="mt-1 text-lg font-semibold" :class="reviewRisk.textClass">{{ reviewRisk.level }}</p>
          <p class="mt-1 text-sm" :class="reviewRisk.textClass">{{ reviewRisk.note }}</p>
        </div>

        <div v-if="reviewTasks.length === 0" class="mt-4 text-sm text-gray-500 dark:text-gray-400">Không có công việc nào đang chờ.</div>
        <div v-else class="mt-4 space-y-2 max-h-[420px] overflow-y-auto pr-1">
          <RouterLink
            v-for="task in reviewTasks"
            :key="task.id"
            :to="`/projects/${task.projectId}`"
            class="block rounded-lg border border-gray-200 dark:border-gray-700 px-3 py-2 hover:bg-gray-50 dark:hover:bg-gray-800"
          >
            <p class="text-sm font-medium text-gray-900 dark:text-white">{{ task.name }}</p>
            <p class="text-xs text-gray-500 dark:text-gray-400">{{ task.projectName }}</p>
          </RouterLink>
        </div>
      </div>
    </div>
  </div>
</template>
