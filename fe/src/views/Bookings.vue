<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import api from '../services/api'
import { useToast } from 'vue-toastification'
import CalendarView from '../components/CalendarView.vue'
import { PhListBullets, PhCalendar } from '@phosphor-icons/vue'
import { useAuthStore } from '../stores/auth'
import { usePagination } from '../composables/usePagination'

const toast = useToast()
const authStore = useAuthStore()

const viewMode = ref<'list' | 'calendar'>('list')
const canCreateBooking = computed(() => {
  const roles = authStore.user?.roles || []
  return roles.includes('ProjectManager') || roles.includes('Subcontractor')
})

interface Booking {
  id: string
  resourceName: string
  startTime: string
  endTime: string
  status: string
  price: number
  memberName: string
  projectId?: string
}

interface Resource {
  id: number
  name: string
  description: string
  hourlyRate: number
  isActive: boolean
}

interface Project {
  id: string
  name: string
}

const bookings = ref<Booking[]>([])
const resources = ref<Resource[]>([])
const projects = ref<Project[]>([])
const loading = ref(true)
const showModal = ref(false)
const selectedStatus = ref('all')
const confirmingPaymentId = ref<string | null>(null)
const showPaymentModal = ref(false)
const bookingToConfirm = ref<Booking | null>(null)

const newBooking = ref({
  resourceId: '',
  projectId: '',
  startTime: '',
  endTime: '',
  notes: ''
})

const recurring = ref({
  enabled: false,
  endDate: '',
  daysOfWeek: [] as string[],
  conflictMode: 0
})

const dayOptions = [
  { label: 'Thứ 2', value: 'Monday' },
  { label: 'Thứ 3', value: 'Tuesday' },
  { label: 'Thứ 4', value: 'Wednesday' },
  { label: 'Thứ 5', value: 'Thursday' },
  { label: 'Thứ 6', value: 'Friday' },
  { label: 'Thứ 7', value: 'Saturday' },
  { label: 'Chủ nhật', value: 'Sunday' }
]

const statusOptions = [
  { value: 'all', label: 'Tất cả' },
  { value: 'PendingPayment', label: 'Chờ thanh toán' },
  { value: 'Confirmed', label: 'Đã xác nhận' },
  { value: 'Completed', label: 'Hoàn thành' },
  { value: 'Cancelled', label: 'Đã hủy' }
]

const filteredBookings = computed(() => {
  if (selectedStatus.value === 'all') {
    return bookings.value
  }
  return bookings.value.filter(b => b.status === selectedStatus.value)
})

const {
  pageSize,
  currentPage,
  totalPages,
  paginatedItems: paginatedBookings,
  pageNumbers,
  setPage
} = usePagination(filteredBookings, {
  pageSize: 10,
  resetOn: selectedStatus
})

const getStatusColor = (status: string) => {
  const colors: Record<string, string> = {
    PendingPayment: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-300',
    Confirmed: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300',
    Completed: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-300',
    Cancelled: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-300'
  }
  return colors[status] || 'bg-gray-100 text-gray-800'
}

const getStatusLabel = (status: string) => {
  const labels: Record<string, string> = {
    PendingPayment: 'Chờ thanh toán',
    Confirmed: 'Đã xác nhận',
    Completed: 'Hoàn thành',
    Cancelled: 'Đã hủy'
  }
  return labels[status] || status
}

const formatCurrency = (amount: number) => {
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND'
  }).format(amount)
}

