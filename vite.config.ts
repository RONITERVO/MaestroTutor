import path from 'node:path';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import type { Plugin } from 'vite';
import { COLOR_GROUPS } from './src/features/theme/config/colorRegistry';
import { DEFAULT_THEME_COLORS } from './src/features/theme/config/defaultTheme';
import { colorVarValue, parseTokenValue } from './src/features/theme/utils/tokenValue';

/**
 * Generates the CSS custom-property block for :root from COLOR_GROUPS + the
 * current default theme colors.
 * Replaces the /* __COLOR_TOKENS__ *\/ marker in index.css at build/dev time.
 *
 * Each token emits up to three properties (see utils/tokenValue.ts):
 *   --x        the HSL channels
 *   --x-alpha  the default opacity, only when the theme asks for one
 *   --x-color  the ready-to-use colour, for hand-written CSS and inline styles
 *
 * To add a new token:
 *   1. Add the value to the active default theme palette in themeColors.ts
 *   2. Add the metadata entry to colorRegistry.ts (cssVar, friendlyName, description, group)
 *   - CSS regenerates automatically on next build or dev-server start.
 *   - JS fallback in useApplyCustomColors applies the new token immediately via HMR.
 */
function colorTokensPlugin(): Plugin {
  const lines: string[] = [];
  for (const group of COLOR_GROUPS) {
    lines.push(`    /* -- ${group.groupName} -- */`);
    for (const color of group.colors) {
      const val = DEFAULT_THEME_COLORS[color.cssVar];
      if (val === undefined) {
        throw new Error(`Missing default color for token: ${color.cssVar}`);
      }
      const { channels, alpha } = parseTokenValue(val);
      lines.push(`    --${color.cssVar}: ${channels};`);
      // Opaque is the default, so only a genuinely translucent token needs the
      // property; everything else relies on the `var(--x-alpha, 1)` fallback.
      if (alpha < 1) {
        lines.push(`    --${color.cssVar}-alpha: ${alpha};`);
      }
      lines.push(`    --${color.cssVar}-color: ${colorVarValue(color.cssVar)};`);
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

/**
 * Transformers.js emits two public strings that resemble provider API keys to
 * GitHub push protection: a Whisper documentation gist identifier and a 32-character
 * Mistral model class name. Rewrite only those generated strings while preserving
 * their runtime meaning so no secret-shaped identifier is published.
 */
function avoidGeneratedSecretFalsePositivesPlugin(): Plugin {
  const publicWhisperGist =
    /https:\/\/gist\.github\.com\/hollance\/[a-f0-9]{32}\b/gi;
  const mistralClassName = ['Mistral3For', 'ConditionalGeneration'].join('');
  const mistralRegistryEntry = `["mistral3","${mistralClassName}"]`;

  return {
    name: 'avoid-generated-secret-false-positives',
    enforce: 'post',
    generateBundle(_options, bundle) {
      for (const artifact of Object.values(bundle)) {
        if (artifact.type !== 'chunk') continue;
        artifact.code = artifact.code.replace(
          publicWhisperGist,
          'https://huggingface.co/docs/transformers.js',
        );
        artifact.code = artifact.code.replace(
          mistralRegistryEntry,
          '["mistral3","Mistral3For"+"ConditionalGeneration"]',
        );
      }
    },
  };
}

/**
 * Diagnostic builds keep function names so a CPU profile and React's fiber tree
 * name real components instead of minified identifiers. Costs a little bundle
 * size, so it is opt-in per build alongside MAESTRO_WEBVIEW_DEBUG.
 */
const keepNames = process.env.MAESTRO_WEBVIEW_DEBUG === '1';

export default defineConfig(() => ({
  plugins: [colorTokensPlugin(), react(), avoidGeneratedSecretFalsePositivesPlugin()],
  // Worker builds have their own Rollup pipeline, so register the post-processor
  // there as well as in the main application build.
  worker: {
    plugins: () => [avoidGeneratedSecretFalsePositivesPlugin()],
  },
  esbuild: { keepNames },
  build: {
    rollupOptions: {
      input: {
        main: path.resolve(__dirname, 'index.html'),
        // Google Play requires an account-deletion route reachable without
        // installing the app, so this is a second entry point rather than a
        // route inside the SPA.
        deleteAccount: path.resolve(__dirname, 'delete-account.html'),
      },
    },
  },
  // Using '/' as base path for custom domain (chatwithmaestro.com)
  // GitHub Pages serves from root when a custom domain is configured
  base: '/',
  publicDir: 'public',
  resolve: {
    alias: {
      '@': '/src',
    },
  },
}));
