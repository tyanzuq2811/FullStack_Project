import axios from 'axios'
import { useAuthStore } from '../stores/auth'
import router from '../router'

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000/api',
  headers: {
    'Content-Type': 'application/json'
  }
})

// Request interceptor
api.interceptors.request.use(
  (config) => {
    const token = sessionStorage.getItem('accessToken')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => Promise.reject(error)
)

// Response interceptor
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config

    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true

      const authStore = useAuthStore()
      const success = await authStore.refreshTokens()

      if (success) {
        const token = sessionStorage.getItem('accessToken')
        originalRequest.headers.Authorization = `Bearer ${token}`
        return api(originalRequest)
      } else {
        authStore.logout()
        router.push({ name: 'login' })
      }
    }

    return Promise.reject(error)
  }
)

export default api
