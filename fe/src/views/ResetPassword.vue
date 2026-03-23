<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import api from '../services/api'
import { useToast } from 'vue-toastification'

const toast = useToast()

const route = useRoute()
const router = useRouter()

const token = ref('')
const email = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const loading = ref(false)
const showPassword = ref(false)
const success = ref(false)

const passwordStrength = ref({
  score: 0,
  label: '',
  color: ''
})

const checkPasswordStrength = () => {
  const password = newPassword.value
  let score = 0

  if (password.length >= 8) score++
  if (password.length >= 12) score++
  if (/[a-z]/.test(password) && /[A-Z]/.test(password)) score++
  if (/\d/.test(password)) score++
  if (/[^a-zA-Z\d]/.test(password)) score++

  passwordStrength.value.score = score

  if (score <= 2) {
    passwordStrength.value.label = 'Yếu'
    passwordStrength.value.color = 'bg-red-500'
  } else if (score === 3) {
    passwordStrength.value.label = 'Trung bình'
    passwordStrength.value.color = 'bg-yellow-500'
  } else if (score === 4) {
    passwordStrength.value.label = 'Tốt'
    passwordStrength.value.color = 'bg-blue-500'
  } else {
    passwordStrength.value.label = 'Rất tốt'
    passwordStrength.value.color = 'bg-green-500'
  }
}

const resetPassword = async () => {
  if (newPassword.value !== confirmPassword.value) {
    toast.error('Mật khẩu xác nhận không khớp')
    return
  }

  if (passwordStrength.value.score < 3) {
    toast.error('Mật khẩu quá yếu, vui lòng chọn mật khẩu mạnh hơn')
    return
  }

  try {
    loading.value = true
    await api.post('/auth/reset-password', {
      token: token.value,
      email: email.value,
      newPassword: newPassword.value
    })
    success.value = true
    toast.success('Đặt lại mật khẩu thành công!')
    setTimeout(() => {
      router.push('/login')
    }, 3000)
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Không thể đặt lại mật khẩu')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  token.value = (route.query.token as string) || ''
  email.value = (route.query.email as string) || ''

  if (!token.value || !email.value) {
    toast.error('Link khôi phục không hợp lệ')
    setTimeout(() => {
      router.push('/forgot-password')
    }, 2000)
  }
})
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
        <div v-if="success" class="text-center">
          <div class="mb-6">
            <div class="w-16 h-16 bg-green-100 dark:bg-green-900 rounded-full flex items-center justify-center mx-auto mb-4">
              <svg class="w-8 h-8 text-green-600 dark:text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/>
              </svg>
            </div>
            <h1 class="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Thành công!
            </h1>
            <p class="text-gray-600 dark:text-gray-400">
              Mật khẩu của bạn đã được đặt lại. Đang chuyển đến trang đăng nhập...
            </p>
          </div>

          <RouterLink to="/login" class="btn btn-primary w-full">
            Đăng nhập ngay
          </RouterLink>
        </div>

        <!-- Form State -->
        <div v-else>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white mb-2 text-center">
            Đặt lại mật khẩu
          </h1>
          <p class="text-gray-600 dark:text-gray-400 mb-6 text-center">
            Email: <strong>{{ email }}</strong>
          </p>

          <form @submit.prevent="resetPassword" class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                Mật khẩu mới
              </label>
              <div class="relative">
                <input
                  v-model="newPassword"
                  :type="showPassword ? 'text' : 'password'"
                  required
                  minlength="8"
                  placeholder="Nhập mật khẩu mới"
                  class="input w-full pr-10"
                  @input="checkPasswordStrength"
                  :disabled="loading"
                />
                <button
                  type="button"
                  @click="showPassword = !showPassword"
                  class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
                >
                  <svg v-if="!showPassword" class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"/>
                  </svg>
                  <svg v-else class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21"/>
                  </svg>
                </button>
              </div>

              <!-- Password Strength -->
              <div v-if="newPassword" class="mt-2">
                <div class="flex items-center justify-between text-sm mb-1">
                  <span class="text-gray-600 dark:text-gray-400">Độ mạnh:</span>
                  <span :class="['font-medium', passwordStrength.score <= 2 ? 'text-red-600' : passwordStrength.score === 3 ? 'text-yellow-600' : 'text-green-600']">
                    {{ passwordStrength.label }}
                  </span>
                </div>
                <div class="h-2 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
                  <div
                    :class="['h-full transition-all duration-300', passwordStrength.color]"
                    :style="{ width: `${(passwordStrength.score / 5) * 100}%` }"
                  ></div>
                </div>
              </div>

              <ul class="mt-3 text-xs text-gray-600 dark:text-gray-400 space-y-1">
                <li :class="newPassword.length >= 8 ? 'text-green-600' : ''">
                  ✓ Tối thiểu 8 ký tự
                </li>
                <li :class="/[a-z]/.test(newPassword) && /[A-Z]/.test(newPassword) ? 'text-green-600' : ''">
                  ✓ Chữ hoa và chữ thường
                </li>
                <li :class="/\d/.test(newPassword) ? 'text-green-600' : ''">
                  ✓ Ít nhất 1 số
                </li>
                <li :class="/[^a-zA-Z\d]/.test(newPassword) ? 'text-green-600' : ''">
                  ✓ Ít nhất 1 ký tự đặc biệt
                </li>
              </ul>
            </div>

            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                Xác nhận mật khẩu
              </label>
              <input
                v-model="confirmPassword"
                :type="showPassword ? 'text' : 'password'"
                required
                placeholder="Nhập lại mật khẩu mới"
                class="input w-full"
                :disabled="loading"
              />
              <p v-if="confirmPassword && newPassword !== confirmPassword" class="text-sm text-red-600 mt-1">
                Mật khẩu không khớp
              </p>
            </div>

            <button
              type="submit"
              :disabled="loading || !newPassword || newPassword !== confirmPassword"
              class="btn btn-primary w-full"
            >
              <span v-if="loading">Đang xử lý...</span>
              <span v-else>Đặt lại mật khẩu</span>
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

      <!-- Additional Info -->
      <div class="mt-6 text-center text-sm text-gray-600 dark:text-gray-400">
        Link khôi phục sẽ hết hạn sau 1 giờ
      </div>
    </div>
  </div>
</template>
