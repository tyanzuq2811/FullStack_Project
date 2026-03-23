<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api } from '../../services/api'
import type { Project, User } from '../../types'

interface NewsItem {
  id: number
  title: string
  updatedAt: string
}

interface ResourceItem {
  id: number
  name: string
  isActive: boolean
}

interface ServiceState {
  name: string
  healthy: boolean
  latencyMs: number
  note: string
}

const isLoading = ref(true)
const projects = ref<Project[]>([])
const users = ref<User[]>([])
const news = ref<NewsItem[]>([])
const resources = ref<ResourceItem[]>([])
const serviceHealth = ref<ServiceState[]>([])

const ongoingCount = computed(() => projects.value.filter(p => p.status === 1).length)

const projectStatusCounts = computed(() => {
  const planning = projects.value.filter(p => p.status === 0).length
  const ongoing = projects.value.filter(p => p.status === 1).length
  const handover = projects.value.filter(p => p.status === 2).length
  const completed = projects.value.filter(p => p.status === 3).length
  return [
    { key: 'planning', label: 'Lập kế hoạch', value: planning, color: 'bg-slate-400' },
    { key: 'ongoing', label: 'Đang triển khai', value: ongoing, color: 'bg-blue-500' },
    { key: 'handover', label: 'Bàn giao', value: handover, color: 'bg-amber-500' },
    { key: 'completed', label: 'Hoàn thành', value: completed, color: 'bg-emerald-500' }
  ]
})

const maxProjectBucket = computed(() => {
  const maxValue = Math.max(...projectStatusCounts.value.map(item => item.value), 1)
  return maxValue
})

const userRoleCounts = computed(() => {
  const counts = {
    ProjectManager: 0,
    Subcontractor: 0,
    Client: 0
  }

  for (const user of users.value) {
    if (user.roles.includes('ProjectManager')) counts.ProjectManager += 1
    if (user.roles.includes('Subcontractor')) counts.Subcontractor += 1
    if (user.roles.includes('Client')) counts.Client += 1
  }

  return counts
})

const userRoleTotal = computed(() => {
  return userRoleCounts.value.ProjectManager + userRoleCounts.value.Subcontractor + userRoleCounts.value.Client
})

const serviceUpCount = computed(() => serviceHealth.value.filter(service => service.healthy).length)
const averageLatency = computed(() => {
  if (serviceHealth.value.length === 0) return 0
  return Math.round(serviceHealth.value.reduce((sum, service) => sum + service.latencyMs, 0) / serviceHealth.value.length)
})

const roleDistribution = computed(() => {
  const total = userRoleTotal.value || 1
  const source = userRoleCounts.value

  return [
    { key: 'ProjectManager', label: 'PM', value: source.ProjectManager, color: 'bg-blue-500', pct: Math.round((source.ProjectManager / total) * 100) },
    { key: 'Subcontractor', label: 'Thầu phụ', value: source.Subcontractor, color: 'bg-amber-500', pct: Math.round((source.Subcontractor / total) * 100) },
    { key: 'Client', label: 'Khách hàng', value: source.Client, color: 'bg-emerald-500', pct: Math.round((source.Client / total) * 100) }
  ]
})

const donutStyle = computed(() => {
  const total = userRoleTotal.value || 1
  const pm = (userRoleCounts.value.ProjectManager / total) * 100
  const subcontractor = (userRoleCounts.value.Subcontractor / total) * 100
  const client = 100 - pm - subcontractor

  return {
    background: `conic-gradient(#3b82f6 0 ${pm}%, #f59e0b ${pm}% ${pm + subcontractor}%, #10b981 ${pm + subcontractor}% ${pm + subcontractor + client}%)`
  }
})

const masterDataChanges = computed(() => {
  const newsChanges = news.value.slice(0, 5).map(item => ({
    id: `news-${item.id}`,
    title: `Tin nội bộ cập nhật: ${item.title}`,
    at: item.updatedAt
  }))

  const resourceChanges = resources.value.slice(0, 5).map(item => ({
    id: `resource-${item.id}`,
    title: `Tài nguyên ${item.name} đang ${item.isActive ? 'hoạt động' : 'tạm dừng'}`,
    at: new Date().toISOString()
  }))

  return [...newsChanges, ...resourceChanges]
    .sort((a, b) => new Date(b.at).getTime() - new Date(a.at).getTime())
    .slice(0, 7)
})

