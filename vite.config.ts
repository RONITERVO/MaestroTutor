import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => ({
  plugins: [react()],
  // Using '/' as base path for custom domain (chatwithmaestro.com)
  // GitHub Pages serves from root when a custom domain is configured
  base: '/',
  publicDir: 'public',
  resolve: {
    alias: {
      '@': '/src'
    }
  }
}));
