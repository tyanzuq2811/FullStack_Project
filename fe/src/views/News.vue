<script setup lang="ts">
import { ref, onMounted } from 'vue'
import api from '../services/api'
import { useToast } from 'vue-toastification'
import { usePagination } from '../composables/usePagination'

const toast = useToast()

interface News {
  id: number
  title: string
  content: string
  isPinned: boolean
  createdAt: string
  updatedAt: string
}

const newsList = ref<News[]>([])
const pinnedNews = ref<News[]>([])
const loading = ref(true)
const {
  pageSize,
  currentPage,
  totalPages,
  paginatedItems: paginatedNews,
  pageNumbers,
  setPage
} = usePagination(newsList, {
  pageSize: 10,
  resetOn: newsList
})

const formatDate = (dateString: string) => {
  return new Date(dateString).toLocaleDateString('vi-VN', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const fetchNews = async () => {
  try {
    loading.value = true
    const [newsResponse, pinnedResponse] = await Promise.all([
      api.get('/news'),
      api.get('/news/pinned')
    ])

    if (newsResponse.data?.success && newsResponse.data?.data) {
      newsList.value = newsResponse.data.data
    } else {
      newsList.value = []
    }

    if (pinnedResponse.data?.success && pinnedResponse.data?.data) {
      pinnedNews.value = pinnedResponse.data.data
    } else {
      pinnedNews.value = []
    }
  } catch (error: any) {
    toast.error('Không thể tải tin tức')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchNews()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div>
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Tin tức & Thông báo</h1>
      <p class="text-gray-600 dark:text-gray-400 mt-1">
        Cập nhật thông tin mới nhất từ hệ thống
      </p>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex justify-center py-12">
      <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-500"></div>
    </div>

    <template v-else>
      <!-- Pinned News -->
      <div v-if="pinnedNews.length > 0" class="space-y-4">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white flex items-center">
          <svg class="w-5 h-5 mr-2 text-yellow-500" fill="currentColor" viewBox="0 0 20 20">
            <path d="M5 4a2 2 0 012-2h6a2 2 0 012 2v14l-5-2.5L5 18V4z"/>
          </svg>
          Tin quan trọng
        </h2>
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <div
            v-for="news in pinnedNews"
            :key="news.id"
            class="card border-l-4 border-yellow-500 bg-yellow-50 dark:bg-yellow-900/20"
          >
            <div class="flex items-start justify-between">
              <h3 class="font-semibold text-gray-900 dark:text-white">{{ news.title }}</h3>
              <span class="text-xs text-yellow-600 dark:text-yellow-400 bg-yellow-100 dark:bg-yellow-800 px-2 py-1 rounded">
                Ghim
              </span>
            </div>
            <p class="text-gray-600 dark:text-gray-400 mt-2 line-clamp-3">{{ news.content }}</p>
            <div class="text-xs text-gray-500 dark:text-gray-500 mt-3">
              {{ formatDate(news.createdAt) }}
            </div>
          </div>
        </div>
      </div>

      <!-- All News -->
      <div class="space-y-4">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Tất cả tin tức</h2>

        <div v-if="newsList.length === 0" class="card text-center py-12">
          <svg class="mx-auto h-12 w-12 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 20H5a2 2 0 01-2-2V6a2 2 0 012-2h10a2 2 0 012 2v1m2 13a2 2 0 01-2-2V7m2 13a2 2 0 002-2V9a2 2 0 00-2-2h-2m-4-3H9M7 16h6M7 8h6v4H7V8z"/>
          </svg>
          <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">Chưa có tin tức</h3>
          <p class="mt-2 text-gray-600 dark:text-gray-400">Hiện tại chưa có tin tức nào được đăng</p>
        </div>

        <div v-else class="space-y-4">
          <div
            v-for="news in paginatedNews"
            :key="news.id"
            class="card hover:shadow-lg transition-shadow"
          >
            <div class="flex items-start justify-between">
              <h3 class="font-semibold text-gray-900 dark:text-white text-lg">{{ news.title }}</h3>
              <span v-if="news.isPinned" class="text-xs text-yellow-600 dark:text-yellow-400 bg-yellow-100 dark:bg-yellow-800 px-2 py-1 rounded">
                Ghim
              </span>
            </div>
            <p class="text-gray-600 dark:text-gray-400 mt-3 whitespace-pre-line">{{ news.content }}</p>
            <div class="flex items-center justify-between mt-4 pt-4 border-t border-gray-200 dark:border-gray-700">
              <div class="text-sm text-gray-500 dark:text-gray-500">
                Đăng lúc: {{ formatDate(news.createdAt) }}
              </div>
              <div v-if="news.updatedAt !== news.createdAt" class="text-sm text-gray-500 dark:text-gray-500">
                Cập nhật: {{ formatDate(news.updatedAt) }}
              </div>
            </div>
          </div>

          <div v-if="newsList.length > pageSize" class="card">
            <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
              <p class="text-sm text-gray-500 dark:text-gray-400">
                Hiển thị {{ (currentPage - 1) * pageSize + 1 }}-{{ Math.min(currentPage * pageSize, newsList.length) }} / {{ newsList.length }} tin tức
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
      </div>
    </template>
  </div>
</template>