const formatAgo = (dateIso: string) => {
  const diffMs = Date.now() - new Date(dateIso).getTime()
  const diffMin = Math.floor(diffMs / 60000)
  if (diffMin < 1) return 'vừa xong'
  if (diffMin < 60) return `${diffMin} phút trước`
  const diffHour = Math.floor(diffMin / 60)
  if (diffHour < 24) return `${diffHour} giờ trước`
  return `${Math.floor(diffHour / 24)} ngày trước`
}

const checkService = async (name: string, url: string): Promise<ServiceState> => {
  const startedAt = performance.now()
  try {
    await api.get(url)
    return {
      name,
      healthy: true,
      latencyMs: Math.round(performance.now() - startedAt),
      note: 'Hoạt động ổn định'
    }
  } catch {
    return {
      name,
      healthy: false,
      latencyMs: Math.round(performance.now() - startedAt),
      note: 'Cần kiểm tra log hệ thống'
    }
  }
}

onMounted(async () => {
  try {
    const [projectsRes, usersRes, newsRes, resourcesRes, health] = await Promise.allSettled([
      api.get('/projects'),
      api.get('/users'),
      api.get('/news'),
      api.get('/bookings/resources'),
      Promise.all([
        checkService('Auth Service', '/auth/me'),
        checkService('Project Service', '/projects'),
        checkService('User Service', '/users'),
        checkService('News Service', '/news')
      ])
    ])

    if (projectsRes.status === 'fulfilled' && projectsRes.value.data?.success) {
      projects.value = projectsRes.value.data.data || []
    }
    if (usersRes.status === 'fulfilled' && usersRes.value.data?.success) {
      users.value = usersRes.value.data.data || []
    }
    if (newsRes.status === 'fulfilled' && newsRes.value.data?.success) {
      news.value = newsRes.value.data.data || []
    }
    if (resourcesRes.status === 'fulfilled' && resourcesRes.value.data?.success) {
      resources.value = resourcesRes.value.data.data || []
    }
    if (health.status === 'fulfilled') {
      serviceHealth.value = health.value
    }
  } finally {
    isLoading.value = false
  }
})
</script>

