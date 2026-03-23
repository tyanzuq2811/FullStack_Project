<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '../services/api'
import { useToast } from 'vue-toastification'
import { PhMagnifyingGlass, PhPlus, PhPencil, PhTrash, PhKey, PhToggleLeft, PhToggleRight } from '@phosphor-icons/vue'
import { usePagination } from '../composables/usePagination'

const toast = useToast()

interface User {
  id: string
  fullName: string
  email: string
  phoneNumber: string | null
  rankELO: number
  isActive: boolean
  roles: string[]
}

const users = ref<User[]>([])
const loading = ref(true)
const searchQuery = ref('')
const showModal = ref(false)
const showDeleteModal = ref(false)
const modalMode = ref<'create' | 'edit' | 'password'>('create')
const selectedUser = ref<User | null>(null)
const deleting = ref(false)
const deleteConfirmChecked = ref(false)

const formData = ref({
  fullName: '',
  email: '',
  password: '',
  phoneNumber: '',
  role: 'Client',
  isActive: true
})

const roles = [
  { value: 'Admin', label: 'Quản trị viên' },
  { value: 'ProjectManager', label: 'Quản lý dự án' },
  { value: 'Accountant', label: 'Kế toán' },
  { value: 'Subcontractor', label: 'Nhà thầu phụ' },
  { value: 'Client', label: 'Khách hàng' }
]

const filteredUsers = computed(() => {
  if (!searchQuery.value) return users.value

  const query = searchQuery.value.toLowerCase()
  return users.value.filter(user =>
    user.fullName.toLowerCase().includes(query) ||
    user.email.toLowerCase().includes(query) ||
    user.roles.some(role => role.toLowerCase().includes(query))
  )
})

const {
  pageSize,
  currentPage,
  totalPages,
  paginatedItems: paginatedUsers,
  pageNumbers,
  setPage
} = usePagination(filteredUsers, {
  pageSize: 10,
  resetOn: searchQuery
})

const getRoleBadgeColor = (role: string) => {
  const colors: Record<string, string> = {
    Admin: 'bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-300',
    ProjectManager: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-300',
    Accountant: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300',
    Subcontractor: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-300',
    Client: 'bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-300'
  }
  return colors[role] || 'bg-gray-100 text-gray-800'
}

const getRoleLabel = (role: string) => {
  return roles.find(r => r.value === role)?.label || role
}

const fetchUsers = async () => {
  try {
    loading.value = true
    const response = await api.get('/users')
    if (response.data?.success && response.data?.data) {
      users.value = response.data.data
    }
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Không thể tải danh sách người dùng')
  } finally {
    loading.value = false
  }
}

const openCreateModal = () => {
  modalMode.value = 'create'
  selectedUser.value = null
  formData.value = {
    fullName: '',
    email: '',
    password: '',
    phoneNumber: '',
    role: 'Client',
    isActive: true
  }
  showModal.value = true
}

const openEditModal = (user: User) => {
  modalMode.value = 'edit'
  selectedUser.value = user
  formData.value = {
    fullName: user.fullName,
    email: user.email,
    password: '',
    phoneNumber: user.phoneNumber || '',
    role: user.roles[0] || 'Client',
    isActive: user.isActive
  }
  showModal.value = true
}

const openPasswordModal = (user: User) => {
  modalMode.value = 'password'
  selectedUser.value = user
  formData.value = {
    fullName: '',
    email: '',
    password: '',
    phoneNumber: '',
    role: 'Client',
    isActive: true
  }
  showModal.value = true
}

const closeModal = () => {
  showModal.value = false
  selectedUser.value = null
}

const handleSubmit = async () => {
  try {
    if (modalMode.value === 'create') {
      await api.post('/users', formData.value)
      toast.success('Tạo người dùng thành công!')
    } else if (modalMode.value === 'edit') {
      await api.put(`/users/${selectedUser.value?.id}`, {
        fullName: formData.value.fullName,
        email: formData.value.email,
        phoneNumber: formData.value.phoneNumber,
        role: formData.value.role,
        isActive: formData.value.isActive
      })
      toast.success('Cập nhật người dùng thành công!')
    } else if (modalMode.value === 'password') {
      await api.post(`/users/${selectedUser.value?.id}/change-password`, {
        newPassword: formData.value.password
      })
      toast.success('Đổi mật khẩu thành công!')
    }
    closeModal()
    fetchUsers()
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Thao tác thất bại')
  }
}

