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
          if (url === '/new/ver1' || url === '/new/ver1/') {
            req.url = '/new/ver1/index.html';
          } else if (url === '/new/ver2' || url === '/new/ver2/') {
            req.url = '/new/ver2/index.html';
          } else if (url === '/new/ver3' || url === '/new/ver3/') {
            req.url = '/new/ver3/index.html';
          } else if (url === '/new/ver4' || url === '/new/ver4/') {
            req.url = '/new/ver4/index.html';
          } else if (
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
