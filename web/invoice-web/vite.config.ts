import { defineConfig } from 'vitest/config';
import vue from '@vitejs/plugin-vue';

export default defineConfig({
  plugins: [vue()],
  test: {
    environment: 'jsdom',
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json-summary', 'html'],
      include: ['src/**/*.{ts,vue}'],
      exclude: ['src/generated/**', 'src/main.ts'],
      thresholds: {
        statements: 35,
        branches: 30,
        functions: 25,
        lines: 38,
      },
    },
  },
});
