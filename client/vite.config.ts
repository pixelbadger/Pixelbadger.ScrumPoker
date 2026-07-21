import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Production build lands in the ASP.NET Core project's wwwroot, which serves the SPA.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../src/Pixelbadger.ScrumPoker.Web/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5080',
      '/hubs': { target: 'http://localhost:5080', ws: true },
    },
  },
});
