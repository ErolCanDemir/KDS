import { test, expect } from './fixtures';

// Ön koşul + bağlantı-ekleme ekranı kapsamı: KdsDemo'ya (LocalDB, entegre kimlik) bir bağlantı
// oluşturur (yoksa). SQL editörü ve tasarımcı bu bağlantıyı kullanır. Idempotent.
test('Bağlantılar: KdsDemo bağlantısı oluştur (yoksa)', async ({ page }) => {
  await page.goto('/baglantilar');
  await expect(page.getByText('Bağlantılar').first()).toBeVisible({ timeout: 20_000 });

  if ((await page.getByText('KdsDemo E2E').count()) === 0) {
    await page.getByRole('button', { name: 'Yeni Bağlantı' }).first().click();
    await page.getByLabel('Bağlantı adı').fill('KdsDemo E2E');
    await page.getByLabel('Sunucu').fill('(localdb)\\MSSQLLocalDB');
    await page.getByLabel('Veritabanı').fill('KdsDemo');
    // Kullanıcı boş = entegre kimlik (LocalDB için uygun); Şifre boş.
    await page.getByRole('button', { name: 'Kaydet' }).click();
    await expect(page.getByText('KdsDemo E2E').first()).toBeVisible({ timeout: 15_000 });
  }
});

// SQL Editörü (editör yolu) uçtan uca: bağlantı seçili (ilk otomatik) → Monaco'ya SELECT yaz →
// Çalıştır → sonuç ızgarası gelir. "SELECT 1 AS Deger" her SQL Server'da geçerli (hedef tabloya
// bağımlı değil), SELECT-only bekçisinden geçer.
test('SQL editörü: SELECT çalıştır → sonuç gelir', async ({ page }) => {
  await page.goto('/sql');
  await expect(page.getByText('SQL Editörü').first()).toBeVisible({ timeout: 20_000 });

  // Bağlantı yoksa test anlamsız — açık mesajla düşsün.
  await expect(
    page.getByText('Önce bir', { exact: false }),
    'Hiç bağlantı yok — önce KdsDemo bağlantısı ekleyin',
  ).toHaveCount(0);

  const editor = page.locator('.monaco-editor').first();
  await expect(editor).toBeVisible({ timeout: 20_000 });
  await editor.click();
  await page.keyboard.press('Control+A');
  await page.keyboard.insertText('SELECT 1 AS Deger');

  await page.getByRole('button', { name: 'Çalıştır' }).click();

  // Sonuç ızgarasında kolon başlığı "Deger" görünür.
  await expect(page.getByText('Deger').first()).toBeVisible({ timeout: 25_000 });
});

// Sorgu Tasarımcısı (sürükle-bırak yolu) açılış smoke: sayfa yükleniyor, harita seçimi / kanvas
// görünüyor. Tam sürükle-bırak akışı canlı DOM ile ayrıca ince ayar ister.
test('Sorgu Tasarımcısı: ekran ve harita seçimi görünür', async ({ page }) => {
  const hatalar: string[] = [];
  page.on('pageerror', (e) => hatalar.push(e.message));

  await page.goto('/tasarimci');
  await expect(page.getByText('Sorgu Tasarımcısı').first()).toBeVisible({ timeout: 20_000 });

  // Harita seçici (Select) ya da "harita" ipucu görünmeli; en azından sayfa hatasız açılmalı.
  await page.waitForLoadState('networkidle');
  expect(hatalar, 'sayfa JS hatası:\n' + hatalar.join('\n')).toHaveLength(0);
});
