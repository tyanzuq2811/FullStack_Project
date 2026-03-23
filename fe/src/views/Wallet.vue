<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useWalletStore } from '../stores/wallet'
import { useAuthStore } from '../stores/auth'
import { useToast } from 'vue-toastification'
import api from '../services/api'
import { usePagination } from '../composables/usePagination'

const walletStore = useWalletStore()
const authStore = useAuthStore()
const toast = useToast()

const showDepositModal = ref(false)
const depositAmount = ref('')
const receiptImageUrl = ref('')
const receiptFileName = ref('')
const selectedProjectId = ref('')
const clientProjects = ref<Array<{ id: string; name: string }>>([])
const isSubmitting = ref(false)

const isClient = computed(() => authStore.primaryRole === 'Client')
const isAccountant = computed(() => authStore.primaryRole === 'Accountant')
const isPM = computed(() => authStore.primaryRole === 'ProjectManager')
const canViewPersonalWallet = computed(() => ['Client', 'Subcontractor'].includes(authStore.primaryRole))
const canViewProjectWallets = computed(() => isAccountant.value || isPM.value || isClient.value)
const hasClientProjects = computed(() => clientProjects.value.length > 0)
const accountantView = ref<'projectWallets' | 'pendingDeposits'>('projectWallets')
const projectWalletLoading = ref(false)
const projectWallets = ref<Array<{
  id: string
  name: string
  totalBudget: number
  walletBalance: number
  status: number
  targetDate?: string
}>>([])
const accountantWalletFilter = ref<'all' | 'critical'>('all')
const accountantWalletSort = ref<'depletionAsc' | 'balanceAsc' | 'balanceDesc'>('depletionAsc')

const totalProjectWalletBalance = computed(() =>
  projectWallets.value.reduce((sum, project) => sum + (project.walletBalance || 0), 0)
)

const totalProjectBudget = computed(() =>
  projectWallets.value.reduce((sum, project) => sum + (project.totalBudget || 0), 0)
)

const projectWalletUtilization = computed(() => {
  if (totalProjectBudget.value <= 0) return 0
  return Math.min(100, Math.max(0, Math.round((totalProjectWalletBalance.value / totalProjectBudget.value) * 100)))
})

const getProjectLiquidityRatio = (project: { totalBudget: number; walletBalance: number }) => {
  if (!project.totalBudget || project.totalBudget <= 0) return 0
  return project.walletBalance / project.totalBudget
}

const isProjectWalletCritical = (project: { totalBudget: number; walletBalance: number }) => {
  return getProjectLiquidityRatio(project) <= 0.2
}

const criticalProjectCount = computed(() =>
  projectWallets.value.filter(project => isProjectWalletCritical(project)).length
)

