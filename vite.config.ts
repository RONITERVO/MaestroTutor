import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import type { Plugin } from 'vite';
import { COLOR_GROUPS } from './src/features/theme/config/colorRegistry';
import { ORIGINAL_COLORS } from './src/features/theme/config/themeColors';

/**
 * Generates the CSS custom-property block for :root from COLOR_GROUPS + ORIGINAL_COLORS.
 * Replaces the /* __COLOR_TOKENS__ *\/ marker in index.css at build/dev time.
 *
 * To add a new token:
 *   1. Add the value to ORIGINAL_COLORS in themeColors.ts
 *   2. Add the metadata entry to colorRegistry.ts (cssVar, friendlyName, description, group)
 *   — CSS regenerates automatically on next build or dev-server start.
 *   — JS fallback in useApplyCustomColors applies the new token immediately via HMR.
 */
function colorTokensPlugin(): Plugin {
  const lines: string[] = [];
  for (const group of COLOR_GROUPS) {
    lines.push(`    /* ── ${group.groupName} ── */`);
    for (const color of group.colors) {
      const val = ORIGINAL_COLORS[color.cssVar];
      if (val !== undefined) lines.push(`    --${color.cssVar}: ${val};`);
    }
    lines.push('');
  }
  const generated = lines.join('\n').trimEnd();

  return {
    name: 'color-tokens',
    transform(code, id) {
      if (!id.includes('/src/app/index.css')) return;
      return code.replace('    /* __COLOR_TOKENS__ */', generated);
    },
  };
}

export default defineConfig(({ }) => ({
  plugins: [colorTokensPlugin(), react()],
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
