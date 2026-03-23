<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useThemeStore } from '../stores/theme'
import api from '../services/api'
import { useToast } from 'vue-toastification'

const toast = useToast()

const authStore = useAuthStore()
const themeStore = useThemeStore()

const activeTab = ref('profile')
const loading = ref(false)

const profileForm = ref({
  fullName: '',
  email: '',
  phoneNumber: ''
})

const passwordForm = ref({
  currentPassword: '',
  newPassword: '',
  confirmPassword: ''
})

const notificationSettings = ref({
  emailNotifications: true,
  projectUpdates: true,
  taskReminders: true,
  walletTransactions: true
})

const tabs = [
  { id: 'profile', label: 'Thông tin cá nhân', icon: 'M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z' },
  { id: 'security', label: 'Bảo mật', icon: 'M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z' },
  { id: 'appearance', label: 'Giao diện', icon: 'M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z' },
  { id: 'notifications', label: 'Thông báo', icon: 'M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9' }
]

const fetchProfile = async () => {
  try {
    const response = await api.get('/auth/me')
    // Extract profile data from ApiResponse wrapper
    if (response.data?.success && response.data?.data) {
      profileForm.value = response.data.data
    } else {
      toast.error(response.data?.message || 'Không thể tải thông tin cá nhân')
    }
  } catch (error: any) {
    toast.error('Không thể tải thông tin cá nhân')
  }
}

const updateProfile = async () => {
  try {
    loading.value = true
    const response = await api.put('/auth/profile', profileForm.value)
    if (response.data?.success) {
      toast.success('Cập nhật thông tin thành công!')
    } else {
      toast.error(response.data?.message || 'Cập nhật thất bại')
    }
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Cập nhật thất bại')
  } finally {
    loading.value = false
  }
}

const changePassword = async () => {
  if (passwordForm.value.newPassword !== passwordForm.value.confirmPassword) {
    toast.error('Mật khẩu xác nhận không khớp')
    return
  }

  try {
    loading.value = true
    const response = await api.post('/auth/change-password', {
      currentPassword: passwordForm.value.currentPassword,
      newPassword: passwordForm.value.newPassword
    })
    if (response.data?.success) {
      toast.success('Đổi mật khẩu thành công!')
      passwordForm.value = {
        currentPassword: '',
        newPassword: '',
        confirmPassword: ''
      }
    } else {
      toast.error(response.data?.message || 'Đổi mật khẩu thất bại')
    }
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Đổi mật khẩu thất bại')
  } finally {
    loading.value = false
  }
}

