<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink } from 'vue-router'
import api from '../services/api'
import { useToast } from 'vue-toastification'

const toast = useToast()

const email = ref('')
const loading = ref(false)
const emailSent = ref(false)

const sendResetLink = async () => {
  try {
    loading.value = true
    await api.post('/auth/forgot-password', { email: email.value })
    emailSent.value = true
    toast.success('Email khôi phục đã được gửi! Vui lòng kiểm tra hộp thư.')
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Không thể gửi email khôi phục')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-gray-50 dark:bg-gray-900 p-4">
    <div class="w-full max-w-md" v-motion-pop>
      <div class="card">
        <!-- Logo/Icon -->
        <div class="flex justify-center mb-6">
          <div class="w-16 h-16 bg-primary-100 dark:bg-primary-900 rounded-full flex items-center justify-center">
            <svg class="w-8 h-8 text-primary-600 dark:text-primary-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z"/>
            </svg>
          </div>
        </div>

        <!-- Success State -->
        <div v-if="emailSent" class="text-center">
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white mb-2">
            Email đã được gửi!
          </h1>
          <p class="text-gray-600 dark:text-gray-400 mb-6">
            Chúng tôi đã gửi link khôi phục mật khẩu đến <strong>{{ email }}</strong>.
            Vui lòng kiểm tra hộp thư và làm theo hướng dẫn.
          </p>

          <div class="bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg p-4 mb-6">
            <p class="text-sm text-blue-800 dark:text-blue-400">
              💡 Link khôi phục sẽ hết hạn sau 1 giờ. Nếu không nhận được email, vui lòng kiểm tra thư mục spam.
            </p>
          </div>

          <div class="space-y-3">
            <RouterLink to="/login" class="btn btn-primary w-full">
              Quay lại đăng nhập
            </RouterLink>
            <button
              @click="emailSent = false"
              class="btn btn-secondary w-full"
            >
              Gửi lại email
            </button>
          </div>
        </div>

        <!-- Form State -->
        <div v-else>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white mb-2 text-center">
            Quên mật khẩu?
          </h1>
          <p class="text-gray-600 dark:text-gray-400 mb-6 text-center">
            Nhập email đăng ký của bạn, chúng tôi sẽ gửi link khôi phục mật khẩu.
          </p>

          <form @submit.prevent="sendResetLink" class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                Email
              </label>
              <input
                v-model="email"
                type="email"
                required
                placeholder="your@email.com"
                class="input w-full"
                :disabled="loading"
              />
            </div>

            <button
              type="submit"
              :disabled="loading"
              class="btn btn-primary w-full"
            >
              <span v-if="loading">Đang gửi...</span>
              <span v-else>Gửi link khôi phục</span>
            </button>

            <div class="text-center">
              <RouterLink
                to="/login"
                class="text-sm text-primary-600 hover:text-primary-700 dark:text-primary-400 dark:hover:text-primary-300"
              >
                ← Quay lại đăng nhập
              </RouterLink>
            </div>
          </form>
        </div>
      </div>

      <!-- Additional Help -->
      <div class="mt-6 text-center text-sm text-gray-600 dark:text-gray-400">
        Vẫn gặp vấn đề?
        <a href="mailto:support@ipm-pro.com" class="text-primary-600 hover:text-primary-700 dark:text-primary-400">
          Liên hệ hỗ trợ
        </a>
      </div>
    </div>
  </div>
</template>
