import { defineConfig } from 'vitest/config';

// Frontend birim testleri (kod incelemesi: "frontend 0 test" borcu). Saf çekirdek (tasarimci/model)
// node ortamında koşar — DOM/render gerekmez; JSX'li ortak modüller otomatik runtime ile derlenir.
export default defineConfig({
  esbuild: { jsx: 'automatic' },
  test: { environment: 'node', include: ['src/**/*.test.ts'] },
});
