<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import api from '../services/api'
import { useToast } from 'vue-toastification'
import { useAuthStore } from '../stores/auth'
import { usePagination } from '../composables/usePagination'
import type { Project } from '../types'

const toast = useToast()
const authStore = useAuthStore()
const router = useRouter()

const projects = ref<Project[]>([])
const loading = ref(true)
const searchQuery = ref('')
const selectedStatus = ref(-1)

const showCreateModal = ref(false)
const creating = ref(false)
const clients = ref<Array<{ id: string; fullName: string; email: string }>>([])
const createForm = ref({
  name: '',
  clientId: '',
  startDate: '',
  targetDate: '',
  totalBudget: ''
})

const statusOptions = [
  { value: -1, label: 'Tất cả' },
  { value: 0, label: 'Lập kế hoạch' },
  { value: 1, label: 'Đang thực hiện' },
  { value: 2, label: 'Ban giao' },
  { value: 3, label: 'Hoàn thành' }
]

const MAX_TOTAL_BUDGET = 9999999999999999.99

const filteredProjects = computed(() => {
  let filtered = projects.value

  if (selectedStatus.value !== -1) {
    filtered = filtered.filter(p => p.status === selectedStatus.value)
  }

  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase()
    filtered = filtered.filter(p =>
      p.name.toLowerCase().includes(query) ||
      p.clientName.toLowerCase().includes(query)
    )
  }

  return filtered
})

const {
  pageSize,
  currentPage,
  totalPages,
  paginatedItems: paginatedProjects,
  pageNumbers,
  setPage
} = usePagination(filteredProjects, {
  pageSize: 10,
  resetOn: [searchQuery, selectedStatus]
})

const getStatusColor = (status: number) => {
  const colors: Record<number, string> = {
    0: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-300',
    1: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-300',
    2: 'bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-300',
    3: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300'
  }
  return colors[status] || 'bg-gray-100 text-gray-800'
}

const getStatusLabel = (status: number) => {
  const labels: Record<number, string> = {
    0: 'Lập kế hoạch',
    1: 'Đang thực hiện',
    2: 'Ban giao',
    3: 'Hoàn thành'
  }
  return labels[status] || 'N/A'
}

const formatCurrency = (amount: number) => {
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND'
  }).format(amount)
}

const formatDate = (dateString: string) => {
  if (!dateString) return 'Chưa xác định'
  const date = new Date(dateString)
  if (Number.isNaN(date.getTime())) return 'Chưa xác định'
  return date.toLocaleDateString('vi-VN')
}

const getDaysRemaining = (targetDate?: string) => {
  if (!targetDate) return null
  const target = new Date(targetDate)
  if (Number.isNaN(target.getTime())) return null
  const today = new Date()
  return Math.ceil((target.getTime() - today.getTime()) / (1000 * 60 * 60 * 24))
}

const getDaysRemainingLabel = (targetDate?: string) => {
  const days = getDaysRemaining(targetDate)
  return days === null ? 'Chưa xác định' : `${days} ngày`
}

const getDaysRemainingClass = (targetDate?: string) => {
  const days = getDaysRemaining(targetDate)
  if (days === null) return 'text-gray-500 dark:text-gray-400'
  return days < 7 ? 'text-red-600' : 'text-green-600'
}

const fetchProjects = async () => {
  try {
    loading.value = true
    const endpoint = authStore.primaryRole === 'Admin' ? '/projects' : '/projects/my'
    const response = await api.get(endpoint)

    if (response.data?.success && response.data?.data) {
      projects.value = response.data.data
    } else {
      projects.value = []
      toast.error(response.data?.message || 'Không thể tải danh sách dự án')
    }
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Không thể tải danh sách dự án')
  } finally {
    loading.value = false
  }
}

const fetchClients = async () => {
  if (authStore.primaryRole !== 'ProjectManager') return

  try {
    const response = await api.get('/users/clients')
    if (response.data?.success) {
      clients.value = response.data.data || []
    }
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Không thể tải danh sách khách hàng')
  }
}

