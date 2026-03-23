<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api } from '../../services/api'
import { useWalletStore } from '../../stores/wallet'
import type { Project } from '../../types'

interface NewsItem {
  id: number
  title: string
  content: string
  updatedAt: string
}

const walletStore = useWalletStore()
const isLoading = ref(true)
const projects = ref<Project[]>([])
const news = ref<NewsItem[]>([])

const totalBudget = computed(() => projects.value.reduce((sum, p) => sum + p.totalBudget, 0))
const totalActual = computed(() => projects.value.reduce((sum, p) => sum + p.spentBudget, 0))
const variance = computed(() => totalBudget.value - totalActual.value)

const budgetPct = computed(() => {
  if (totalBudget.value <= 0) return 0
  return Math.min(100, Math.round((totalActual.value / totalBudget.value) * 100))
})

const aiRedFlags = computed(() => {
  const overspentProjects = projects.value
    .filter(project => project.spentBudget > project.totalBudget)
    .map(project => ({
      id: `project-${project.id}`,
      title: `Dự án ${project.name} đã vượt ngân sách`,
      detail: `${new Intl.NumberFormat('vi-VN').format(project.spentBudget - project.totalBudget)} VND vượt hạn`,
      at: new Date().toISOString()
    }))

  const newsFlags = news.value
    .filter(item => /ai|hóa đơn|hoa don|vượt|vuot|cảnh báo|canh bao|budget/i.test(`${item.title} ${item.content}`))
    .slice(0, 5)
    .map(item => ({
      id: `news-${item.id}`,
      title: item.title,
      detail: item.content.slice(0, 120),
      at: item.updatedAt
    }))

  return [...overspentProjects, ...newsFlags]
    .sort((a, b) => new Date(b.at).getTime() - new Date(a.at).getTime())
    .slice(0, 6)
})

const budgetBars = computed(() => {
  const maxValue = Math.max(totalBudget.value, totalActual.value, 1)
  return [
    {
      key: 'budget',
      label: 'Dự toán',
      amount: totalBudget.value,
      color: 'bg-blue-500',
      height: Math.max(24, Math.round((totalBudget.value / maxValue) * 180))
    },
    {
      key: 'actual',
      label: 'Thực tế',
      amount: totalActual.value,
      color: totalActual.value > totalBudget.value ? 'bg-red-500' : 'bg-emerald-500',
      height: Math.max(24, Math.round((totalActual.value / maxValue) * 180))
    }
  ]
})

const projectRiskCounts = computed(() => {
  const counts = {
    critical: 0,
    warning: 0,
    safe: 0
  }

  for (const project of projects.value) {
    const ratio = project.totalBudget > 0 ? project.spentBudget / project.totalBudget : 0
    if (ratio >= 0.9) counts.critical += 1
    else if (ratio >= 0.7) counts.warning += 1
    else counts.safe += 1
  }

  return counts
})

const totalRiskProjects = computed(() => {
  return projectRiskCounts.value.critical + projectRiskCounts.value.warning + projectRiskCounts.value.safe
})

const riskSegments = computed(() => {
  const total = totalRiskProjects.value || 1
  return [
    {
      key: 'critical',
      label: 'Rủi ro cao',
      value: projectRiskCounts.value.critical,
      pct: Math.round((projectRiskCounts.value.critical / total) * 100),
      color: 'bg-red-600',
      textClass: 'text-red-700 dark:text-red-300',
      cardClass: 'border-red-300 dark:border-red-900 bg-red-50/80 dark:bg-red-950/20'
    },
    {
      key: 'warning',
      label: 'Cần theo dõi',
      value: projectRiskCounts.value.warning,
      pct: Math.round((projectRiskCounts.value.warning / total) * 100),
      color: 'bg-amber-500',
      textClass: 'text-amber-700 dark:text-amber-300',
      cardClass: 'border-amber-300 dark:border-amber-900 bg-amber-50/80 dark:bg-amber-950/20'
    },
    {
      key: 'safe',
      label: 'An toàn',
      value: projectRiskCounts.value.safe,
      pct: Math.round((projectRiskCounts.value.safe / total) * 100),
      color: 'bg-emerald-500',
      textClass: 'text-emerald-700 dark:text-emerald-300',
      cardClass: 'border-emerald-300 dark:border-emerald-900 bg-emerald-50/80 dark:bg-emerald-950/20'
    }
  ]
})

