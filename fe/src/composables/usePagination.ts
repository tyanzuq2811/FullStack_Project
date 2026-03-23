import { computed, ref, watch, type ComputedRef, type Ref, type WatchSource } from 'vue'

interface UsePaginationOptions {
  pageSize?: number
  resetOn?: WatchSource | WatchSource[]
}

export function usePagination<T>(
  items: Ref<T[]> | ComputedRef<T[]>,
  options: UsePaginationOptions = {}
) {
  const pageSize = options.pageSize ?? 10
  const currentPage = ref(1)

  const totalPages = computed(() => Math.max(1, Math.ceil(items.value.length / pageSize)))

  const paginatedItems = computed(() => {
    const start = (currentPage.value - 1) * pageSize
    return items.value.slice(start, start + pageSize)
  })

  const pageNumbers = computed(() => Array.from({ length: totalPages.value }, (_, i) => i + 1))

  const setPage = (page: number) => {
    if (page < 1 || page > totalPages.value) return
    currentPage.value = page
  }

  watch(totalPages, (pages) => {
    if (currentPage.value > pages) {
      currentPage.value = pages
    }
  })

  if (options.resetOn) {
    watch(options.resetOn, () => {
      currentPage.value = 1
    })
  }

  return {
    pageSize,
    currentPage,
    totalPages,
    paginatedItems,
    pageNumbers,
    setPage
  }
}
