<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useThemeStore } from '../stores/theme'
import { useToast } from 'vue-toastification'

const router = useRouter()
const authStore = useAuthStore()
const themeStore = useThemeStore()
const toast = useToast()

const form = ref({
  email: '',
  password: '',
  confirmPassword: '',
  fullName: '',
  phoneNumber: ''
})
const showPassword = ref(false)

const handleRegister = async () => {
  if (form.value.password !== form.value.confirmPassword) {
    toast.error('Mật khẩu xác nhận không khớp')
    return
  }

  const result = await authStore.register({
    email: form.value.email,
    password: form.value.password,
    confirmPassword: form.value.confirmPassword,
    fullName: form.value.fullName,
    phoneNumber: form.value.phoneNumber || undefined
  })

  if (result.success) {
    toast.success(result.message || 'Đăng ký thành công! Vui lòng đăng nhập.')
    authStore.logout()
    router.push('/login')
  } else {
    toast.error(result.message || 'Đăng ký thất bại')
  }
}
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-gradient-to-br from-primary-50 to-primary-100 dark:from-gray-900 dark:to-gray-800 transition-colors duration-300 p-4">
    <button
      @click="themeStore.toggleTheme()"
      class="fixed top-4 right-4 p-2 rounded-lg bg-white dark:bg-gray-800 shadow-lg"
    >
      <svg v-if="themeStore.isDark" class="w-5 h-5 text-yellow-400" fill="currentColor" viewBox="0 0 20 20">
        <path fill-rule="evenodd" d="M10 2a1 1 0 011 1v1a1 1 0 11-2 0V3a1 1 0 011-1zm4 8a4 4 0 11-8 0 4 4 0 018 0zm-.464 4.95l.707.707a1 1 0 001.414-1.414l-.707-.707a1 1 0 00-1.414 1.414zm2.12-10.607a1 1 0 010 1.414l-.706.707a1 1 0 11-1.414-1.414l.707-.707a1 1 0 011.414 0zM17 11a1 1 0 100-2h-1a1 1 0 100 2h1zm-7 4a1 1 0 011 1v1a1 1 0 11-2 0v-1a1 1 0 011-1zM5.05 6.464A1 1 0 106.465 5.05l-.708-.707a1 1 0 00-1.414 1.414l.707.707zm1.414 8.486l-.707.707a1 1 0 01-1.414-1.414l.707-.707a1 1 0 011.414 1.414zM4 11a1 1 0 100-2H3a1 1 0 000 2h1z" clip-rule="evenodd" />
      </svg>
      <svg v-else class="w-5 h-5 text-gray-600" fill="currentColor" viewBox="0 0 20 20">
        <path d="M17.293 13.293A8 8 0 016.707 2.707a8.001 8.001 0 1010.586 10.586z" />
      </svg>
    </button>

    <div class="w-full max-w-md">
      <div class="text-center mb-8">
        <div class="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-gradient-to-br from-primary-400 to-primary-600 mb-4 shadow-lg">
          <span class="text-white font-bold text-2xl">IP</span>
        </div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">IPM Pro</h1>
      </div>

      <div class="card">
        <h2 class="text-xl font-semibold text-gray-900 dark:text-white mb-6">Đăng ký tài khoản</h2>

        <form @submit.prevent="handleRegister" class="space-y-4">
          <div>
            <label class="label">Họ và tên</label>
            <input v-model="form.fullName" type="text" class="input" placeholder="Nguyễn Văn A" required />
          </div>
          <div>
            <label class="label">Email</label>
            <input v-model="form.email" type="email" class="input" placeholder="your@email.com" required />
          </div>
          <div>
            <label class="label">Số điện thoại (tùy chọn)</label>
            <input v-model="form.phoneNumber" type="tel" class="input" placeholder="0901234567" />
          </div>
          <div>
            <label class="label">Mật khẩu</label>
            <input v-model="form.password" :type="showPassword ? 'text' : 'password'" class="input" placeholder="••••••••" required />
          </div>
          <div>
            <label class="label">Xác nhận mật khẩu</label>
            <input v-model="form.confirmPassword" :type="showPassword ? 'text' : 'password'" class="input" placeholder="••••••••" required />
          </div>
          <label class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
            <input v-model="showPassword" type="checkbox" class="rounded border-gray-300 dark:border-gray-600 text-primary-500" />
            Hiển thị mật khẩu
          </label>
          <button type="submit" :disabled="authStore.isLoading" class="w-full btn-primary py-3">
            {{ authStore.isLoading ? 'Đang đăng ký...' : 'Đăng ký' }}
          </button>
        </form>

        <div class="mt-6 text-center text-sm text-gray-600 dark:text-gray-400">
          Đã có tài khoản?
          <RouterLink to="/login" class="text-primary-600 hover:text-primary-700 dark:text-primary-400 font-medium">
            Đăng nhập
          </RouterLink>
        </div>
      </div>
    </div>
  </div>
</template>
