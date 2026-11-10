import { test, expect } from './fixtures';

// Admin oturumunun rolleri istemci tarafında doğru çözülüyor mu. Roller boşsa rol-korumalı menü
// grupları (Oluştur/Veri/Yönetim) ve "Yeni Pano" gibi yetki-koşullu UI GİZLENİR.

test('admin: rol-korumalı menü öğeleri görünür (Oluştur/Veri grupları)', async ({ page }) => {
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  const yanNav = page.getByRole('complementary');
  // Bu öğeler roller: [gelistirici, admin] gerektirir → admin görmeli.
  await expect(yanNav.getByText('Sorgu Tasarımcısı')).toBeVisible({ timeout: 10_000 });
  await expect(yanNav.getByText('SQL Editörü')).toBeVisible();
  await expect(yanNav.getByText('Veri Haritaları')).toBeVisible();
  await expect(yanNav.getByText('Bağlantılar')).toBeVisible();
});

test('admin: Panolar ekranında "Yeni Pano" butonu görünür', async ({ page }) => {
  await page.goto('/panolar');
  await expect(page.getByRole('heading', { name: 'Panolar' })).toBeVisible({ timeout: 10_000 });
  await expect(page.getByRole('button', { name: 'Yeni Pano' })).toBeVisible({ timeout: 10_000 });
});
