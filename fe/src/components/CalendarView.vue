<script setup lang="ts">
import { ref, computed } from 'vue'
import { PhCaretLeft, PhCaretRight } from '@phosphor-icons/vue'

interface Booking {
  id: string
  resourceName: string
  startTime: string
  endTime: string
  status: string
  price: number
  memberName: string
  projectId?: string
}

interface Props {
  bookings: Booking[]
}

const props = defineProps<Props>()
const emit = defineEmits<{
  (e: 'view-booking', booking: Booking): void
}>()

const currentDate = ref(new Date())

const year = computed(() => currentDate.value.getFullYear())
const month = computed(() => currentDate.value.getMonth())

const monthNames = [
  'Tháng 1', 'Tháng 2', 'Tháng 3', 'Tháng 4', 'Tháng 5', 'Tháng 6',
  'Tháng 7', 'Tháng 8', 'Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12'
]

const weekDays = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7']

// Get calendar days
const calendarDays = computed(() => {
  const firstDay = new Date(year.value, month.value, 1)
  const lastDay = new Date(year.value, month.value + 1, 0)
  const prevLastDay = new Date(year.value, month.value, 0)

  const firstDayIndex = firstDay.getDay()
  const lastDayDate = lastDay.getDate()
  const prevLastDayDate = prevLastDay.getDate()

  const days = []

  // Previous month days
  for (let i = firstDayIndex; i > 0; i--) {
    days.push({
      date: prevLastDayDate - i + 1,
      isCurrentMonth: false,
      fullDate: new Date(year.value, month.value - 1, prevLastDayDate - i + 1)
    })
  }

  // Current month days
  for (let i = 1; i <= lastDayDate; i++) {
    days.push({
      date: i,
      isCurrentMonth: true,
      fullDate: new Date(year.value, month.value, i)
    })
  }

  // Next month days
  const remainingDays = 42 - days.length // 6 weeks * 7 days
  for (let i = 1; i <= remainingDays; i++) {
    days.push({
      date: i,
      isCurrentMonth: false,
      fullDate: new Date(year.value, month.value + 1, i)
    })
  }

  return days
})

// Get bookings for a specific date
const getBookingsForDate = (date: Date) => {
  return props.bookings.filter(booking => {
    const bookingStart = new Date(booking.startTime)
    const bookingEnd = new Date(booking.endTime)

    // Check if date falls within booking period
    const dateOnly = new Date(date.getFullYear(), date.getMonth(), date.getDate())
    const startOnly = new Date(bookingStart.getFullYear(), bookingStart.getMonth(), bookingStart.getDate())
    const endOnly = new Date(bookingEnd.getFullYear(), bookingEnd.getMonth(), bookingEnd.getDate())

    return dateOnly >= startOnly && dateOnly <= endOnly
  })
}

const isToday = (date: Date) => {
  const today = new Date()
  return date.getDate() === today.getDate() &&
    date.getMonth() === today.getMonth() &&
    date.getFullYear() === today.getFullYear()
}

const previousMonth = () => {
  currentDate.value = new Date(year.value, month.value - 1, 1)
}

const nextMonth = () => {
  currentDate.value = new Date(year.value, month.value + 1, 1)
}

const goToToday = () => {
  currentDate.value = new Date()
}

const getStatusColor = (status: string) => {
  const colors: Record<string, string> = {
    PendingPayment: 'bg-yellow-400',
    Confirmed: 'bg-green-400',
    Completed: 'bg-blue-400',
    Cancelled: 'bg-red-400'
  }
  return colors[status] || 'bg-gray-400'
}
</script>

<template>
  <div class="card">
    <!-- Calendar Header -->
    <div class="flex items-center justify-between mb-6">
      <button @click="previousMonth" class="p-2 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg">
        <PhCaretLeft :size="20" class="text-gray-600 dark:text-gray-300" />
      </button>

      <div class="flex items-center gap-4">
        <h2 class="text-xl font-bold text-gray-900 dark:text-white">
          {{ monthNames[month] }} {{ year }}
        </h2>
        <button @click="goToToday" class="btn btn-secondary text-sm">
          Hôm nay
        </button>
      </div>

      <button @click="nextMonth" class="p-2 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg">
        <PhCaretRight :size="20" class="text-gray-600 dark:text-gray-300" />
      </button>
    </div>

    <!-- Weekday Headers -->
    <div class="grid grid-cols-7 gap-2 mb-2">
      <div
        v-for="day in weekDays"
        :key="day"
        class="text-center text-sm font-semibold text-gray-600 dark:text-gray-400 py-2"
      >
        {{ day }}
      </div>
    </div>

    <!-- Calendar Grid -->
    <div class="grid grid-cols-7 gap-2">
      <div
        v-for="(day, index) in calendarDays"
        :key="index"
        :class="[
          'min-h-[100px] p-2 border rounded-lg transition-colors',
          day.isCurrentMonth
            ? 'bg-white dark:bg-gray-800 border-gray-200 dark:border-gray-700'
            : 'bg-gray-50 dark:bg-gray-900 border-gray-100 dark:border-gray-800',
          isToday(day.fullDate) && 'ring-2 ring-primary-500'
        ]"
      >
        <!-- Date Number -->
        <div
          :class="[
            'text-sm font-medium mb-1',
            day.isCurrentMonth
              ? 'text-gray-900 dark:text-white'
              : 'text-gray-400 dark:text-gray-600',
            isToday(day.fullDate) && 'text-primary-600 dark:text-primary-400 font-bold'
          ]"
        >
          {{ day.date }}
        </div>

        <!-- Bookings for this date -->
        <div class="space-y-1">
          <div
            v-for="booking in getBookingsForDate(day.fullDate).slice(0, 3)"
            :key="booking.id"
            @click="emit('view-booking', booking)"
            :class="[
              'text-xs p-1 rounded cursor-pointer hover:opacity-80 transition-opacity',
              'text-white truncate',
              getStatusColor(booking.status)
            ]"
            :title="`${booking.resourceName} - ${booking.memberName}`"
          >
            {{ booking.resourceName }}
          </div>
          <div
            v-if="getBookingsForDate(day.fullDate).length > 3"
            class="text-xs text-gray-500 dark:text-gray-400 text-center"
          >
            +{{ getBookingsForDate(day.fullDate).length - 3 }} khác
          </div>
        </div>
      </div>
    </div>

    <!-- Legend -->
    <div class="flex items-center gap-4 mt-6 pt-4 border-t border-gray-200 dark:border-gray-700">
      <div class="text-sm font-medium text-gray-700 dark:text-gray-300">Trạng thái:</div>
      <div class="flex items-center gap-2">
        <div class="w-3 h-3 rounded bg-yellow-400"></div>
        <span class="text-xs text-gray-600 dark:text-gray-400">Chờ thanh toán</span>
      </div>
      <div class="flex items-center gap-2">
        <div class="w-3 h-3 rounded bg-green-400"></div>
        <span class="text-xs text-gray-600 dark:text-gray-400">Đã xác nhận</span>
      </div>
      <div class="flex items-center gap-2">
        <div class="w-3 h-3 rounded bg-blue-400"></div>
        <span class="text-xs text-gray-600 dark:text-gray-400">Hoàn thành</span>
      </div>
      <div class="flex items-center gap-2">
        <div class="w-3 h-3 rounded bg-red-400"></div>
        <span class="text-xs text-gray-600 dark:text-gray-400">Đã hủy</span>
      </div>
    </div>
  </div>
</template>
