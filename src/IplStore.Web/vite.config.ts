import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // The browser talks to the Vite dev server; /api is forwarded to the .NET API (no CORS needed in dev).
    proxy: { '/api': { target: 'http://localhost:5212', changeOrigin: true } }
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false
  }
});
