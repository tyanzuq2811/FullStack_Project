import { defineStore } from 'pinia'
import { ref } from 'vue'
import { api } from '../services/api'
import type { WalletSummary, Transaction } from '../types'

export const useWalletStore = defineStore('wallet', () => {
  const summary = ref<WalletSummary | null>(null)
  const transactions = ref<Transaction[]>([])
  const pendingDeposits = ref<Transaction[]>([])
  const isLoading = ref(false)
  const totalPages = ref(0)

  async function fetchSummary() {
    isLoading.value = true
    try {
      const response = await api.get('/wallets/me')
      if (response.data.success) {
        summary.value = response.data.data
      }
      return response.data
    } catch (error: any) {
      return { success: false, message: error.response?.data?.message || 'Không thể tải ví' }
    } finally {
      isLoading.value = false
    }
  }

  async function fetchTransactions(page = 1, pageSize = 20) {
    isLoading.value = true
    try {
      const response = await api.get('/wallets/transactions', {
        params: { page, pageSize }
      })
      if (response.data.success) {
        transactions.value = response.data.data.items
        totalPages.value = response.data.data.totalPages
      }
    } finally {
      isLoading.value = false
    }
  }

  async function requestDeposit(amount: number, projectId: string, receiptImageUrl: string, description?: string) {
    try {
      const response = await api.post('/wallets/deposit', {
        amount,
        projectId,
        receiptImageUrl,
        description
      })
      return response.data
    } catch (error: any) {
      return { success: false, message: error.response?.data?.message || 'Yêu cầu nạp tiền thất bại' }
    }
  }

  async function fetchPendingDeposits() {
    isLoading.value = true
    try {
      const response = await api.get('/wallets/pending')
      if (response.data.success) {
        pendingDeposits.value = response.data.data || []
      }
    } finally {
      isLoading.value = false
    }
  }

  async function approveDeposit(id: string, approved: boolean, notes?: string) {
    try {
      const query = notes ? `?notes=${encodeURIComponent(notes)}` : ''
      const response = await api.post(`/wallets/approve/${id}${query}`, approved)
      return response.data
    } catch (error: any) {
      return { success: false, message: error.response?.data?.message || 'Không thể xử lý yêu cầu nạp tiền' }
    }
  }

  function updateBalance(newBalance: number) {
    if (summary.value) {
      summary.value.balance = newBalance
    }
  }

  return {
    summary,
    transactions,
    pendingDeposits,
    isLoading,
    totalPages,
    fetchSummary,
    fetchTransactions,
    requestDeposit,
    fetchPendingDeposits,
    approveDeposit,
    updateBalance
  }
})