const getProjectRiskLevel = (project: { totalBudget: number; walletBalance: number }) => {
  const ratio = getProjectLiquidityRatio(project)
  if (ratio <= 0.2) {
    return { label: 'Mức rủi ro quỹ: Cao', className: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-300' }
  }
  if (ratio <= 0.5) {
    return { label: 'Mức rủi ro quỹ: Trung bình', className: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300' }
  }
  return { label: 'Mức rủi ro quỹ: An toàn', className: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-300' }
}

const filteredProjectWallets = computed(() => {
  let items = [...projectWallets.value]

  if (isAccountant.value && accountantWalletFilter.value === 'critical') {
    items = items.filter(project => isProjectWalletCritical(project))
  }

  if (isAccountant.value) {
    items.sort((a, b) => {
      if (accountantWalletSort.value === 'balanceAsc') {
        return a.walletBalance - b.walletBalance
      }
      if (accountantWalletSort.value === 'balanceDesc') {
        return b.walletBalance - a.walletBalance
      }
      return getProjectLiquidityRatio(a) - getProjectLiquidityRatio(b)
    })
  }

  return items
})

const {
  pageSize: projectWalletPageSize,
  currentPage: projectWalletCurrentPage,
  totalPages: projectWalletTotalPages,
  paginatedItems: paginatedProjectWallets,
  pageNumbers: projectWalletPageNumbers,
  setPage: setProjectWalletPage
} = usePagination(filteredProjectWallets, {
  pageSize: 10,
  resetOn: [accountantWalletFilter, accountantWalletSort]
})

const {
  pageSize: pendingDepositsPageSize,
  currentPage: pendingDepositsCurrentPage,
  totalPages: pendingDepositsTotalPages,
  paginatedItems: paginatedPendingDeposits,
  pageNumbers: pendingDepositsPageNumbers,
  setPage: setPendingDepositsPage
} = usePagination(computed(() => walletStore.pendingDeposits), {
  pageSize: 10,
  resetOn: computed(() => walletStore.pendingDeposits.length)
})

const {
  pageSize: transactionsPageSize,
  currentPage: transactionsCurrentPage,
  totalPages: transactionsTotalPages,
  paginatedItems: paginatedTransactions,
  pageNumbers: transactionsPageNumbers,
  setPage: setTransactionsPage
} = usePagination(computed(() => walletStore.transactions), {
  pageSize: 10,
  resetOn: computed(() => walletStore.transactions.length)
})

const getProjectStatusLabel = (status: number) => {
  const labels: Record<number, string> = {
    0: 'Lập kế hoạch',
    1: 'Đang thực hiện',
    2: 'Bàn giao',
    3: 'Hoàn thành'
  }
  return labels[status] || 'N/A'
}

const getProjectStatusBadge = (status: number) => {
  const badges: Record<number, string> = {
    0: 'badge-info',
    1: 'badge-warning',
    2: 'badge-info',
    3: 'badge-success'
  }
  return badges[status] || 'badge-info'
}

const fetchProjectWallets = async () => {
  if (!canViewProjectWallets.value) return

  projectWalletLoading.value = true
  try {
    const endpoint = isAccountant.value ? '/projects' : '/projects/my'
    const response = await api.get(endpoint)
    if (response.data?.success) {
      projectWallets.value = (response.data.data || []).map((project: any) => ({
        id: project.id,
        name: project.name,
        totalBudget: project.totalBudget || 0,
        walletBalance: project.walletBalance || 0,
        status: project.status,
        targetDate: project.targetDate
      }))
    }
  } catch {
    projectWallets.value = []
    toast.error('Không thể tải dữ liệu ví dự án')
  } finally {
    projectWalletLoading.value = false
  }
}

onMounted(async () => {
  if (isAccountant.value) {
    accountantWalletFilter.value = 'critical'
  }

  if (canViewProjectWallets.value) {
    await fetchProjectWallets()
  }

  if (isAccountant.value) {
    await walletStore.fetchPendingDeposits()
    return
  }

  if (isClient.value) {
    try {
      const projectResponse = await api.get('/projects/my')
      if (projectResponse.data?.success) {
        clientProjects.value = (projectResponse.data.data || []).map((project: any) => ({
          id: project.id,
          name: project.name
        }))
        selectedProjectId.value = clientProjects.value[0]?.id || ''
      }
    } catch {
      clientProjects.value = []
    }
  }

  await Promise.all([walletStore.fetchSummary(), walletStore.fetchTransactions()])
})

const handleDeposit = async () => {
  if (!isClient.value) {
    toast.error('Chỉ Khách hàng được tạo lệnh nạp tiền')
    return
  }

  if (!hasClientProjects.value) {
    toast.error('Bạn chưa có dự án nào để nạp tiền')
    return
  }

  const amount = Number(depositAmount.value)
  if (!Number.isFinite(amount) || amount <= 0) {
    toast.error('Số tiền nạp phải lớn hơn 0')
    return
  }

  if (!depositAmount.value || !receiptImageUrl.value || !selectedProjectId.value) {
    toast.error('Vui lòng điền đầy đủ thông tin')
    return
  }

  isSubmitting.value = true
  try {
    const result = await walletStore.requestDeposit(
      amount,
      selectedProjectId.value,
      receiptImageUrl.value
    )

    if (result.success) {
      toast.success('Yêu cầu nạp tiền đã được gửi')
      showDepositModal.value = false
      depositAmount.value = ''
      receiptImageUrl.value = ''
      receiptFileName.value = ''
      selectedProjectId.value = clientProjects.value[0]?.id || ''
    } else {
      toast.error(result.message)
    }
  } finally {
    isSubmitting.value = false
  }
}

const handleReceiptSelected = async (event: Event) => {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) {
    receiptImageUrl.value = ''
    receiptFileName.value = ''
    return
  }

  receiptFileName.value = file.name
  receiptImageUrl.value = file.name
}

const handleApproveDeposit = async (id: string, approved: boolean) => {
  const notes = approved ? '' : prompt('Nhập lý do từ chối:') || ''
  if (!approved && !notes.trim()) {
    toast.error('Cần nhập lý do từ chối')
    return
  }

  const result = await walletStore.approveDeposit(id, approved, notes)
  if (result.success) {
    toast.success(approved ? 'Đã phê duyệt nạp tiền' : 'Đã từ chối yêu cầu nạp tiền')
    await walletStore.fetchPendingDeposits()
  } else {
    toast.error(result.message || 'Không thể xử lý yêu cầu')
  }
}

const getCategoryText = (category: number) => {
  const categories = ['Nạp tiền', 'Thanh toán thầu', 'Mua vật tư', 'Hoàn tiền']
  return categories[category] || 'N/A'
}

const isProjectAllocationTransaction = (tx: { category: number; transType: number; description?: string }) => {
  return tx.category === 0 && tx.transType === 1 && (tx.description || '').includes('Phân bổ vào ngân sách dự án')
}

const getTransactionLabel = (tx: { category: number; transType: number; description?: string }) => {
  if (isProjectAllocationTransaction(tx)) {
    return 'Phân bổ vào ví dự án'
  }
  return getCategoryText(tx.category)
}

const getStatusBadge = (status: number) => {
  const badges: Record<number, string> = {
    0: 'badge-warning',
    1: 'badge-success',
    2: 'badge-danger',
    3: 'badge-info'
  }
  return badges[status] || 'badge-info'
}

const getStatusText = (status: number) => {
  const texts = ['Đang chờ', 'Thành công', 'Thất bại', 'Đã hủy']
  return texts[status] || 'N/A'
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex items-center justify-between">
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">{{ isAccountant ? 'Kiểm soát tài chính' : 'Ví tiền' }}</h1>
      <button v-if="isClient" @click="showDepositModal = true" class="btn-primary" :disabled="!hasClientProjects">
        + Nạp tiền
      </button>
    </div>

    <div v-if="isAccountant" class="card">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Mục kiểm soát</h2>
        <div class="flex flex-wrap gap-2">
          <button
            class="px-4 py-2 rounded-lg border text-sm font-medium transition-colors"
            :class="accountantView === 'projectWallets'
              ? 'bg-primary-600 text-white border-primary-600'
              : 'border-gray-300 text-gray-700 hover:bg-gray-100 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-700'"
            @click="accountantView = 'projectWallets'"
          >
            Giám sát ví dự án
          </button>
          <button
            class="px-4 py-2 rounded-lg border text-sm font-medium transition-colors"
            :class="accountantView === 'pendingDeposits'
              ? 'bg-primary-600 text-white border-primary-600'
              : 'border-gray-300 text-gray-700 hover:bg-gray-100 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-700'"
            @click="accountantView = 'pendingDeposits'"
          >
            Yêu cầu nạp chờ duyệt ({{ walletStore.pendingDeposits.length }})
          </button>
        </div>
      </div>
    </div>

    <div v-if="canViewProjectWallets && (!isAccountant || accountantView === 'projectWallets')" class="card">
      <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div>
          <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Giám sát Ví Dự án</h2>
          <p class="text-sm text-gray-500 dark:text-gray-400">Dành cho PM và Kế toán theo dõi thanh khoản giải ngân theo tiến độ</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <div v-if="isAccountant" class="flex items-center gap-2">
            <select
              v-model="accountantWalletFilter"
              class="input text-sm min-w-52"
            >
              <option value="all">Tất cả ví dự án</option>
              <option value="critical">Ví dự án sắp cạn (<= 20%)</option>
            </select>
            <span class="inline-flex items-center rounded-full bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-300 px-3 py-1 text-xs font-medium">
              {{ criticalProjectCount }} dự án sắp cạn
            </span>
          </div>
          <select
            v-if="isAccountant"
            v-model="accountantWalletSort"
            class="input text-sm min-w-56"
          >
            <option value="depletionAsc">Sắp cạn nhất trước</option>
            <option value="balanceAsc">Số dư tăng dần</option>
            <option value="balanceDesc">Số dư giảm dần</option>
          </select>
          <button
            v-if="isAccountant"
            class="btn-secondary"
            :class="accountantWalletFilter === 'critical' ? 'ring-2 ring-red-400 ring-offset-1' : ''"
            @click="accountantWalletFilter = 'critical'"
          >
            Chỉ xem rủi ro cao
          </button>
          <button
            v-if="isAccountant"
            class="btn-secondary"
            @click="accountantWalletFilter = 'all'"
          >
            Xóa lọc
          </button>
          <button class="btn-secondary" :disabled="projectWalletLoading" @click="fetchProjectWallets">
            {{ projectWalletLoading ? 'Đang tải...' : 'Làm mới' }}
          </button>
        </div>
      </div>

      <div class="mt-4 grid grid-cols-1 md:grid-cols-3 gap-4">
        <div class="rounded-lg border border-gray-200 dark:border-gray-700 p-4">
          <p class="text-xs text-gray-500 dark:text-gray-400">Tổng số dư ví dự án</p>
          <p class="mt-1 text-xl font-bold text-gray-900 dark:text-white">{{ totalProjectWalletBalance.toLocaleString('vi-VN') }} VNĐ</p>
        </div>
        <div class="rounded-lg border border-gray-200 dark:border-gray-700 p-4">
          <p class="text-xs text-gray-500 dark:text-gray-400">Tổng ngân sách</p>
          <p class="mt-1 text-xl font-bold text-gray-900 dark:text-white">{{ totalProjectBudget.toLocaleString('vi-VN') }} VNĐ</p>
        </div>
        <div class="rounded-lg border border-gray-200 dark:border-gray-700 p-4">
          <p class="text-xs text-gray-500 dark:text-gray-400">Tỷ lệ quỹ còn sẵn sàng</p>
          <p class="mt-1 text-xl font-bold text-gray-900 dark:text-white">{{ projectWalletUtilization }}%</p>
          <div class="mt-2 h-2 rounded-full bg-gray-200 dark:bg-gray-700 overflow-hidden">
            <div class="h-2 rounded-full bg-primary-600" :style="{ width: `${projectWalletUtilization}%` }"></div>
          </div>
        </div>
      </div>

      <div v-if="projectWalletLoading" class="mt-4 text-sm text-gray-500 dark:text-gray-400">Đang tải dữ liệu ví dự án...</div>
      <div v-else-if="filteredProjectWallets.length === 0" class="mt-4 text-sm text-gray-500 dark:text-gray-400">Không có dự án phù hợp bộ lọc.</div>
      <div v-else class="mt-4 space-y-3">
        <p v-if="isAccountant" class="text-xs text-gray-500 dark:text-gray-400">
          Đang hiển thị {{ filteredProjectWallets.length }}/{{ projectWallets.length }} dự án
        </p>
        <div
          v-for="project in paginatedProjectWallets"
          :key="project.id"
          class="rounded-lg border border-gray-200 dark:border-gray-700 p-4"
        >
          <div class="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
            <div>
              <p class="font-semibold text-gray-900 dark:text-white">{{ project.name }}</p>
              <p class="text-xs text-gray-500 dark:text-gray-400">Hạn mục tiêu: {{ project.targetDate ? new Date(project.targetDate).toLocaleDateString('vi-VN') : 'Chưa xác định' }}</p>
            </div>
            <span :class="['badge', getProjectStatusBadge(project.status)]">{{ getProjectStatusLabel(project.status) }}</span>
          </div>

          <div class="mt-3 grid grid-cols-1 md:grid-cols-2 gap-3 text-sm">
            <div>
              <p class="text-gray-500 dark:text-gray-400">Ví dự án</p>
              <p class="font-semibold text-gray-900 dark:text-white">{{ project.walletBalance.toLocaleString('vi-VN') }} VNĐ</p>
            </div>
            <div>
              <p class="text-gray-500 dark:text-gray-400">Ngân sách kế hoạch</p>
              <p class="font-semibold text-gray-900 dark:text-white">{{ project.totalBudget.toLocaleString('vi-VN') }} VNĐ</p>
            </div>
          </div>

          <div class="mt-3">
            <span
              :class="[
                'inline-flex items-center rounded-full px-3 py-1 text-xs font-medium',
                getProjectRiskLevel(project).className
              ]"
            >
              {{ getProjectRiskLevel(project).label }}
            </span>
          </div>

          <div class="mt-3">
            <div class="flex items-center justify-between text-xs text-gray-500 dark:text-gray-400 mb-1">
              <span>Tỷ lệ quỹ còn lại</span>
              <span>{{ Math.round(getProjectLiquidityRatio(project) * 100) }}%</span>
            </div>
            <div class="h-2 rounded-full bg-gray-200 dark:bg-gray-700 overflow-hidden">
              <div
                :class="[
                  'h-2 rounded-full',
                  isProjectWalletCritical(project) ? 'bg-red-500' : 'bg-emerald-500'
                ]"
                :style="{ width: `${Math.min(100, Math.max(0, Math.round(getProjectLiquidityRatio(project) * 100)))}%` }"
              ></div>
            </div>
          </div>
        </div>

        <div v-if="filteredProjectWallets.length > projectWalletPageSize" class="pt-1">
          <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <p class="text-sm text-gray-500 dark:text-gray-400">
              Hiển thị {{ (projectWalletCurrentPage - 1) * projectWalletPageSize + 1 }}-{{ Math.min(projectWalletCurrentPage * projectWalletPageSize, filteredProjectWallets.length) }} / {{ filteredProjectWallets.length }} dự án
            </p>
            <div class="flex items-center gap-2 flex-wrap">
              <button class="btn-secondary px-3 py-1.5 text-sm" :disabled="projectWalletCurrentPage === 1" @click="setProjectWalletPage(projectWalletCurrentPage - 1)">
                Trước
              </button>
              <button
                v-for="page in projectWalletPageNumbers"
                :key="page"
                class="px-3 py-1.5 text-sm rounded-lg border transition-colors"
                :class="page === projectWalletCurrentPage
                  ? 'bg-primary-600 text-white border-primary-600'
                  : 'border-gray-300 text-gray-700 hover:bg-gray-100 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-700'"
                @click="setProjectWalletPage(page)"
              >
                {{ page }}
              </button>
              <button class="btn-secondary px-3 py-1.5 text-sm" :disabled="projectWalletCurrentPage === projectWalletTotalPages" @click="setProjectWalletPage(projectWalletCurrentPage + 1)">
                Sau
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div v-if="isAccountant && accountantView === 'pendingDeposits'" class="card">
      <h2 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Yêu cầu nạp tiền chờ kiểm duyệt</h2>

      <div v-if="walletStore.pendingDeposits.length === 0" class="text-center py-8 text-gray-500 dark:text-gray-400">
        Không có yêu cầu nạp tiền đang chờ
      </div>

      <div v-else class="space-y-3">
        <div
          v-for="tx in paginatedPendingDeposits"
          :key="tx.id"
          class="rounded-lg border border-gray-200 p-4 dark:border-gray-700"
        >
          <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <p class="font-medium text-gray-900 dark:text-white">Yêu cầu nạp: {{ tx.amount.toLocaleString() }} VNĐ</p>
              <p class="text-sm text-gray-500 dark:text-gray-400">Thời gian: {{ new Date(tx.createdAt).toLocaleString('vi-VN') }}</p>
              <p class="text-sm text-gray-500 dark:text-gray-400">Biên lai: {{ tx.refId || 'N/A' }}</p>
            </div>
            <div class="flex gap-2">
              <button class="btn-secondary" @click="handleApproveDeposit(tx.id, false)">Từ chối</button>
              <button class="btn-primary" @click="handleApproveDeposit(tx.id, true)">Phê duyệt</button>
            </div>
          </div>
        </div>

        <div v-if="walletStore.pendingDeposits.length > pendingDepositsPageSize" class="pt-1">
          <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <p class="text-sm text-gray-500 dark:text-gray-400">
              Hiển thị {{ (pendingDepositsCurrentPage - 1) * pendingDepositsPageSize + 1 }}-{{ Math.min(pendingDepositsCurrentPage * pendingDepositsPageSize, walletStore.pendingDeposits.length) }} / {{ walletStore.pendingDeposits.length }} yêu cầu
            </p>
            <div class="flex items-center gap-2 flex-wrap">
              <button class="btn-secondary px-3 py-1.5 text-sm" :disabled="pendingDepositsCurrentPage === 1" @click="setPendingDepositsPage(pendingDepositsCurrentPage - 1)">
                Trước
              </button>
              <button
                v-for="page in pendingDepositsPageNumbers"
                :key="page"
                class="px-3 py-1.5 text-sm rounded-lg border transition-colors"
                :class="page === pendingDepositsCurrentPage
                  ? 'bg-primary-600 text-white border-primary-600'
                  : 'border-gray-300 text-gray-700 hover:bg-gray-100 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-700'"
                @click="setPendingDepositsPage(page)"
              >
                {{ page }}
              </button>
              <button class="btn-secondary px-3 py-1.5 text-sm" :disabled="pendingDepositsCurrentPage === pendingDepositsTotalPages" @click="setPendingDepositsPage(pendingDepositsCurrentPage + 1)">
                Sau
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Balance Card -->
    <div v-if="canViewPersonalWallet" class="card bg-gradient-to-br from-primary-500 to-primary-700 text-white">
      <p class="text-primary-100 text-sm">Số dư hiện tại</p>
      <p class="text-4xl font-bold mt-1">
        {{ walletStore.summary?.balance?.toLocaleString() || 0 }}
        <span class="text-lg font-normal">VNĐ</span>
      </p>
      <div class="flex gap-8 mt-4 text-sm">
        <div>
          <p class="text-primary-200">Tổng nạp</p>
          <p class="font-semibold">{{ walletStore.summary?.totalDeposits?.toLocaleString() || 0 }} VNĐ</p>
        </div>
        <div>
          <p class="text-primary-200">Tổng chi</p>
          <p class="font-semibold">{{ walletStore.summary?.totalWithdrawals?.toLocaleString() || 0 }} VNĐ</p>
        </div>
      </div>
    </div>

    <!-- Transactions -->
    <div v-if="canViewPersonalWallet" class="card">
      <h2 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Lịch sử giao dịch</h2>

      <div v-if="walletStore.isLoading" class="space-y-4">
        <div v-for="i in 5" :key="i" class="flex items-center gap-4">
          <div class="skeleton w-10 h-10 rounded-full"></div>
          <div class="flex-1 space-y-2">
            <div class="skeleton h-4 w-1/3"></div>
            <div class="skeleton h-3 w-1/4"></div>
          </div>
        </div>
      </div>

      <div v-else-if="walletStore.transactions.length === 0" class="text-center py-8 text-gray-500 dark:text-gray-400">
        Chưa có giao dịch nào
      </div>

      <div v-else class="space-y-3">
        <div
          v-for="tx in paginatedTransactions"
          :key="tx.id"
          class="flex items-center gap-4 p-3 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700/50"
        >
          <div :class="[
            'w-10 h-10 rounded-full flex items-center justify-center',
            tx.transType === 0
              ? 'bg-green-100 dark:bg-green-900/30'
              : (isProjectAllocationTransaction(tx) ? 'bg-blue-100 dark:bg-blue-900/30' : 'bg-red-100 dark:bg-red-900/30')
          ]">
            <svg :class="[
              'w-5 h-5',
              tx.transType === 0
                ? 'text-green-600 dark:text-green-400'
                : (isProjectAllocationTransaction(tx) ? 'text-blue-600 dark:text-blue-400' : 'text-red-600 dark:text-red-400')
            ]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path v-if="tx.transType === 0" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8l-8-8-8 8" />
              <path v-else stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 20V4m-8 8l8 8 8-8" />
            </svg>
          </div>
          <div class="flex-1">
            <p class="font-medium text-gray-900 dark:text-white">{{ getTransactionLabel(tx) }}</p>
            <p class="text-sm text-gray-500 dark:text-gray-400">
              {{ new Date(tx.createdAt).toLocaleString('vi-VN') }}
            </p>
          </div>
          <div class="text-right">
            <p :class="[
              'font-semibold',
              tx.transType === 0
                ? 'text-green-600 dark:text-green-400'
                : (isProjectAllocationTransaction(tx) ? 'text-blue-600 dark:text-blue-400' : 'text-red-600 dark:text-red-400')
            ]">
              {{ tx.transType === 0 ? '+' : '-' }}{{ tx.amount.toLocaleString() }} VNĐ
            </p>
            <span :class="['badge', getStatusBadge(tx.status)]">{{ getStatusText(tx.status) }}</span>
          </div>
        </div>

        <div v-if="walletStore.transactions.length > transactionsPageSize" class="pt-1">
          <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <p class="text-sm text-gray-500 dark:text-gray-400">
              Hiển thị {{ (transactionsCurrentPage - 1) * transactionsPageSize + 1 }}-{{ Math.min(transactionsCurrentPage * transactionsPageSize, walletStore.transactions.length) }} / {{ walletStore.transactions.length }} giao dịch
            </p>
            <div class="flex items-center gap-2 flex-wrap">
              <button class="btn-secondary px-3 py-1.5 text-sm" :disabled="transactionsCurrentPage === 1" @click="setTransactionsPage(transactionsCurrentPage - 1)">
                Trước
              </button>
              <button
                v-for="page in transactionsPageNumbers"
                :key="page"
                class="px-3 py-1.5 text-sm rounded-lg border transition-colors"
                :class="page === transactionsCurrentPage
                  ? 'bg-primary-600 text-white border-primary-600'
                  : 'border-gray-300 text-gray-700 hover:bg-gray-100 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-700'"
                @click="setTransactionsPage(page)"
              >
                {{ page }}
              </button>
              <button class="btn-secondary px-3 py-1.5 text-sm" :disabled="transactionsCurrentPage === transactionsTotalPages" @click="setTransactionsPage(transactionsCurrentPage + 1)">
                Sau
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Deposit Modal -->
    <div v-if="isClient && showDepositModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div class="card w-full max-w-md mx-4 animate-slide-up">
        <div class="flex items-center justify-between mb-4">
          <h3 class="text-lg font-semibold text-gray-900 dark:text-white">Nạp tiền</h3>
          <button @click="showDepositModal = false" class="text-gray-400 hover:text-gray-600">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <form @submit.prevent="handleDeposit" class="space-y-4">
          <div>
            <label class="label">Dự án nhận phân bổ</label>
            <select v-model="selectedProjectId" class="input" required>
              <option value="" disabled>{{ hasClientProjects ? 'Chọn dự án' : 'Chưa có dự án' }}</option>
              <option v-for="project in clientProjects" :key="project.id" :value="project.id">
                {{ project.name }}
              </option>
            </select>
          </div>
          <div>
            <label class="label">Số tiền (VNĐ)</label>
            <input v-model="depositAmount" type="number" class="input" placeholder="100000" required />
          </div>
          <div>
            <label class="label">Ảnh biên lai</label>
            <input type="file" accept="image/*" class="input" @change="handleReceiptSelected" required />
            <p v-if="receiptFileName" class="mt-1 text-xs text-gray-500 dark:text-gray-400">{{ receiptFileName }}</p>
          </div>
          <div class="flex gap-3">
            <button type="button" @click="showDepositModal = false" class="flex-1 btn-secondary">
              Hủy
            </button>
            <button type="submit" :disabled="isSubmitting || !hasClientProjects || !selectedProjectId" class="flex-1 btn-primary">
              {{ isSubmitting ? 'Đang gửi...' : 'Gửi yêu cầu' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>
