/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        harness: {
          dark: '#0B0F17',
          surface: '#111827',
          card: '#1F2937',
          border: '#374151',
          accent: '#3B82F6',
          success: '#10B981',
          warning: '#F59E0B',
          danger: '#EF4444',
          muted: '#9CA3AF'
        }
      }
    },
  },
  plugins: [],
}
