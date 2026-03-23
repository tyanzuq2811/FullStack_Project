<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '../services/api'
import { useToast } from 'vue-toastification'
import { usePagination } from '../composables/usePagination'

const toast = useToast()

interface Contractor {
  memberId: string
  memberName: string
  rank: number
  eloScore: number
}

const contractors = ref<Contractor[]>([])
const loading = ref(true)
const {
  pageSize,
  currentPage,
  totalPages,
  paginatedItems: paginatedContractors,
  pageNumbers,
  setPage
} = usePagination(contractors, {
  pageSize: 10,
  resetOn: contractors
})

const topThree = computed(() => contractors.value.slice(0, 3))
const others = computed(() => contractors.value.slice(3))

const getDisplayRank = (index: number) => ((currentPage.value - 1) * pageSize) + index + 1

const getRankColor = (rank: number) => {
  if (rank >= 1500) return 'text-yellow-600 dark:text-yellow-400'
  if (rank >= 1400) return 'text-purple-600 dark:text-purple-400'
  if (rank >= 1300) return 'text-blue-600 dark:text-blue-400'
  return 'text-gray-600 dark:text-gray-400'
}

const getRankBadge = (rank: number) => {
  if (rank >= 1500) return { label: 'Master', color: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-300' }
  if (rank >= 1400) return { label: 'Expert', color: 'bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-300' }
  if (rank >= 1300) return { label: 'Advanced', color: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-300' }
  return { label: 'Intermediate', color: 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300' }
}

const getMedalColor = (index: number) => {
  if (index === 0) return 'text-yellow-500'
  if (index === 1) return 'text-gray-400'
  if (index === 2) return 'text-orange-600'
  return ''
}

const getInitials = (name: string) => {
  if (!name) return '??'
  return name
    .split(' ')
    .map(n => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2)
}

const fetchLeaderboard = async () => {
  try {
    loading.value = true
    const response = await api.get('/leaderboard')
    // Extract leaderboard data from ApiResponse wrapper
    if (response.data?.success && response.data?.data) {
      contractors.value = response.data.data
    } else {
      contractors.value = []
      toast.error(response.data?.message || 'Không thể tải bảng xếp hạng')
    }
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Không thể tải bảng xếp hạng')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchLeaderboard()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Bảng xếp hạng ELO</h1>
        <p class="text-gray-600 dark:text-gray-400 mt-1">
          Top nhà thầu uy tín nhất dựa trên lịch sử hoàn thành dự án
        </p>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex justify-center py-12">
      <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-500"></div>
    </div>

    <template v-else>
      <!-- Top 3 Podium -->
      <div v-if="topThree.length > 0" class="card bg-gradient-to-br from-primary-50 to-primary-100 dark:from-gray-800 dark:to-gray-900">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white mb-6">Top 3 Nhà thầu xuất sắc</h2>
        <div class="flex items-end justify-center gap-4">
          <!-- 2nd Place -->
          <div v-if="topThree[1]" class="flex-1 text-center" v-motion-slide-visible-once-bottom>
            <div class="bg-white dark:bg-gray-800 rounded-lg p-4 shadow-lg">
              <div class="relative inline-block">
                <div class="w-16 h-16 rounded-full bg-gradient-to-br from-gray-300 to-gray-500 flex items-center justify-center text-white text-xl font-bold">
                  {{ getInitials(topThree[1].memberName) }}
                </div>
                <div class="absolute -top-2 -right-2 w-8 h-8 bg-gray-400 rounded-full flex items-center justify-center text-white font-bold text-sm">
                  2
                </div>
              </div>
              <h3 class="font-semibold text-gray-900 dark:text-white mt-3">{{ topThree[1].memberName }}</h3>
              <div class="text-2xl font-bold text-gray-600 dark:text-gray-300">
                {{ topThree[1].eloScore }}
              </div>
            </div>
            <div class="h-24 bg-gray-300 dark:bg-gray-700 rounded-t-lg mt-4"></div>
          </div>

          <!-- 1st Place -->
          <div v-if="topThree[0]" class="flex-1 text-center" v-motion-slide-visible-once-bottom :delay="100">
            <div class="bg-white dark:bg-gray-800 rounded-lg p-4 shadow-xl border-2 border-yellow-400">
              <div class="relative inline-block">
                <div class="w-20 h-20 rounded-full bg-gradient-to-br from-yellow-300 to-yellow-600 flex items-center justify-center text-white text-2xl font-bold">
                  {{ getInitials(topThree[0].memberName) }}
                </div>
                <div class="absolute -top-2 -right-2 w-10 h-10 bg-yellow-500 rounded-full flex items-center justify-center">
                  <svg class="w-6 h-6 text-white" fill="currentColor" viewBox="0 0 20 20">
                    <path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z"/>
                  </svg>
                </div>
              </div>
              <h3 class="font-bold text-gray-900 dark:text-white mt-3 text-lg">{{ topThree[0].memberName }}</h3>
              <div class="text-3xl font-bold text-yellow-600 dark:text-yellow-400">
                {{ topThree[0].eloScore }}
              </div>
            </div>
            <div class="h-32 bg-gradient-to-br from-yellow-300 to-yellow-500 rounded-t-lg mt-4"></div>
          </div>

          <!-- 3rd Place -->
          <div v-if="topThree[2]" class="flex-1 text-center" v-motion-slide-visible-once-bottom :delay="200">
            <div class="bg-white dark:bg-gray-800 rounded-lg p-4 shadow-lg">
              <div class="relative inline-block">
                <div class="w-16 h-16 rounded-full bg-gradient-to-br from-orange-300 to-orange-600 flex items-center justify-center text-white text-xl font-bold">
                  {{ getInitials(topThree[2].memberName) }}
                </div>
                <div class="absolute -top-2 -right-2 w-8 h-8 bg-orange-600 rounded-full flex items-center justify-center text-white font-bold text-sm">
                  3
                </div>
              </div>
              <h3 class="font-semibold text-gray-900 dark:text-white mt-3">{{ topThree[2].memberName }}</h3>
              <div class="text-2xl font-bold text-orange-600 dark:text-orange-400">
                {{ topThree[2].eloScore }}
              </div>
            </div>
            <div class="h-20 bg-orange-400 dark:bg-orange-700 rounded-t-lg mt-4"></div>
          </div>
        </div>
      </div>

      <!-- Full Ranking Table -->
      <div class="card">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Bảng xếp hạng đầy đủ</h2>
        <div class="overflow-x-auto">
          <table class="w-full">
            <thead>
              <tr class="border-b border-gray-200 dark:border-gray-700">
                <th class="text-left py-3 px-4 font-semibold text-gray-900 dark:text-white">Hạng</th>
                <th class="text-left py-3 px-4 font-semibold text-gray-900 dark:text-white">Nhà thầu</th>
                <th class="text-center py-3 px-4 font-semibold text-gray-900 dark:text-white">ELO</th>
                <th class="text-center py-3 px-4 font-semibold text-gray-900 dark:text-white">Cấp độ</th>
                <th class="text-center py-3 px-4 font-semibold text-gray-900 dark:text-white">Đánh giá</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="(contractor, index) in paginatedContractors"
                :key="contractor.memberId"
                class="border-b border-gray-100 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors"
              >
                <td class="py-4 px-4">
                  <div class="flex items-center">
                    <span :class="['text-lg font-bold', getMedalColor(index)]">
                      {{ getDisplayRank(index) <= 3 ? '🏆' : `#${getDisplayRank(index)}` }}
                    </span>
                  </div>
                </td>
                <td class="py-4 px-4">
                  <div class="flex items-center space-x-3">
                    <div class="w-10 h-10 rounded-full bg-primary-500 flex items-center justify-center text-white font-semibold">
                      {{ getInitials(contractor.memberName) }}
                    </div>
                    <div>
                      <div class="font-medium text-gray-900 dark:text-white">{{ contractor.memberName }}</div>
                      <div class="text-sm text-gray-600 dark:text-gray-400">Rank #{{ getDisplayRank(index) }}</div>
                    </div>
                  </div>
                </td>
                <td class="py-4 px-4 text-center">
                  <span :class="['text-xl font-bold', getRankColor(contractor.eloScore)]">
                    {{ contractor.eloScore }}
                  </span>
                </td>
                <td class="py-4 px-4 text-center">
                  <span :class="['px-3 py-1 text-xs font-medium rounded-full', getRankBadge(contractor.eloScore).color]">
                    {{ getRankBadge(contractor.eloScore).label }}
                  </span>
                </td>
                <td class="py-4 px-4 text-center">
                  <span class="text-gray-900 dark:text-white font-medium">
                    -
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-if="contractors.length > pageSize" class="mt-4 border-t border-gray-200 dark:border-gray-700 pt-4">
          <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <p class="text-sm text-gray-500 dark:text-gray-400">
              Hiển thị {{ (currentPage - 1) * pageSize + 1 }}-{{ Math.min(currentPage * pageSize, contractors.length) }} / {{ contractors.length }} nhà thầu
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

      <!-- ELO Info -->
      <div class="card bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800">
        <h3 class="font-semibold text-blue-900 dark:text-blue-300 mb-2">
          🏅 Hệ thống xếp hạng ELO
        </h3>
        <p class="text-sm text-blue-800 dark:text-blue-400">
          Điểm ELO được tính dựa trên chất lượng và tỷ lệ hoàn thành dự án đúng hạn. Nhà thầu có ELO cao hơn sẽ được ưu tiên trong các dự án lớn.
        </p>
        <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mt-4">
          <div class="text-center">
            <div class="text-yellow-600 dark:text-yellow-400 font-bold">≥ 1500</div>
            <div class="text-xs text-gray-600 dark:text-gray-400">Master</div>
          </div>
          <div class="text-center">
            <div class="text-purple-600 dark:text-purple-400 font-bold">≥ 1400</div>
            <div class="text-xs text-gray-600 dark:text-gray-400">Expert</div>
          </div>
          <div class="text-center">
            <div class="text-blue-600 dark:text-blue-400 font-bold">≥ 1300</div>
            <div class="text-xs text-gray-600 dark:text-gray-400">Advanced</div>
          </div>
          <div class="text-center">
            <div class="text-gray-600 dark:text-gray-400 font-bold">< 1300</div>
            <div class="text-xs text-gray-600 dark:text-gray-400">Intermediate</div>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>
