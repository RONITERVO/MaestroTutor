import { configDefaults, defineConfig, mergeConfig } from 'vitest/config';
import viteConfig from './vite.config';

export default defineConfig(env => mergeConfig(viteConfig(env), {
  test: {
    // Local Quest evidence can contain copied diagnostic tests with private dependencies.
    // Keep the normal application suite independent of those ignored run artifacts.
    exclude: [...configDefaults.exclude, '**/.quest-evidence/**'],
  },
}));