const viewProject = (id: string) => {
  router.push(`/projects/${id}`)
}

const openCreateModal = () => {
  showCreateModal.value = true
}

const closeCreateModal = () => {
  showCreateModal.value = false
  createForm.value = {
    name: '',
    clientId: '',
    startDate: '',
    targetDate: '',
    totalBudget: ''
  }
}

const submitCreateProject = async () => {
  if (authStore.primaryRole !== 'ProjectManager') {
    toast.error('Chỉ PM được tạo dự án')
    return
  }

  if (!createForm.value.name || !createForm.value.clientId || !createForm.value.startDate || !createForm.value.totalBudget) {
    toast.error('Vui lòng nhập đầy đủ thông tin bắt buộc')
    return
  }

  const totalBudget = Number(createForm.value.totalBudget)
  if (!Number.isFinite(totalBudget) || totalBudget <= 0) {
    toast.error('Tổng ngân sách phải lớn hơn 0')
    return
  }

  if (totalBudget > MAX_TOTAL_BUDGET) {
    toast.error('Tổng ngân sách vượt quá giới hạn cho phép')
    return
  }

  if (createForm.value.targetDate && new Date(createForm.value.targetDate) < new Date(createForm.value.startDate)) {
    toast.error('Ngày dự kiến hoàn thành không được sớm hơn ngày bắt đầu')
    return
  }

  if (!authStore.user?.id) {
    toast.error('Không xác định được PM hiện tại')
    return
  }

  creating.value = true
  try {
    const payload = {
      name: createForm.value.name,
      clientId: createForm.value.clientId,
      managerId: authStore.user.id,
      startDate: createForm.value.startDate,
      targetDate: createForm.value.targetDate || null,
      totalBudget
    }

    const response = await api.post('/projects', payload)
    if (response.data?.success) {
      toast.success('Tạo dự án thành công')
      closeCreateModal()
      await fetchProjects()
      return
    }

    toast.error(response.data?.message || 'Không thể tạo dự án')
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Không thể tạo dự án')
  } finally {
    creating.value = false
  }
}

onMounted(() => {
  fetchClients()
  fetchProjects()
})
</script>