const toggleUserStatus = async (user: User) => {
  try {
    await api.post(`/users/${user.id}/toggle-status`)
    toast.success(`Đã ${!user.isActive ? 'kích hoạt' : 'vô hiệu hóa'} người dùng`)
    fetchUsers()
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Không thể thay đổi trạng thái')
  }
}

const openDeleteModal = (user: User) => {
  selectedUser.value = user
  deleteConfirmChecked.value = false
  showDeleteModal.value = true
}

const closeDeleteModal = () => {
  showDeleteModal.value = false
  deleteConfirmChecked.value = false
  selectedUser.value = null
}

const deleteUser = async () => {
  if (!selectedUser.value) return

  deleting.value = true
  try {
    await api.delete(`/users/${selectedUser.value.id}`)
    toast.success('Đã xóa người dùng khỏi hệ thống')
    closeDeleteModal()
    fetchUsers()
  } catch (error: any) {
    toast.error(error.response?.data?.message || 'Không thể xóa người dùng')
  } finally {
    deleting.value = false
  }
}

onMounted(() => {
  fetchUsers()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Quản lý người dùng</h1>
        <p class="text-gray-600 dark:text-gray-400 mt-1">
          Quản lý tài khoản, phân quyền và trạng thái người dùng
        </p>
      </div>
      <button @click="openCreateModal" class="btn btn-primary">
        <PhPlus :size="20" class="mr-2" />
        Thêm người dùng
      </button>
    </div>

    <!-- Search Bar -->
    <div class="card">
      <div class="relative">
        <PhMagnifyingGlass :size="20" class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
        <input
          v-model="searchQuery"
          type="text"
          placeholder="Tìm kiếm theo tên, email, vai trò..."
          class="input pl-10 w-full"
        />
      </div>
    </div>

    <!-- Stats -->
    <div class="grid grid-cols-1 md:grid-cols-4 gap-6">
      <div class="card">
        <div class="text-sm text-gray-600 dark:text-gray-400">Tổng số</div>
        <div class="text-2xl font-bold text-gray-900 dark:text-white mt-1">{{ users.length }}</div>
      </div>
      <div class="card">
        <div class="text-sm text-gray-600 dark:text-gray-400">Đang hoạt động</div>
        <div class="text-2xl font-bold text-green-600 dark:text-green-400 mt-1">
          {{ users.filter(u => u.isActive).length }}
        </div>
      </div>
      <div class="card">
        <div class="text-sm text-gray-600 dark:text-gray-400">Vô hiệu hóa</div>
        <div class="text-2xl font-bold text-red-600 dark:text-red-400 mt-1">
          {{ users.filter(u => !u.isActive).length }}
        </div>
      </div>
      <div class="card">
        <div class="text-sm text-gray-600 dark:text-gray-400">Kết quả tìm kiếm</div>
        <div class="text-2xl font-bold text-primary-600 dark:text-primary-400 mt-1">
          {{ filteredUsers.length }}
        </div>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex justify-center py-12">
      <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-500"></div>
    </div>

    <!-- Users Table -->
    <div v-else class="card overflow-hidden">
      <div class="overflow-x-auto">
        <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
          <thead class="bg-gray-50 dark:bg-gray-800">
            <tr>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                Người dùng
              </th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                Vai trò
              </th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                Điện thoại
              </th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                ELO
              </th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                Trạng thái
              </th>
              <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                Thao tác
              </th>
            </tr>
          </thead>
          <tbody class="bg-white dark:bg-gray-900 divide-y divide-gray-200 dark:divide-gray-700">
            <tr v-for="user in paginatedUsers" :key="user.id" class="hover:bg-gray-50 dark:hover:bg-gray-800">
              <td class="px-6 py-4 whitespace-nowrap">
                <div class="flex items-center">
                  <div class="flex-shrink-0 h-10 w-10">
                    <div class="h-10 w-10 rounded-full bg-primary-100 dark:bg-primary-900 flex items-center justify-center">
                      <span class="text-primary-700 dark:text-primary-300 font-medium">
                        {{ user.fullName[0] }}
                      </span>
                    </div>
                  </div>
                  <div class="ml-4">
                    <div class="text-sm font-medium text-gray-900 dark:text-white">{{ user.fullName }}</div>
                    <div class="text-sm text-gray-500 dark:text-gray-400">{{ user.email }}</div>
                  </div>
                </div>
              </td>
              <td class="px-6 py-4 whitespace-nowrap">
                <span
                  v-for="role in user.roles"
                  :key="role"
                  :class="['px-2 py-1 text-xs font-medium rounded-full', getRoleBadgeColor(role)]"
                >
                  {{ getRoleLabel(role) }}
                </span>
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500 dark:text-gray-400">
                {{ user.phoneNumber || '-' }}
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900 dark:text-white">
                {{ user.rankELO.toFixed(0) }}
              </td>
              <td class="px-6 py-4 whitespace-nowrap">
                <button
                  @click="toggleUserStatus(user)"
                  :class="[
                    'flex items-center gap-2 px-3 py-1 text-xs font-medium rounded-full transition-colors',
                    user.isActive
                      ? 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300 hover:bg-green-200 dark:hover:bg-green-800'
                      : 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-300 hover:bg-red-200 dark:hover:bg-red-800'
                  ]"
                >
                  <component :is="user.isActive ? PhToggleRight : PhToggleLeft" :size="16" />
                  {{ user.isActive ? 'Hoạt động' : 'Vô hiệu hóa' }}
                </button>
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-right text-sm font-medium">
                <div class="flex items-center justify-end gap-2">
                  <button
                    @click="openEditModal(user)"
                    class="p-2 text-primary-600 hover:text-primary-900 dark:text-primary-400 dark:hover:text-primary-300"
                    title="Chỉnh sửa"
                  >
                    <PhPencil :size="18" />
                  </button>
                  <button
                    @click="openPasswordModal(user)"
                    class="p-2 text-blue-600 hover:text-blue-900 dark:text-blue-400 dark:hover:text-blue-300"
                    title="Đổi mật khẩu"
                  >
                    <PhKey :size="18" />
                  </button>
                  <button
                    @click="openDeleteModal(user)"
                    class="p-2 text-red-600 hover:text-red-900 dark:text-red-400 dark:hover:text-red-300"
                    title="Xóa vĩnh viễn"
                  >
                    <PhTrash :size="18" />
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-if="filteredUsers.length > pageSize" class="px-6 py-4 border-t border-gray-200 dark:border-gray-700">
        <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <p class="text-sm text-gray-500 dark:text-gray-400">
            Hiển thị {{ (currentPage - 1) * pageSize + 1 }}-{{ Math.min(currentPage * pageSize, filteredUsers.length) }} / {{ filteredUsers.length }} người dùng
          </p>
          <div class="flex items-center gap-2 flex-wrap">
            <button
              class="btn-secondary px-3 py-1.5 text-sm"
              :disabled="currentPage === 1"
              @click="setPage(currentPage - 1)"
            >
              Trước
            </button>
            <button
              v-for="page in pageNumbers"
              :key="page"
              class="px-3 py-1.5 text-sm rounded-lg border transition-colors"
              :class="page === currentPage
                ? 'bg-primary-600 text-white border-primary-600'
                : 'border-gray-300 text-gray-700 hover:bg-gray-100 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-700'"
              @click="setPage(page)"
            >
              {{ page }}
            </button>
            <button
              class="btn-secondary px-3 py-1.5 text-sm"
              :disabled="currentPage === totalPages"
              @click="setPage(currentPage + 1)"
            >
              Sau
            </button>
          </div>
        </div>
      </div>

      <!-- Empty State -->
      <div v-if="filteredUsers.length === 0" class="text-center py-12">
        <p class="text-gray-500 dark:text-gray-400">Không tìm thấy người dùng</p>
      </div>
    </div>

    <!-- Modal -->
    <div
      v-if="showModal"
      class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4"
      @click.self="closeModal"
    >
      <div class="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-md w-full p-6" v-motion-pop>
        <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">
          {{ modalMode === 'create' ? 'Thêm người dùng mới' : modalMode === 'edit' ? 'Chỉnh sửa người dùng' : 'Đổi mật khẩu' }}
        </h2>

        <form @submit.prevent="handleSubmit" class="space-y-4">
          <template v-if="modalMode !== 'password'">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                Họ và tên *
              </label>
              <input
                v-model="formData.fullName"
                type="text"
                required
                class="input w-full"
                placeholder="Nguyễn Văn A"
              />
            </div>

            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                Email *
              </label>
              <input
                v-model="formData.email"
                type="email"
                required
                class="input w-full"
                placeholder="email@example.com"
              />
            </div>

            <div v-if="modalMode === 'create'">
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                Mật khẩu *
              </label>
              <input
                v-model="formData.password"
                type="password"
                required
                minlength="8"
                class="input w-full"
                placeholder="Tối thiểu 8 ký tự"
              />
            </div>

            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                Điện thoại
              </label>
              <input
                v-model="formData.phoneNumber"
                type="tel"
                class="input w-full"
                placeholder="0901234567"
              />
            </div>

            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                Vai trò *
              </label>
              <select v-model="formData.role" required class="input w-full">
                <option v-for="role in roles" :key="role.value" :value="role.value">
                  {{ role.label }}
                </option>
              </select>
            </div>

            <div v-if="modalMode === 'edit'" class="flex items-center">
              <input
                v-model="formData.isActive"
                type="checkbox"
                id="isActive"
                class="w-4 h-4 text-primary-600 rounded focus:ring-primary-500"
              />
              <label for="isActive" class="ml-2 text-sm text-gray-700 dark:text-gray-300">
                Tài khoản đang hoạt động
              </label>
            </div>
          </template>

          <template v-else>
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                Mật khẩu mới *
              </label>
              <input
                v-model="formData.password"
                type="password"
                required
                minlength="8"
                class="input w-full"
                placeholder="Tối thiểu 8 ký tự"
              />
            </div>
            <p class="text-sm text-gray-600 dark:text-gray-400">
              Đặt mật khẩu mới cho: <strong>{{ selectedUser?.fullName }}</strong>
            </p>
          </template>

          <div class="flex gap-3 pt-4">
            <button type="button" @click="closeModal" class="btn btn-secondary flex-1">
              Hủy
            </button>
            <button type="submit" class="btn btn-primary flex-1">
              {{ modalMode === 'create' ? 'Tạo' : modalMode === 'edit' ? 'Cập nhật' : 'Đổi mật khẩu' }}
            </button>
          </div>
        </form>
      </div>
    </div>

    <div
      v-if="showDeleteModal"
      class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4"
      @click.self="closeDeleteModal"
    >
      <div class="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-md w-full p-6" v-motion-pop>
        <div class="flex items-start gap-3">
          <div class="mt-0.5 rounded-full bg-red-100 dark:bg-red-900/40 p-2">
            <PhTrash :size="18" class="text-red-600 dark:text-red-400" />
          </div>
          <div class="flex-1">
            <h3 class="text-lg font-semibold text-gray-900 dark:text-white">Xóa vĩnh viễn người dùng</h3>
            <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">
              Hành động này không thể hoàn tác. Tài khoản
              <span class="font-medium text-gray-900 dark:text-white">{{ selectedUser?.fullName }}</span>
              sẽ bị xóa khỏi hệ thống.
            </p>
          </div>
        </div>

        <label class="mt-4 flex items-start gap-2 text-sm text-gray-700 dark:text-gray-300">
          <input
            v-model="deleteConfirmChecked"
            type="checkbox"
            class="mt-0.5 h-4 w-4 rounded border-gray-300 text-red-600 focus:ring-red-500"
          />
          <span>Tôi hiểu hành động này không thể hoàn tác.</span>
        </label>

        <div class="mt-6 flex gap-3">
          <button type="button" class="btn btn-secondary flex-1" @click="closeDeleteModal" :disabled="deleting">
            Hủy
          </button>
          <button type="button" class="btn bg-red-600 text-white hover:bg-red-700 flex-1 disabled:opacity-50 disabled:cursor-not-allowed" @click="deleteUser" :disabled="deleting || !deleteConfirmChecked">
            {{ deleting ? 'Đang xóa...' : 'Xóa vĩnh viễn' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
