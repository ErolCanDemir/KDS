import { defineConfig, devices } from '@playwright/test';

// LST-KDS E2E (gerçek tarayıcı). Ön koşul: yığın ayakta olmalı — Keycloak (8080), API (5210),
// Vite (5173). Kimlik, gerçek Keycloak login formuyla otomatikleştirilir (ROPC kapalı); token
// sessionStorage'da olduğundan setup projesi bir kez giriş yapıp oidc kullanıcısını dosyaya yazar,
// testler her sayfada bunu sessionStorage'a enjekte eder (bkz. e2e/fixtures.ts).
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,               // tek tenant/veri; sıralı koş
  // Demo DB büyük (2M+ satırlı tablolar); LocalDB'de aralıklı duraksama gerçek sorgularda tek
  // seferlik zaman aşımı yapabiliyor. 1 tekrar ortam takılmasını eler — gerçek regresyon iki
  // koşuda da düşer (raporda "flaky" ayrı görünür, sessizce yutulmaz).
  retries: 1,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'e2e-rapor' }]],
  use: {
    baseURL: process.env.KDS_ONYUZ ?? 'http://localhost:5173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    locale: 'tr-TR',
  },
  projects: [
    // setup'ın teardown'u, ona bağlı TÜM projeler bittikten sonra koşar → süit nasıl biterse bitsin
    // (yeşil ya da kırmızı) ürettiği "E2E " önekli kayıtlar silinir. Artıkların birikmesi 19 Ağu
    // 2026'da dev veritabanını gerçek verinin görünmez olduğu hâle getirmişti (58 raporun 42'si artık).
    { name: 'setup', testMatch: /auth\.setup\.ts/, teardown: 'temizlik' },
    { name: 'temizlik', testMatch: /temizlik\.teardown\.ts/ },
    { name: 'chromium', use: { ...devices['Desktop Chrome'] }, dependencies: ['setup'] },
  ],
});
