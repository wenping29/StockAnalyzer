import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      // '/api': { target: 'http://43.142.147.244:9999', changeOrigin: true },
      '/api': { target: 'http://localhost:5000', changeOrigin: true },
    },
  },
})