const riskDonutStyle = computed(() => {
  const redFlags = aiRedFlags.value.length
  const pending = walletStore.pendingDeposits.length
  const total = redFlags + pending || 1
  const redPct = (redFlags / total) * 100

  return {
    background: `conic-gradient(#dc2626 0 ${redPct}%, #f59e0b ${redPct}% 100%)`
  }
})

const approve = async (id: string, approved: boolean) => {
  const notes = approved ? '' : prompt('Nhập lý do từ chối') || ''
  if (!approved && !notes.trim()) return

  const result = await walletStore.approveDeposit(id, approved, notes)
  if (result.success) {
    await walletStore.fetchPendingDeposits()
  }
}

onMounted(async () => {
  try {
    const [projectsRes, newsRes] = await Promise.all([
      api.get('/projects'),
      api.get('/news')
    ])

    if (projectsRes.data?.success) projects.value = projectsRes.data.data || []
    if (newsRes.data?.success) news.value = newsRes.data.data || []

    await walletStore.fetchPendingDeposits()
  } finally {
    isLoading.value = false
  }
})
</script>

<template>
  <div class="space-y-6">
    <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
      <div class="card">
        <p class="text-sm text-gray-500 dark:text-gray-400">Tổng dự toán công ty</p>
        <p class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">{{ totalBudget.toLocaleString('vi-VN') }} VND</p>
      </div>
      <div class="card">
        <p class="text-sm text-gray-500 dark:text-gray-400">Tổng chi thực tế</p>
        <p class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">{{ totalActual.toLocaleString('vi-VN') }} VND</p>
      </div>
      <div class="card">
        <p class="text-sm text-gray-500 dark:text-gray-400">Chênh lệch</p>
        <p :class="variance >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400'" class="mt-1 text-2xl font-bold">
          {{ variance.toLocaleString('vi-VN') }} VND
        </p>
      </div>
    </div>

    <div class="grid grid-cols-1 xl:grid-cols-2 gap-6">
      <div class="card">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Dự toán so với thực chi</h2>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">Biểu đồ cột so sánh ngân sách đã duyệt và chi phí thực tế</p>
        <div class="mt-6 grid grid-cols-2 gap-6 items-end min-h-[220px]">
          <div v-for="bar in budgetBars" :key="bar.key" class="flex flex-col items-center gap-2">
            <div class="w-20 rounded-t-lg" :class="bar.color" :style="{ height: `${bar.height}px` }"></div>
            <p class="text-xs text-gray-500 dark:text-gray-400">{{ bar.label }}</p>
            <p class="text-sm font-semibold text-gray-900 dark:text-white text-center">{{ bar.amount.toLocaleString('vi-VN') }}</p>
          </div>
        </div>
        <div class="mt-4 flex items-center justify-between text-sm text-gray-600 dark:text-gray-300">
          <span>Tỷ lệ sử dụng ngân sách</span>
          <span class="font-semibold">{{ budgetPct }}%</span>
        </div>
        <div class="mt-3 h-2 rounded-full bg-gray-200 dark:bg-gray-700 overflow-hidden">
          <div
            class="h-full rounded-full"
            :class="budgetPct > 100 ? 'bg-red-500' : budgetPct >= 90 ? 'bg-amber-500' : 'bg-emerald-500'"
            :style="{ width: `${Math.min(100, budgetPct)}%` }"
          ></div>
        </div>

        <div class="mt-5">
          <p class="text-sm font-medium text-gray-800 dark:text-gray-200">Phân bố rủi ro theo dự án</p>
          <div class="mt-2 h-2.5 rounded-full bg-gray-200 dark:bg-gray-700 overflow-hidden flex">
            <div
              v-for="segment in riskSegments"
              :key="segment.key"
              :class="segment.color"
              :style="{ width: `${segment.pct}%` }"
            ></div>
          </div>
          <div class="mt-3 grid grid-cols-1 sm:grid-cols-3 gap-2">
            <div v-for="segment in riskSegments" :key="`${segment.key}-legend`" :class="['rounded-md border px-2.5 py-1.5 text-xs', segment.cardClass]">
              <p :class="segment.textClass">{{ segment.label }}</p>
              <p class="font-semibold" :class="segment.textClass">{{ segment.value }} dự án ({{ segment.pct }}%)</p>
            </div>
          </div>
        </div>
      </div>

      <div class="card border-red-300 dark:border-red-900 bg-red-50/80 dark:bg-red-950/20">
        <h2 class="text-lg font-semibold text-red-700 dark:text-red-300">Rủi ro cao: AI cảnh báo hóa đơn</h2>
        <p class="mt-1 text-sm text-red-600/90 dark:text-red-300/90">Ưu tiên xử lý trước khi phê duyệt thanh toán</p>
        <div class="mt-4 flex items-center gap-4">
          <div class="h-24 w-24 rounded-full p-3" :style="riskDonutStyle">
            <div class="h-full w-full rounded-full bg-white dark:bg-gray-900 flex items-center justify-center">
              <span class="text-sm font-semibold text-gray-900 dark:text-white">{{ aiRedFlags.length + walletStore.pendingDeposits.length }}</span>
            </div>
          </div>
          <div class="text-sm text-red-700 dark:text-red-300">
            <p>Cửa sổ rủi ro hiện tại</p>
            <p class="mt-1">Red Flag: {{ aiRedFlags.length }}</p>
            <p>Top-up chờ duyệt: {{ walletStore.pendingDeposits.length }}</p>
          </div>
        </div>
        <div v-if="aiRedFlags.length === 0" class="mt-4 text-sm text-red-700 dark:text-red-300">Không có cảnh báo nghiêm trọng.</div>
        <div v-else class="mt-4 space-y-2">
          <div v-for="flag in aiRedFlags" :key="flag.id" class="rounded-lg border border-red-200 dark:border-red-900 bg-white/80 dark:bg-red-900/20 p-3">
            <p class="text-sm font-medium text-red-800 dark:text-red-200">{{ flag.title }}</p>
            <p class="mt-1 text-xs text-red-700 dark:text-red-300">{{ flag.detail }}</p>
          </div>
        </div>
      </div>
    </div>

    <div class="card">
      <h2 class="mb-4 text-lg font-semibold text-gray-900 dark:text-white">Yêu cầu top-up chờ duyệt</h2>
      <div v-if="isLoading" class="text-sm text-gray-500 dark:text-gray-400">Đang tải dữ liệu...</div>
      <div v-else-if="walletStore.pendingDeposits.length === 0" class="text-sm text-gray-500 dark:text-gray-400">Không có yêu cầu nào.</div>
      <div v-else class="space-y-3">
        <div v-for="tx in walletStore.pendingDeposits" :key="tx.id" class="rounded-lg border border-gray-200 p-4 dark:border-gray-700">
          <div class="flex flex-wrap items-start justify-between gap-2">
            <div>
              <p class="font-medium text-gray-900 dark:text-white">{{ tx.amount.toLocaleString('vi-VN') }} VND</p>
              <p class="text-sm text-gray-500 dark:text-gray-400">{{ new Date(tx.createdAt).toLocaleString('vi-VN') }}</p>
              <p class="text-xs text-gray-500 dark:text-gray-400">Biên lai: {{ tx.refId || 'N/A' }}</p>
            </div>
            <div class="flex gap-2">
              <button class="btn-secondary" @click="approve(tx.id, false)">Từ chối</button>
              <button class="btn-primary" @click="approve(tx.id, true)">Phê duyệt</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
