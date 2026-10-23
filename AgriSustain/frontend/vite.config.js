import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

const apiOrigin = process.env.AGRI_API_ORIGIN || 'http://127.0.0.1:5179';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    host: '127.0.0.1',
    port: Number(process.env.AGRI_WEB_PORT || 5173),
    strictPort: true,
    proxy: { '/api': apiOrigin, '/health': apiOrigin },
  },
});
