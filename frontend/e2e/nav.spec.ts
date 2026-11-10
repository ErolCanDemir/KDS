import { test, expect } from './fixtures';

// Sol menü linkleri doğru rotaya gidip ekranı açıyor mu (menü yolu ↔ rota tutarlılığı regresyon
// kontrolü). Not: linkin erişilebilir-ADI `aciklama` (tooltip); görünür ETİKETLE (hasText) seçilir.
// Rol-korumalı öğeler (Bağlantılar/Tasarımcı) whoami-roller düzeldiği için artık görünür.

test('menü: Panolar → /panolar', async ({ page }) => {
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await page.getByRole('complementary').getByRole('link').filter({ hasText: 'Panolar' }).first().click();
  await expect(page).toHaveURL(/\/panolar$/, { timeout: 10_000 });
  await expect(page.getByRole('heading', { name: 'Panolar' })).toBeVisible({ timeout: 10_000 });
});

test('menü: Bağlantılar → /baglantilar', async ({ page }) => {
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await page.getByRole('complementary').getByRole('link').filter({ hasText: 'Bağlantılar' }).first().click();
  await expect(page).toHaveURL(/\/baglantilar$/, { timeout: 10_000 });
  await expect(page.getByRole('heading', { name: 'Bağlantılar' })).toBeVisible({ timeout: 10_000 });
});

test('menü: Sorgu Tasarımcısı → /tasarimci', async ({ page }) => {
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await page.getByRole('complementary').getByRole('link').filter({ hasText: 'Sorgu Tasarımcısı' }).first().click();
  await expect(page).toHaveURL(/\/tasarimci$/, { timeout: 10_000 });
});

test('menü: Raporlar → /raporlar', async ({ page }) => {
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  // Raporlar alt-menü butonu; tıkla, gerekirse "Tüm Raporlar" alt öğesine geç.
  await page.getByRole('complementary').getByText('Raporlar', { exact: true }).first().click();
  const tum = page.getByRole('complementary').getByText('Tüm Raporlar', { exact: true });
  if (await tum.count()) await tum.first().click();
  await expect(page).toHaveURL(/\/raporlar$/, { timeout: 10_000 });
  await expect(page.getByRole('heading', { name: 'Raporlar' })).toBeVisible({ timeout: 10_000 });
});
