import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [
    react(),
    {
      name: 'spa-app-html-fallback',
      configureServer(server) {
        server.middlewares.use((req, _res, next) => {
          const url = req.url ? req.url.split('?')[0] : '/';
          if (
            url === '/' ||
            url === '/index.html' ||
            url === '/architectures' ||
            url === '/architectures.html' ||
            url === '/ontology' ||
            url === '/ontology.html' ||
            url === '/benchmarks' ||
            url === '/benchmarks.html' ||
            url === '/privacy' ||
            url === '/privacy.html' ||
            url.startsWith('/benchmarks/0')
          ) {
            req.url = '/app.html';
          }
          next();
        });
      },
    },
  ],
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    cssMinify: false,
    rollupOptions: {
      input: 'app.html',
    },
  },
});
