import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

export const useThemeStore = defineStore('theme', () => {
  const isDark = ref(false)

  const theme = computed(() => isDark.value ? 'dark' : 'light')

  function initTheme() {
    isDark.value = localStorage.getItem('theme') === 'dark' ||
      (!localStorage.getItem('theme') && window.matchMedia('(prefers-color-scheme: dark)').matches)
    updateTheme()
  }

  function toggleTheme() {
    isDark.value = !isDark.value
    updateTheme()
  }

  function setTheme(dark: boolean) {
    isDark.value = dark
    updateTheme()
  }

  function updateTheme() {
    if (isDark.value) {
      document.documentElement.classList.add('dark')
      localStorage.setItem('theme', 'dark')
    } else {
      document.documentElement.classList.remove('dark')
      localStorage.setItem('theme', 'light')
    }
  }

  return {
    isDark,
    theme,
    initTheme,
    toggleTheme,
    setTheme
  }
})