const saveNotificationSettings = async () => {
  try {
    loading.value = true
    const response = await api.put('/auth/notifications', notificationSettings.value)
    if (response.data?.success) {
      toast.success('Lưu cài đặt thành công!')
    } else {
      toast.error(response.data?.message || 'Lưu cài đặt thất bại')
    }
  } catch (error: any) {
    toast.error('Lưu cài đặt thất bại')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchProfile()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div>
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Cài đặt</h1>
      <p class="text-gray-600 dark:text-gray-400 mt-1">
        Quản lý thông tin cá nhân và tùy chọn hệ thống
      </p>
    </div>

    <!-- Tabs -->
    <div class="border-b border-gray-200 dark:border-gray-700">
      <nav class="-mb-px flex space-x-8 overflow-x-auto">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          @click="activeTab = tab.id"
          :class="[
            'flex items-center py-4 px-1 border-b-2 font-medium text-sm whitespace-nowrap',
            activeTab === tab.id
              ? 'border-primary-500 text-primary-600 dark:text-primary-400'
              : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300 dark:text-gray-400 dark:hover:text-gray-300'
          ]"
        >
          <svg class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="tab.icon"/>
          </svg>
          {{ tab.label }}
        </button>
      </nav>
    </div>

    <!-- Profile Tab -->
    <div v-if="activeTab === 'profile'" class="card" v-motion-slide-visible-once-left>
      <h2 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Thông tin cá nhân</h2>
      <form @submit.prevent="updateProfile" class="space-y-4">
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            Họ và tên
          </label>
          <input v-model="profileForm.fullName" type="text" required class="input w-full" />
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            Email
          </label>
          <input v-model="profileForm.email" type="email" required class="input w-full" />
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            Số điện thoại
          </label>
          <input v-model="profileForm.phoneNumber" type="tel" class="input w-full" />
        </div>

        <div class="pt-4">
          <button type="submit" :disabled="loading" class="btn btn-primary">
            <span v-if="loading">Đang lưu...</span>
            <span v-else>Lưu thay đổi</span>
          </button>
        </div>
      </form>
    </div>

    <!-- Security Tab -->
    <div v-if="activeTab === 'security'" class="card" v-motion-slide-visible-once-left>
      <h2 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Đổi mật khẩu</h2>
      <form @submit.prevent="changePassword" class="space-y-4">
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            Mật khẩu hiện tại
          </label>
          <input
            v-model="passwordForm.currentPassword"
            type="password"
            required
            class="input w-full"
          />
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            Mật khẩu mới
          </label>
          <input
            v-model="passwordForm.newPassword"
            type="password"
            required
            minlength="8"
            class="input w-full"
          />
          <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
            Tối thiểu 8 ký tự, bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt
          </p>
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            Xác nhận mật khẩu mới
          </label>
          <input
            v-model="passwordForm.confirmPassword"
            type="password"
            required
            class="input w-full"
          />
        </div>

        <div class="pt-4">
          <button type="submit" :disabled="loading" class="btn btn-primary">
            <span v-if="loading">Đang đổi...</span>
            <span v-else>Đổi mật khẩu</span>
          </button>
        </div>
      </form>
    </div>

    <!-- Appearance Tab -->
    <div v-if="activeTab === 'appearance'" class="card" v-motion-slide-visible-once-left>
      <h2 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Giao diện</h2>
      <div class="space-y-4">
        <div class="flex items-center justify-between">
          <div>
            <h3 class="font-medium text-gray-900 dark:text-white">Chế độ tối</h3>
            <p class="text-sm text-gray-600 dark:text-gray-400">
              Sử dụng giao diện tối để giảm mỏi mắt
            </p>
          </div>
          <button
            @click="themeStore.toggleTheme()"
            :class="[
              'relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-2',
              themeStore.isDark ? 'bg-primary-600' : 'bg-gray-200'
            ]"
          >
            <span
              :class="[
                'pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out',
                themeStore.isDark ? 'translate-x-5' : 'translate-x-0'
              ]"
            ></span>
          </button>
        </div>

        <div class="pt-4 border-t border-gray-200 dark:border-gray-700">
          <p class="text-sm text-gray-600 dark:text-gray-400">
            Giao diện hiện tại: <span class="font-medium">{{ themeStore.isDark ? 'Tối' : 'Sáng' }}</span>
          </p>
        </div>
      </div>
    </div>

    <!-- Notifications Tab -->
    <div v-if="activeTab === 'notifications'" class="card" v-motion-slide-visible-once-left>
      <h2 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Thông báo</h2>
      <form @submit.prevent="saveNotificationSettings" class="space-y-4">
        <div class="space-y-4">
          <div class="flex items-center justify-between">
            <div>
              <h3 class="font-medium text-gray-900 dark:text-white">Thông báo email</h3>
              <p class="text-sm text-gray-600 dark:text-gray-400">
                Nhận thông báo qua email
              </p>
            </div>
            <input
              v-model="notificationSettings.emailNotifications"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
            />
          </div>

          <div class="flex items-center justify-between">
            <div>
              <h3 class="font-medium text-gray-900 dark:text-white">Cập nhật dự án</h3>
              <p class="text-sm text-gray-600 dark:text-gray-400">
                Thông báo khi có cập nhật dự án
              </p>
            </div>
            <input
              v-model="notificationSettings.projectUpdates"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
            />
          </div>

          <div class="flex items-center justify-between">
            <div>
              <h3 class="font-medium text-gray-900 dark:text-white">Nhắc công việc</h3>
              <p class="text-sm text-gray-600 dark:text-gray-400">
                Nhắc nhở về công việc sắp đến hạn
              </p>
            </div>
            <input
              v-model="notificationSettings.taskReminders"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
            />
          </div>

          <div class="flex items-center justify-between">
            <div>
              <h3 class="font-medium text-gray-900 dark:text-white">Giao dịch ví</h3>
              <p class="text-sm text-gray-600 dark:text-gray-400">
                Thông báo về giao dịch trong ví
              </p>
            </div>
            <input
              v-model="notificationSettings.walletTransactions"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
            />
          </div>
        </div>

        <div class="pt-4">
          <button type="submit" :disabled="loading" class="btn btn-primary">
            <span v-if="loading">Đang lưu...</span>
            <span v-else>Lưu cài đặt</span>
          </button>
        </div>
      </form>
    </div>
  </div>
</template>
