<script setup lang="ts">
import { computed } from 'vue'
import { useAuthStore } from '../stores/auth'
import AdminDashboard from '../components/dashboard/AdminDashboard.vue'
import AccountantDashboard from '../components/dashboard/AccountantDashboard.vue'
import ProjectManagerDashboard from '../components/dashboard/ProjectManagerDashboard.vue'
import SubcontractorDashboard from '../components/dashboard/SubcontractorDashboard.vue'
import ClientDashboard from '../components/dashboard/ClientDashboard.vue'

const authStore = useAuthStore()

const roleLabels: Record<string, string> = {
  Admin: 'Quản trị viên',
  Accountant: 'Kế toán',
  ProjectManager: 'Quản lý dự án',
  Subcontractor: 'Nhà thầu phụ',
  Client: 'Chủ đầu tư'
}

const roleComponentMap = {
  Admin: AdminDashboard,
  Accountant: AccountantDashboard,
  ProjectManager: ProjectManagerDashboard,
  Subcontractor: SubcontractorDashboard,
  Client: ClientDashboard
}

const primaryRole = computed(() => authStore.primaryRole || authStore.effectiveRoles[0] || '')
const activeRoleLabel = computed(() => roleLabels[primaryRole.value] || 'Người dùng')
const roleDashboardComponent = computed(() => {
  const key = primaryRole.value as keyof typeof roleComponentMap
  return roleComponentMap[key] || ClientDashboard
})
</script>

<template>
  <div class="space-y-6">
    <div class="rounded-2xl border border-primary-100 dark:border-primary-900/50 bg-gradient-to-r from-primary-50/80 to-sky-50/70 dark:from-primary-950/20 dark:to-sky-950/10 p-5">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">
          Xin chào, {{ authStore.user?.fullName }}!
        </h1>
        <p class="mt-1 text-gray-600 dark:text-gray-400">
          Dashboard đã được tối ưu theo vai trò nghiệp vụ của bạn.
        </p>
        <div class="mt-3 inline-flex items-center gap-2 rounded-full bg-primary-50 px-3 py-1 text-sm text-primary-700 dark:bg-primary-900/30 dark:text-primary-300">
          <span class="h-2 w-2 rounded-full bg-primary-500"></span>
          Vai trò hiện tại: {{ activeRoleLabel }}
        </div>
      </div>
    </div>

    <component :is="roleDashboardComponent" />
  </div>
</template>
