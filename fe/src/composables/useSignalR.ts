import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '../stores/auth'
import { useWalletStore } from '../stores/wallet'
import { useToast } from 'vue-toastification'

let connection: signalR.HubConnection | null = null

export function useSignalR() {
  const toast = useToast()

  async function connect() {
    const authStore = useAuthStore()
    if (!authStore.accessToken) return

    const apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000'

    connection = new signalR.HubConnectionBuilder()
      .withUrl(`${apiUrl}/hubs/notifications`, {
        accessTokenFactory: () => authStore.accessToken || ''
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build()

    // Event handlers
    connection.on('ReceiveNotification', (notification: any) => {
      const toastType = notification.type === 'success' ? toast.success :
                       notification.type === 'warning' ? toast.warning :
                       notification.type === 'error' ? toast.error : toast.info
      toastType(notification.message, { timeout: 5000 })
    })

    connection.on('WalletBalanceChanged', (data: any) => {
      const walletStore = useWalletStore()
      walletStore.updateBalance(data.newBalance)

      const message = data.changeAmount > 0
        ? `+${data.changeAmount.toLocaleString()} VNĐ`
        : `${data.changeAmount.toLocaleString()} VNĐ`
      toast.info(`Số dư ví: ${message}`)
    })

    connection.on('TaskStatusChanged', (data: any) => {
      toast.info(`Task "${data.taskName}" đã được cập nhật`)
      // Emit custom event for components to listen
      window.dispatchEvent(new CustomEvent('taskStatusChanged', { detail: data }))
    })

    connection.on('BookingStatusChanged', (data: any) => {
      window.dispatchEvent(new CustomEvent('bookingStatusChanged', { detail: data }))
    })

    connection.on('AiAnalysisCompleted', (data: any) => {
      if (data.hasAnomalies) {
        toast.warning(`AI phát hiện ${data.anomalies.length} vấn đề trong Task #${data.taskId}`)
      } else {
        toast.success(`AI đã phân tích xong. Tiến độ: ${data.progressPct}%`)
      }
    })

    try {
      await connection.start()
      console.log('SignalR Connected')

      // Join booking updates group
      await connection.invoke('JoinBookingGroup')
    } catch (err) {
      console.error('SignalR Connection Error:', err)
    }
  }

  async function disconnect() {
    if (connection) {
      await connection.stop()
      connection = null
    }
  }

  async function joinProjectGroup(projectId: string) {
    if (connection?.state === signalR.HubConnectionState.Connected) {
      await connection.invoke('JoinProjectGroup', projectId)
    }
  }

  async function leaveProjectGroup(projectId: string) {
    if (connection?.state === signalR.HubConnectionState.Connected) {
      await connection.invoke('LeaveProjectGroup', projectId)
    }
  }

  return {
    connect,
    disconnect,
    joinProjectGroup,
    leaveProjectGroup,
    connection
  }
}