const formatDateTime = (dateString: string) => {
  return new Date(dateString).toLocaleString('vi-VN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const fetchBookings = async () => {
  try {
    loading.value = true
    const response = await api.get('/bookings')
    // Extract bookings from ApiResponse wrapper
    if (response.data?.success && response.data?.data) {
      bookings.value = response.data.data
    } else {
      bookings.value = []
      toast.error(response.data?.message || 'Không thể tải danh sách đặt lịch')
    }
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Không thể tải danh sách đặt lịch')
  } finally {
    loading.value = false
  }
}

const fetchResources = async () => {
  try {
    const response = await api.get('/bookings/resources')
    // Extract resources from ApiResponse wrapper
    if (response.data?.success && response.data?.data) {
      resources.value = response.data.data.filter((r: Resource) => r.isActive)
    } else {
      resources.value = []
    }
  } catch (error: any) {
    toast.error('Không thể tải danh sách tài nguyên')
  }
}

const fetchProjects = async () => {
  try {
    const response = await api.get('/projects/my')
    // Extract projects from ApiResponse wrapper
    if (response.data?.success && response.data?.data) {
      projects.value = response.data.data.map((p: any) => ({
        id: p.id,
        name: p.name
      }))
    } else {
      projects.value = []
    }
  } catch (error: any) {
    toast.error('Không thể tải danh sách dự án')
  }
}

const openModal = () => {
  if (!canCreateBooking.value) {
    toast.error('Bạn không có quyền đặt lịch tài nguyên')
    return
  }

  showModal.value = true
  recurring.value.enabled = false
  recurring.value.endDate = ''
  recurring.value.daysOfWeek = []
  recurring.value.conflictMode = 0
  if (resources.value.length === 0) {
    fetchResources()
  }
}

const closeModal = () => {
  showModal.value = false
  newBooking.value = {
    resourceId: '',
    projectId: '',
    startTime: '',
    endTime: '',
    notes: ''
  }
  recurring.value.enabled = false
  recurring.value.endDate = ''
  recurring.value.daysOfWeek = []
  recurring.value.conflictMode = 0
}

const toTimeSpan = (date: Date) => {
  const h = String(date.getHours()).padStart(2, '0')
  const m = String(date.getMinutes()).padStart(2, '0')
  return `${h}:${m}:00`
}

const toDurationTimeSpan = (start: Date, end: Date) => {
  const diffMs = end.getTime() - start.getTime()
  const diffMinutes = Math.floor(diffMs / 60000)
  const hours = String(Math.floor(diffMinutes / 60)).padStart(2, '0')
  const minutes = String(diffMinutes % 60).padStart(2, '0')
  return `${hours}:${minutes}:00`
}

const getDayOfWeekName = (date: Date) => {
  const names = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
  return names[date.getDay()]
}

const createBooking = async () => {
  if (!canCreateBooking.value) {
    toast.error('Bạn không có quyền đặt lịch tài nguyên')
    return
  }

  if (!newBooking.value.projectId) {
    toast.error('Vui lòng chọn dự án')
    return
  }

  try {
    if (!recurring.value.enabled) {
      await api.post('/bookings', {
        resourceId: Number(newBooking.value.resourceId),
        projectId: newBooking.value.projectId,
        startTime: newBooking.value.startTime,
        endTime: newBooking.value.endTime
      })
      toast.success('Đặt lịch thành công!')
    } else {
      const startDateTime = new Date(newBooking.value.startTime)
      const endDateTime = new Date(newBooking.value.endTime)
      const recurringEndDate = recurring.value.endDate ? new Date(recurring.value.endDate) : null

      if (!recurringEndDate) {
        toast.error('Vui lòng chọn ngày kết thúc chuỗi đặt lịch')
        return
      }

      if (endDateTime <= startDateTime) {
        toast.error('Thời gian kết thúc phải sau thời gian bắt đầu')
        return
      }

      const selectedDays = recurring.value.daysOfWeek.length > 0
        ? recurring.value.daysOfWeek
        : [getDayOfWeekName(startDateTime)]

      const payload = {
        resourceId: Number(newBooking.value.resourceId),
        projectId: newBooking.value.projectId,
        startDate: startDateTime.toISOString(),
        endDate: recurringEndDate.toISOString(),
        startTime: toTimeSpan(startDateTime),
        duration: toDurationTimeSpan(startDateTime, endDateTime),
        daysOfWeek: selectedDays,
        conflictMode: recurring.value.conflictMode
      }

      const response = await api.post('/bookings/recurring', payload)
      const result = response.data?.data
      const bookedCount = result?.successfulBookings?.length || 0
      const conflictCount = result?.conflicts?.length || 0
      toast.success(`Đặt lịch định kỳ: ${bookedCount} thành công, ${conflictCount} xung đột`)
    }

    closeModal()
    fetchBookings()
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Không thể tạo đặt lịch')
  }
}

const cancelBooking = async (id: string) => {
  if (!confirm('Bạn có chắc muốn hủy đặt lịch này?')) return

  try {
    await api.delete('/bookings', {
      data: {
        bookingId: id,
        cancelEntireGroup: false
      }
    })
    toast.success('Đã hủy đặt lịch')
    fetchBookings()
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Không thể hủy đặt lịch')
  }
}

const openPaymentModal = (booking: Booking) => {
  if (booking.status !== 'PendingPayment') return
  bookingToConfirm.value = booking
  showPaymentModal.value = true
}

const closePaymentModal = () => {
  showPaymentModal.value = false
  bookingToConfirm.value = null
}

const confirmBookingPayment = async () => {
  if (!bookingToConfirm.value || bookingToConfirm.value.status !== 'PendingPayment') return

  confirmingPaymentId.value = bookingToConfirm.value.id
  try {
    const response = await api.post(`/bookings/${bookingToConfirm.value.id}/confirm-payment`)
    if (response.data?.success) {
      toast.success('Thanh toán thành công, lịch đã được xác nhận')
      closePaymentModal()
      await fetchBookings()
      return
    }
    toast.error(response.data?.message || 'Không thể thanh toán lịch đặt')
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Không thể thanh toán lịch đặt')
  } finally {
    confirmingPaymentId.value = null
  }
}

const viewBooking = (booking: Booking) => {
  // Show booking details - for now just show an alert
  // Can be enhanced with a modal later
  const details = `
    Tài nguyên: ${booking.resourceName}
    Người đặt: ${booking.memberName}
    Trạng thái: ${getStatusLabel(booking.status)}
    Bắt đầu: ${formatDateTime(booking.startTime)}
    Kết thúc: ${formatDateTime(booking.endTime)}
    Giá: ${formatCurrency(booking.price)}
  `
  alert(details)
}

onMounted(() => {
  fetchBookings()
  fetchResources()
  fetchProjects()
  window.addEventListener('bookingStatusChanged', fetchBookings as EventListener)
})

onUnmounted(() => {
  window.removeEventListener('bookingStatusChanged', fetchBookings as EventListener)
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Đặt lịch tài nguyên</h1>
        <p class="text-gray-600 dark:text-gray-400 mt-1">
          Quản lý đặt lịch sử dụng thiết bị, nhân công và tài nguyên
        </p>
      </div>
      <button v-if="canCreateBooking" @click="openModal" class="btn btn-primary">
        <svg class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
        </svg>
        Đặt lịch mới
      </button>
    </div>

    <!-- Filter & View Toggle -->
    <div class="card">
      <div class="flex items-center justify-between">
        <select v-model="selectedStatus" class="input md:w-48">
          <option v-for="option in statusOptions" :key="option.value" :value="option.value">
            {{ option.label }}
          </option>
        </select>

        <!-- View Toggle -->
        <div class="flex gap-2">
          <button
            @click="viewMode = 'list'"
            :class="[
              'p-2 rounded-lg transition-colors',
              viewMode === 'list'
                ? 'bg-primary-100 dark:bg-primary-900 text-primary-700 dark:text-primary-300'
                : 'hover:bg-gray-100 dark:hover:bg-gray-700 text-gray-600 dark:text-gray-400'
            ]"
          >
            <PhListBullets :size="20" />
          </button>
          <button
            @click="viewMode = 'calendar'"
            :class="[
              'p-2 rounded-lg transition-colors',
              viewMode === 'calendar'
                ? 'bg-primary-100 dark:bg-primary-900 text-primary-700 dark:text-primary-300'
                : 'hover:bg-gray-100 dark:hover:bg-gray-700 text-gray-600 dark:text-gray-400'
            ]"
          >
            <PhCalendar :size="20" />
          </button>
        </div>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex justify-center py-12">
      <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-500"></div>
    </div>

    <!-- Calendar View -->
    <CalendarView
      v-else-if="viewMode === 'calendar'"
      :bookings="filteredBookings"
      @view-booking="viewBooking"
    />

    <!-- Empty State -->
    <div v-else-if="filteredBookings.length === 0" class="card text-center py-12">
      <svg class="mx-auto h-12 w-12 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/>
      </svg>
      <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">Chưa có đặt lịch</h3>
      <p class="mt-2 text-gray-600 dark:text-gray-400">Bắt đầu bằng cách đặt lịch tài nguyên mới</p>
    </div>

    <!-- Bookings List -->
    <div v-else class="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <div
        v-for="booking in paginatedBookings"
        :key="booking.id"
        class="card hover:shadow-lg transition-shadow"
      >
        <!-- Header -->
        <div class="flex items-start justify-between mb-4">
          <div class="flex-1">
            <h3 class="font-semibold text-gray-900 dark:text-white">
              {{ booking.resourceName }}
            </h3>
            <p class="text-sm text-gray-600 dark:text-gray-400 mt-1">
              Người đặt: {{ booking.memberName }}
            </p>
          </div>
          <span :class="['px-3 py-1 text-xs font-medium rounded-full', getStatusColor(booking.status)]">
            {{ getStatusLabel(booking.status) }}
          </span>
        </div>

        <!-- Time Info -->
        <div class="space-y-3 mb-4">
          <div class="flex items-center text-sm text-gray-600 dark:text-gray-400">
            <svg class="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/>
            </svg>
            <div>
              <div class="font-medium text-gray-900 dark:text-white">Bắt đầu</div>
              <div>{{ formatDateTime(booking.startTime) }}</div>
            </div>
          </div>
          <div class="flex items-center text-sm text-gray-600 dark:text-gray-400">
            <svg class="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/>
            </svg>
            <div>
              <div class="font-medium text-gray-900 dark:text-white">Kết thúc</div>
              <div>{{ formatDateTime(booking.endTime) }}</div>
            </div>
          </div>
        </div>

        <!-- Price & Actions -->
        <div class="flex items-center justify-between pt-4 border-t border-gray-200 dark:border-gray-700">
          <div>
            <div class="text-sm text-gray-600 dark:text-gray-400">Giá thuê</div>
            <div class="text-lg font-bold text-gray-900 dark:text-white">
              {{ formatCurrency(booking.price) }}
            </div>
            <p v-if="booking.status === 'PendingPayment'" class="mt-1 text-xs text-amber-600 dark:text-amber-300">
              Lịch sẽ tự hủy nếu chưa thanh toán trong vòng 15 phút.
            </p>
          </div>
          <div class="flex gap-2">
            <button
              v-if="booking.status === 'PendingPayment'"
              @click="openPaymentModal(booking)"
              class="btn btn-primary text-sm"
            >
              Thanh toán ngay
            </button>
            <button
              v-if="booking.status === 'PendingPayment' || booking.status === 'Confirmed'"
              @click="cancelBooking(booking.id)"
              class="btn btn-secondary text-sm"
              :disabled="confirmingPaymentId === booking.id"
            >
              Hủy
            </button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="viewMode === 'list' && filteredBookings.length > pageSize" class="card">
      <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <p class="text-sm text-gray-500 dark:text-gray-400">
          Hiển thị {{ (currentPage - 1) * pageSize + 1 }}-{{ Math.min(currentPage * pageSize, filteredBookings.length) }} / {{ filteredBookings.length }} lịch đặt
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

    <!-- Create Booking Modal -->
    <div
      v-if="showModal"
      class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4"
      @click.self="closeModal"
    >
      <div class="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-md w-full p-6" v-motion-pop>
        <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">Đặt lịch mới</h2>

        <form @submit.prevent="createBooking" class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
              Tài nguyên
            </label>
            <select v-model="newBooking.resourceId" required class="input w-full">
              <option value="">Chọn tài nguyên</option>
              <option v-for="resource in resources" :key="resource.id" :value="resource.id">
                {{ resource.name }} - {{ formatCurrency(resource.hourlyRate) }}/giờ
              </option>
            </select>
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
              Dự án
            </label>
            <select v-model="newBooking.projectId" required class="input w-full">
              <option value="">Chọn dự án</option>
              <option v-for="project in projects" :key="project.id" :value="project.id">
                {{ project.name }}
              </option>
            </select>
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
              Thời gian bắt đầu
            </label>
            <input
              v-model="newBooking.startTime"
              type="datetime-local"
              required
              class="input w-full"
            />
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
              Thời gian kết thúc
            </label>
            <input
              v-model="newBooking.endTime"
              type="datetime-local"
              required
              class="input w-full"
            />
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
              Ghi chú (tùy chọn)
            </label>
            <textarea
              v-model="newBooking.notes"
              rows="3"
              class="input w-full"
              placeholder="Mô tả công việc..."
            ></textarea>
          </div>

          <div class="rounded-lg border border-gray-200 dark:border-gray-700 p-3">
            <label class="inline-flex items-center gap-2 text-sm font-medium text-gray-700 dark:text-gray-300">
              <input v-model="recurring.enabled" type="checkbox" class="rounded border-gray-300" />
              Đặt lịch định kỳ
            </label>

            <div v-if="recurring.enabled" class="mt-3 space-y-3">
              <div>
                <label class="block text-xs font-medium text-gray-600 dark:text-gray-400 mb-1">Ngày kết thúc chuỗi</label>
                <input v-model="recurring.endDate" type="date" class="input w-full" />
              </div>

              <div>
                <label class="block text-xs font-medium text-gray-600 dark:text-gray-400 mb-1">Lặp theo thứ</label>
                <div class="grid grid-cols-2 gap-2">
                  <label v-for="day in dayOptions" :key="day.value" class="inline-flex items-center gap-2 text-xs text-gray-700 dark:text-gray-300">
                    <input v-model="recurring.daysOfWeek" :value="day.value" type="checkbox" class="rounded border-gray-300" />
                    {{ day.label }}
                  </label>
                </div>
              </div>

              <div>
                <label class="block text-xs font-medium text-gray-600 dark:text-gray-400 mb-1">Xử lý xung đột</label>
                <select v-model.number="recurring.conflictMode" class="input w-full">
                  <option :value="0">Bỏ qua ngày bị trùng</option>
                  <option :value="1">Hủy toàn bộ chuỗi nếu có ngày trùng</option>
                </select>
              </div>
            </div>
          </div>

          <div class="flex gap-3 pt-4">
            <button type="button" @click="closeModal" class="btn btn-secondary flex-1">
              Hủy
            </button>
            <button type="submit" class="btn btn-primary flex-1">
              Xác nhận đặt lịch
            </button>
          </div>
        </form>
      </div>
    </div>

    <div
      v-if="showPaymentModal && bookingToConfirm"
      class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4"
      @click.self="closePaymentModal"
    >
      <div class="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-md w-full p-6">
        <h2 class="text-xl font-bold text-gray-900 dark:text-white">Xác nhận thanh toán lịch đặt</h2>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">Vui lòng kiểm tra thông tin trước khi thanh toán</p>

        <div class="mt-4 space-y-3 rounded-lg border border-gray-200 dark:border-gray-700 p-4">
          <div>
            <p class="text-xs text-gray-500 dark:text-gray-400">Tài nguyên</p>
            <p class="font-semibold text-gray-900 dark:text-white">{{ bookingToConfirm.resourceName }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-500 dark:text-gray-400">Thời gian bắt đầu</p>
            <p class="font-medium text-gray-900 dark:text-white">{{ formatDateTime(bookingToConfirm.startTime) }}</p>
          </div>
          <div>
            <p class="text-xs text-gray-500 dark:text-gray-400">Thời gian kết thúc</p>
            <p class="font-medium text-gray-900 dark:text-white">{{ formatDateTime(bookingToConfirm.endTime) }}</p>
          </div>
          <div class="pt-2 border-t border-gray-200 dark:border-gray-700">
            <p class="text-xs text-gray-500 dark:text-gray-400">Tổng tiền cần thanh toán</p>
            <p class="text-lg font-bold text-primary-600 dark:text-primary-400">{{ formatCurrency(bookingToConfirm.price) }}</p>
          </div>
        </div>

        <p class="mt-3 text-xs text-amber-600 dark:text-amber-300">Nếu không thanh toán trong vòng 15 phút, lịch đặt sẽ tự động bị hủy.</p>

        <div class="mt-5 flex gap-3">
          <button type="button" class="btn btn-secondary flex-1" @click="closePaymentModal" :disabled="confirmingPaymentId === bookingToConfirm.id">
            Hủy
          </button>
          <button type="button" class="btn btn-primary flex-1" @click="confirmBookingPayment" :disabled="confirmingPaymentId === bookingToConfirm.id">
            {{ confirmingPaymentId === bookingToConfirm.id ? 'Đang thanh toán...' : 'Xác nhận trả tiền' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