<template>
  <div class="space-y-6">
    <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
      <div class="card">
        <p class="text-sm text-gray-500 dark:text-gray-400">Tổng dự án đang chạy</p>
        <p class="mt-1 text-3xl font-bold text-gray-900 dark:text-white">{{ ongoingCount }}</p>
      </div>
      <div class="card">
        <p class="text-sm text-gray-500 dark:text-gray-400">Tổng người dùng hệ thống</p>
        <p class="mt-1 text-3xl font-bold text-gray-900 dark:text-white">{{ users.length }}</p>
      </div>
      <div class="card">
        <p class="text-sm text-gray-500 dark:text-gray-400">Lượt cập nhật master data</p>
        <p class="mt-1 text-3xl font-bold text-gray-900 dark:text-white">{{ masterDataChanges.length }}</p>
      </div>
    </div>

    <div class="grid grid-cols-1 xl:grid-cols-2 gap-6">
      <div class="card">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Phân bổ người dùng theo vai trò</h2>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">PM, Thầu phụ và Khách hàng</p>
        <div class="mt-4 grid grid-cols-1 md:grid-cols-2 gap-4 items-center">
          <div class="mx-auto h-44 w-44 rounded-full p-5" :style="donutStyle">
            <div class="h-full w-full rounded-full bg-white dark:bg-gray-900 flex flex-col items-center justify-center">
              <p class="text-xs text-gray-500 dark:text-gray-400">Tổng</p>
              <p class="text-2xl font-bold text-gray-900 dark:text-white">{{ userRoleTotal }}</p>
            </div>
          </div>

          <div class="space-y-4">
            <div v-for="role in roleDistribution" :key="role.key" class="space-y-1">
              <div class="flex items-center justify-between text-sm text-gray-700 dark:text-gray-200">
                <span>{{ role.label }}</span>
                <span>{{ role.value }} ({{ role.pct }}%)</span>
              </div>
              <div class="h-2 rounded-full bg-gray-200 dark:bg-gray-700 overflow-hidden">
                <div :class="['h-full rounded-full', role.color]" :style="{ width: `${role.pct}%` }"></div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="card">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Phân bổ trạng thái dự án</h2>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">Biểu đồ cột thể hiện tình hình vận hành</p>
        <div class="mt-6 grid grid-cols-4 gap-3 items-end min-h-[160px]">
          <div v-for="bucket in projectStatusCounts" :key="bucket.key" class="flex flex-col items-center gap-2">
            <div class="w-full max-w-[48px] rounded-t-md" :class="bucket.color" :style="{ height: `${Math.max(20, (bucket.value / maxProjectBucket) * 130)}px` }"></div>
            <p class="text-xs text-gray-500 dark:text-gray-400 text-center leading-tight">{{ bucket.label }}</p>
            <p class="text-sm font-semibold text-gray-900 dark:text-white">{{ bucket.value }}</p>
          </div>
        </div>
      </div>
      <div class="card xl:col-span-2">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Sức khỏe dịch vụ</h2>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">Theo dõi tình trạng các API quan trọng</p>
        <div class="mt-3 grid grid-cols-1 md:grid-cols-2 gap-3">
          <div class="rounded-lg border border-emerald-200 dark:border-emerald-900 bg-emerald-50/60 dark:bg-emerald-900/20 p-3">
            <p class="text-xs text-emerald-700 dark:text-emerald-300">Dịch vụ hoạt động</p>
            <p class="text-xl font-bold text-emerald-700 dark:text-emerald-300">{{ serviceUpCount }}/{{ serviceHealth.length }}</p>
          </div>
          <div class="rounded-lg border border-blue-200 dark:border-blue-900 bg-blue-50/60 dark:bg-blue-900/20 p-3">
            <p class="text-xs text-blue-700 dark:text-blue-300">Độ trễ trung bình</p>
            <p class="text-xl font-bold text-blue-700 dark:text-blue-300">{{ averageLatency }} ms</p>
          </div>
        </div>
        <div class="mt-4 space-y-3">
          <div v-for="service in serviceHealth" :key="service.name" class="rounded-lg border border-gray-200 dark:border-gray-700 p-3 flex items-center justify-between">
            <div>
              <p class="font-medium text-gray-900 dark:text-white">{{ service.name }}</p>
              <p class="text-xs text-gray-500 dark:text-gray-400">{{ service.note }}</p>
            </div>
            <div class="text-right">
              <p :class="service.healthy ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'" class="font-semibold">
                {{ service.healthy ? 'UP' : 'DOWN' }}
              </p>
              <p class="text-xs text-gray-500 dark:text-gray-400">{{ service.latencyMs }} ms</p>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="card">
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Master Data thay đổi gần đây</h2>
        <RouterLink to="/users" class="text-sm font-medium text-primary-600 hover:text-primary-700">Quản lý người dùng</RouterLink>
      </div>

      <div v-if="isLoading" class="mt-4 text-sm text-gray-500 dark:text-gray-400">Đang tải dữ liệu...</div>
      <div v-else-if="masterDataChanges.length === 0" class="mt-4 text-sm text-gray-500 dark:text-gray-400">Chưa có thay đổi nào.</div>
      <div v-else class="mt-4 space-y-2">
        <div v-for="change in masterDataChanges" :key="change.id" class="rounded-lg border border-gray-200 dark:border-gray-700 px-3 py-2 flex items-center justify-between">
          <p class="text-sm text-gray-800 dark:text-gray-200">{{ change.title }}</p>
          <p class="text-xs text-gray-500 dark:text-gray-400">{{ formatAgo(change.at) }}</p>
        </div>
      </div>
    </div>
  </div>
</template>
