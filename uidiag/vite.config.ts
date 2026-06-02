import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'node:path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5218',
        changeOrigin: true,
      },
      '/diagnosticHub': {
        target: 'http://localhost:5218',
        changeOrigin: true,
        ws: true,
      },
    },
  },
  build: {
    outDir: '../DiagExperimentsSolution/DiagnosticServer/wwwroot',
    emptyOutDir: true,
    chunkSizeWarningLimit: 1200,
    rolldownOptions: {
      onLog(level, log) {
        // Suppress INVALID_ANNOTATION warnings from third-party signalr package
        if (log.code === 'INVALID_ANNOTATION') return
      },
    },
  },
})
