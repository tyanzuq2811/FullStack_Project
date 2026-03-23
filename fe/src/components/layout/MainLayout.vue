<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { useThemeStore } from '../../stores/theme'
import { useWalletStore } from '../../stores/wallet'
import { useSignalR } from '../../composables/useSignalR'
import {
  PhHouse,
  PhFolder,
  PhKanban,
  PhCalendar,
  PhWallet,
  PhTrophy,
  PhClipboardText,
  PhNewspaper,
  PhGear,
  PhList,
  PhSun,
  PhMoon,
  PhBell,
  PhSignOut,
  PhUsers
} from '@phosphor-icons/vue'

const router = useRouter()
const authStore = useAuthStore()
const themeStore = useThemeStore()
const walletStore = useWalletStore()
const { connect, disconnect } = useSignalR()

const isSidebarOpen = ref(true)
const showPersonalWalletBalance = computed(() => ['Client', 'Subcontractor'].includes(authStore.primaryRole))

// All available menu items with role restrictions
const allMenuItems = [
  { name: 'Dashboard', icon: 'home', route: '/', roles: ['Admin', 'ProjectManager', 'Accountant', 'Subcontractor', 'Client'] },
  { name: 'Dự án', icon: 'folder', route: '/projects', roles: ['Admin', 'ProjectManager', 'Subcontractor', 'Client'] },
  { name: 'Kanban', icon: 'kanban', route: '/kanban', roles: ['Admin', 'ProjectManager', 'Subcontractor', 'Client'] },
  { name: 'Công việc', icon: 'tasks', route: '/tasks', roles: ['Subcontractor'] },
  { name: 'Đặt lịch', icon: 'calendar', route: '/bookings', roles: ['ProjectManager', 'Subcontractor'] },
  { name: 'Ví tiền', icon: 'wallet', route: '/wallet', roles: ['ProjectManager', 'Accountant', 'Subcontractor', 'Client'] },
  { name: 'Bảng xếp hạng', icon: 'trophy', route: '/leaderboard', roles: ['Admin', 'ProjectManager', 'Client', 'Subcontractor'] },
  { name: 'Tin tức', icon: 'news', route: '/news', roles: ['Admin', 'ProjectManager', 'Accountant', 'Subcontractor', 'Client'] },
  { name: 'Người dùng', icon: 'users', route: '/users', roles: ['Admin'] },
  { name: 'Cài đặt', icon: 'settings', route: '/settings', roles: ['Admin', 'ProjectManager', 'Accountant', 'Subcontractor', 'Client'] },
]

// Filter menu items based on user roles
const menuItems = computed(() => {
  const roles = authStore.effectiveRoles
  if (!roles || roles.length === 0) return []

  return allMenuItems.filter(item => {
    return item.roles.some(role => roles.includes(role))
  })
})

const iconComponents: Record<string, any> = {
  home: PhHouse,
  folder: PhFolder,
  kanban: PhKanban,
  calendar: PhCalendar,
  wallet: PhWallet,
  trophy: PhTrophy,
  tasks: PhClipboardText,
  news: PhNewspaper,
  users: PhUsers,
  settings: PhGear
}

const getIconComponent = (iconName: string) => {
  return iconComponents[iconName] || PhHouse
}

const handleLogout = async () => {
  await disconnect()
  authStore.logout()
  router.push('/login')
}

onMounted(async () => {
  await connect()
  if (showPersonalWalletBalance.value) {
    await walletStore.fetchSummary()
  }
})
</script>

<template>
  <div class="min-h-screen bg-gray-50 dark:bg-gray-900 transition-colors duration-300">
    <!-- Sidebar -->
    <aside
      :class="[
        'fixed top-0 left-0 z-40 h-screen transition-transform duration-300',
        isSidebarOpen ? 'translate-x-0' : '-translate-x-full',
        'w-64 bg-white dark:bg-gray-800 border-r border-gray-200 dark:border-gray-700'
      ]"
    >
      <!-- Logo -->
      <div class="flex items-center gap-3 px-6 py-5 border-b border-gray-200 dark:border-gray-700">
        <div class="w-10 h-10 rounded-lg bg-gradient-to-br from-primary-400 to-primary-600 flex items-center justify-center">
          <span class="text-white font-bold text-lg">IP</span>
        </div>
        <div>
          <h1 class="text-lg font-bold text-gray-900 dark:text-white">IPM Pro</h1>
          <p class="text-xs text-gray-500 dark:text-gray-400">Interior Project Manager</p>
        </div>
      </div>

      <!-- Navigation -->
      <nav class="px-3 py-4 space-y-1">
        <RouterLink
          v-for="item in menuItems"
          :key="item.route"
          :to="item.route"
          class="sidebar-item"
          active-class="sidebar-item-active"
        >
          <component :is="getIconComponent(item.icon)" :size="20" weight="regular" />
          <span>{{ item.name }}</span>
        </RouterLink>
      </nav>

      <!-- User Info -->
      <div class="absolute bottom-0 left-0 right-0 p-4 border-t border-gray-200 dark:border-gray-700">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-full bg-primary-100 dark:bg-primary-900 flex items-center justify-center">
            <span class="text-primary-700 dark:text-primary-300 font-medium">
              {{ authStore.user?.fullName?.[0] || 'U' }}
            </span>
          </div>
          <div class="flex-1 min-w-0">
            <p class="text-sm font-medium text-gray-900 dark:text-white truncate">
              {{ authStore.user?.fullName }}
            </p>
            <p v-if="showPersonalWalletBalance" class="text-xs text-gray-500 dark:text-gray-400 truncate">
              {{ walletStore.summary?.balance?.toLocaleString() || 0 }} VNĐ
            </p>
            <p v-else class="text-xs text-gray-500 dark:text-gray-400 truncate">
              {{ authStore.primaryRole }}
            </p>
          </div>
        </div>
      </div>
    </aside>

    <!-- Main Content -->
    <div :class="['transition-all duration-300', isSidebarOpen ? 'ml-64' : 'ml-0']">
      <!-- Header -->
      <header class="sticky top-0 z-30 bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
        <div class="flex items-center justify-between px-6 py-4">
          <button
            @click="isSidebarOpen = !isSidebarOpen"
            class="p-2 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
          >
            <PhList :size="20" class="text-gray-600 dark:text-gray-300" />
          </button>

          <div class="flex items-center gap-4">
            <!-- Theme Toggle -->
            <button
              @click="themeStore.toggleTheme()"
              class="p-2 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            >
              <PhSun v-if="themeStore.isDark" :size="20" class="text-yellow-400" weight="fill" />
              <PhMoon v-else :size="20" class="text-gray-600" weight="fill" />
            </button>

            <!-- Notifications -->
            <button class="p-2 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors relative">
              <PhBell :size="20" class="text-gray-600 dark:text-gray-300" />
              <span class="absolute top-1 right-1 w-2 h-2 bg-red-500 rounded-full"></span>
            </button>

            <!-- Logout -->
            <button
              @click="handleLogout"
              class="p-2 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            >
              <PhSignOut :size="20" class="text-gray-600 dark:text-gray-300" />
            </button>
          </div>
        </div>
      </header>

      <!-- Page Content -->
      <main class="p-6">
        <RouterView v-slot="{ Component }">
          <transition name="fade" mode="out-in">
            <component :is="Component" />
          </transition>
        </RouterView>
      </main>
    </div>
  </div>
</template>

<style scoped>
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
