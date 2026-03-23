/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{vue,js,ts,jsx,tsx}",
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        // Soft Sky Blue theme
        sky: {
          50: '#eaf6ff',
          100: '#d6eeff',
          200: '#b5e2ff',
          300: '#86d0f5',
          400: '#84cdee',
          500: '#4db5e6',
          600: '#2a9cd4',
          700: '#1e7eb0',
          800: '#1c6690',
          900: '#1c5476',
          950: '#133549',
        },
        primary: {
          50: '#eaf6ff',
          100: '#d6eeff',
          200: '#b5e2ff',
          300: '#86d0f5',
          400: '#84cdee',
          500: '#4db5e6',
          600: '#2a9cd4',
          700: '#1e7eb0',
          800: '#1c6690',
          900: '#1c5476',
          950: '#133549',
        }
      },
      animation: {
        'fade-in': 'fadeIn 0.3s ease-in-out',
        'slide-up': 'slideUp 0.3s ease-out',
        'slide-down': 'slideDown 0.3s ease-out',
      },
      keyframes: {
        fadeIn: {
          '0%': { opacity: '0' },
          '100%': { opacity: '1' },
        },
        slideUp: {
          '0%': { transform: 'translateY(10px)', opacity: '0' },
          '100%': { transform: 'translateY(0)', opacity: '1' },
        },
        slideDown: {
          '0%': { transform: 'translateY(-10px)', opacity: '0' },
          '100%': { transform: 'translateY(0)', opacity: '1' },
        },
      },
    },
  },
  plugins: [],
}
