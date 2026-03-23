import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      component: () => import('../components/layout/MainLayout.vue'),
      meta: { requiresAuth: true },
      children: [
        {
          path: '',
          name: 'dashboard',
          component: () => import('../views/Dashboard.vue'),
          meta: { allowedRoles: ['Admin', 'ProjectManager', 'Accountant', 'Subcontractor', 'Client'] }
        },
        {
          path: 'projects',
          name: 'projects',
          component: () => import('../views/Projects.vue'),
          meta: { allowedRoles: ['Admin', 'ProjectManager', 'Subcontractor', 'Client'] }
        },
        {
          path: 'projects/:id',
          name: 'project-detail',
          component: () => import('../views/ProjectDetail.vue'),
          meta: { allowedRoles: ['Admin', 'Accountant', 'ProjectManager', 'Subcontractor', 'Client'] }
        },
        {
          path: 'tasks',
          name: 'tasks',
          component: () => import('../views/Tasks.vue'),
          meta: { allowedRoles: ['Subcontractor'] }
        },
        {
          path: 'kanban',
          name: 'kanban',
          component: () => import('../views/Kanban.vue'),
          meta: { allowedRoles: ['Admin', 'ProjectManager', 'Subcontractor', 'Client'] }
        },
        {
          path: 'bookings',
          name: 'bookings',
          component: () => import('../views/Bookings.vue'),
          meta: { allowedRoles: ['ProjectManager', 'Subcontractor'] }
        },
        {
          path: 'wallet',
          name: 'wallet',
          component: () => import('../views/Wallet.vue'),
          meta: { allowedRoles: ['ProjectManager', 'Accountant', 'Subcontractor', 'Client'] }
        },
        {
          path: 'leaderboard',
          name: 'leaderboard',
          component: () => import('../views/Leaderboard.vue'),
          meta: { allowedRoles: ['Admin', 'ProjectManager', 'Subcontractor', 'Client'] }
        },
        {
          path: 'settings',
          name: 'settings',
          component: () => import('../views/Settings.vue'),
          meta: { allowedRoles: ['Admin', 'ProjectManager', 'Accountant', 'Subcontractor', 'Client'] }
        },
        {
          path: 'news',
          name: 'news',
          component: () => import('../views/News.vue'),
          meta: { allowedRoles: ['Admin', 'ProjectManager', 'Accountant', 'Subcontractor', 'Client'] }
        },
        {
          path: 'users',
          name: 'users',
          component: () => import('../views/Users.vue'),
          meta: { allowedRoles: ['Admin'] }
        }
      ]
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('../views/Login.vue'),
      meta: { guest: true }
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('../views/Register.vue'),
      meta: { guest: true }
    },
    {
      path: '/forgot-password',
      name: 'forgot-password',
      component: () => import('../views/ForgotPassword.vue'),
      meta: { guest: true }
    },
    {
      path: '/reset-password',
      name: 'reset-password',
      component: () => import('../views/ResetPassword.vue'),
      meta: { guest: true }
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('../views/NotFound.vue')
    }
  ]
})

router.beforeEach(async (to, _from, next) => {
  const authStore = useAuthStore()

  // Check if route requires authentication
  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    return next({ name: 'login', query: { redirect: to.fullPath } })
  }

  if (authStore.isAuthenticated && !authStore.user) {
    await authStore.fetchCurrentUser()
  }

  const allowedRoles = to.meta.allowedRoles as string[] | undefined
  if (allowedRoles && allowedRoles.length > 0) {
    const userRoles = authStore.effectiveRoles || []
    const hasPermission = allowedRoles.some(role => userRoles.includes(role))
    if (!hasPermission) {
      return next({ name: 'dashboard' })
    }
  }

  // Redirect authenticated users away from guest pages
  if (to.meta.guest && authStore.isAuthenticated) {
    return next({ name: 'dashboard' })
  }

  next()
})

export default router
