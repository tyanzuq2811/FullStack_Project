import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { api } from '../services/api'
import type { User, LoginRequest, RegisterRequest } from '../types'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null)
  const accessToken = ref<string | null>(sessionStorage.getItem('accessToken'))
  const refreshToken = ref<string | null>(sessionStorage.getItem('refreshToken'))
  const isLoading = ref(false)

  const isAuthenticated = computed(() => !!accessToken.value)
  const isAdmin = computed(() => user.value?.roles?.includes('Admin') || false)
  const isManager = computed(() => user.value?.roles?.includes('ProjectManager') || false)
  const primaryRole = computed(() => {
    const roles = user.value?.roles || []
    if (roles.includes('Admin')) return 'Admin'
    if (roles.includes('Accountant')) return 'Accountant'
    if (roles.includes('ProjectManager')) return 'ProjectManager'
    if (roles.includes('Subcontractor')) return 'Subcontractor'
    if (roles.includes('Client')) return 'Client'
    return ''
  })
  const effectiveRoles = computed(() => {
    return primaryRole.value ? [primaryRole.value] : []
  })

  const hasRole = (role: string) => effectiveRoles.value.includes(role)

  async function login(credentials: LoginRequest) {
    isLoading.value = true
    try {
      const response = await api.post('/auth/login', credentials)
      if (response.data.success) {
        setTokens(response.data.data.accessToken, response.data.data.refreshToken)
        user.value = response.data.data.user
        return { success: true }
      }
      return { success: false, message: response.data.message }
    } catch (error: any) {
      return { success: false, message: error.response?.data?.message || 'Đăng nhập thất bại' }
    } finally {
      isLoading.value = false
    }
  }

  async function register(data: RegisterRequest) {
    isLoading.value = true
    try {
      const response = await api.post('/auth/register', data)
      if (response.data.success) {
        // Keep signup as a non-authenticated flow; user must login explicitly.
        return { success: true, message: response.data.message }
      }
      return { success: false, message: response.data.message }
    } catch (error: any) {
      return { success: false, message: error.response?.data?.message || 'Đăng ký thất bại' }
    } finally {
      isLoading.value = false
    }
  }

  async function fetchCurrentUser() {
    if (!accessToken.value) return
    try {
      const response = await api.get('/auth/me')
      if (response.data.success) {
        user.value = response.data.data
      }
    } catch {
      logout()
    }
  }

  async function refreshTokens() {
    if (!accessToken.value || !refreshToken.value) return false
    try {
      const response = await api.post('/auth/refresh', {
        accessToken: accessToken.value,
        refreshToken: refreshToken.value
      })
      if (response.data.success) {
        setTokens(response.data.data.accessToken, response.data.data.refreshToken)
        user.value = response.data.data.user
        return true
      }
      return false
    } catch {
      logout()
      return false
    }
  }

  function setTokens(access: string, refresh: string) {
    accessToken.value = access
    refreshToken.value = refresh
    sessionStorage.setItem('accessToken', access)
    sessionStorage.setItem('refreshToken', refresh)
  }

  function logout() {
    user.value = null
    accessToken.value = null
    refreshToken.value = null
    sessionStorage.removeItem('accessToken')
    sessionStorage.removeItem('refreshToken')
  }

  // Initialize user if token exists
  if (accessToken.value) {
    fetchCurrentUser()
  }

  return {
    user,
    accessToken,
    isAuthenticated,
    isAdmin,
    isManager,
    primaryRole,
    effectiveRoles,
    hasRole,
    isLoading,
    login,
    register,
    logout,
    fetchCurrentUser,
    refreshTokens
  }
})