<template>
  <div class="space-y-6">
    <div class="flex items-center justify-between">
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Danh sách dự án</h1>
      <button v-if="authStore.primaryRole === 'ProjectManager'" @click="openCreateModal" class="btn btn-primary">
        <svg class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
        </svg>
        Tạo dự án mới
      </button>
    </div>

    <div class="card">
      <div class="flex flex-col md:flex-row gap-4">
        <div class="flex-1">
          <input
            v-model="searchQuery"
            type="text"
            placeholder="Tìm kiếm dự án, khách hàng..."
            class="input w-full"
          />
        </div>
        <select v-model="selectedStatus" class="input md:w-48">
          <option v-for="option in statusOptions" :key="option.value" :value="option.value">
            {{ option.label }}
          </option>
        </select>
      </div>
    </div>

    <div v-if="loading" class="flex justify-center py-12">
      <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-500"></div>
    </div>

    <div v-else-if="filteredProjects.length === 0" class="card text-center py-12">
      <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">Không tìm thấy dự án</h3>
      <p class="mt-2 text-gray-600 dark:text-gray-400">
        {{ searchQuery || selectedStatus !== -1 ? 'Thử thay đổi bộ lọc' : 'Bắt đầu bằng cách tạo dự án mới' }}
      </p>
    </div>

    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
      <div
        v-for="project in paginatedProjects"
        :key="project.id"
        class="card hover:shadow-lg transition-shadow cursor-pointer"
        @click="viewProject(project.id)"
      >
        <div class="flex items-start justify-between mb-4">
          <div class="flex-1">
            <h3 class="font-semibold text-gray-900 dark:text-white line-clamp-2">
              {{ project.name }}
            </h3>
            <p class="text-sm text-gray-600 dark:text-gray-400 mt-1">
              {{ project.clientName }}
            </p>
          </div>
          <span :class="['px-2 py-1 text-xs font-medium rounded-full', getStatusColor(project.status)]">
            {{ getStatusLabel(project.status) }}
          </span>
        </div>

        <div class="mb-4">
          <div class="flex items-center justify-between text-sm mb-2">
            <span class="text-gray-600 dark:text-gray-400">Tiến độ</span>
            <span class="font-medium text-gray-900 dark:text-white">{{ Math.round(project.progressPercentage) }}%</span>
          </div>
          <div class="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-2">
            <div
              class="bg-primary-600 h-2 rounded-full transition-all"
              :style="{ width: `${Math.round(project.progressPercentage)}%` }"
            ></div>
          </div>
        </div>

        <div class="space-y-2 text-sm">
          <div class="flex items-center text-gray-600 dark:text-gray-400">
            <span>{{ project.managerName }}</span>
          </div>
          <div class="flex items-center text-gray-600 dark:text-gray-400">
            <span>{{ formatDate(project.startDate) }} - {{ project.targetDate ? formatDate(project.targetDate) : 'Chưa xác định' }}</span>
          </div>
          <div class="flex items-center justify-between">
            <span class="text-gray-600 dark:text-gray-400">Ngân sách:</span>
            <span class="font-semibold text-gray-900 dark:text-white">
              {{ formatCurrency(project.totalBudget) }}
            </span>
          </div>
          <div
            v-if="project.status === 1"
            class="flex items-center justify-between pt-2 border-t border-gray-200 dark:border-gray-700"
          >
            <span class="text-gray-600 dark:text-gray-400">Còn lại:</span>
            <span
              :class="[
                'font-medium',
                getDaysRemainingClass(project.targetDate)
              ]"
            >
              {{ getDaysRemainingLabel(project.targetDate) }}
            </span>
          </div>
        </div>
      </div>
    </div>

    <div v-if="filteredProjects.length > pageSize" class="card">
      <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <p class="text-sm text-gray-500 dark:text-gray-400">
          Hiển thị {{ (currentPage - 1) * pageSize + 1 }}-{{ Math.min(currentPage * pageSize, filteredProjects.length) }} / {{ filteredProjects.length }} dự án
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

    <div v-if="showCreateModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div class="card w-full max-w-2xl">
        <div class="mb-4 flex items-center justify-between">
          <h3 class="text-lg font-semibold text-gray-900 dark:text-white">Tạo dự án mới</h3>
          <button class="text-gray-400 hover:text-gray-600" @click="closeCreateModal">x</button>
        </div>

        <form class="space-y-4" @submit.prevent="submitCreateProject">
          <div>
            <label class="label">Tên dự án</label>
            <input v-model="createForm.name" class="input" required placeholder="VD: Thi công căn hộ mẫu" />
          </div>

          <div>
            <label class="label">Khách hàng (Client)</label>
            <select v-model="createForm.clientId" class="input" required>
              <option value="">Chọn khách hàng</option>
              <option v-for="client in clients" :key="client.id" :value="client.id">
                {{ client.fullName }} - {{ client.email }}
              </option>
            </select>
          </div>

          <div class="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label class="label">Ngày bắt đầu</label>
              <input v-model="createForm.startDate" type="date" class="input" required />
            </div>
            <div>
              <label class="label">Ngày dự kiến hoàn thành</label>
              <input v-model="createForm.targetDate" type="date" class="input" />
            </div>
          </div>

          <div>
            <label class="label">Tổng ngân sách (VND)</label>
            <input v-model="createForm.totalBudget" type="number" min="1" max="9999999999999999" class="input" required placeholder="100000000" />
          </div>

          <div class="flex justify-end gap-2 pt-2">
            <button type="button" class="btn-secondary" @click="closeCreateModal">Hủy</button>
            <button type="submit" class="btn-primary" :disabled="creating">
              {{ creating ? 'Đang tạo...' : 'Tạo dự án' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>
