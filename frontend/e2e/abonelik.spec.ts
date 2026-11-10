import { test, expect } from './fixtures';

// Zamanlanmış Raporlar (rapor aboneliği) uçtan uca: bir rapor oluştur → abonelik kur (rapor + biçim +
// alıcı) → "Şimdi Gönder" → gönderim başarılı (dev'de e-posta sunucu günlüğüne yazılır). (KDSDemoHarita3 = KdsDemo.)
test('abonelik: rapor oluştur → abone ol → Şimdi Gönder → başarılı', async ({ page }) => {
  test.setTimeout(120_000);

  // --- 1) Basit bir rapor (tasarımcı) ---
  await page.goto('/tasarimci');
  await expect(page.getByText('Sorgu Tasarımcısı').first()).toBeVisible({ timeout: 20_000 });
  await page.getByRole('combobox').first().click();
  await page.getByRole('option', { name: 'KDSDemoHarita3', exact: true }).click();
  await page.getByText('Markalar', { exact: true }).click();
  await page.getByText('Ad', { exact: true }).first().click();
  await page.getByRole('button', { name: /Sonuç & SQL/ }).first().click();

  const raporAd = 'E2E Abonelik Rapor';
  await Promise.all([
    page.waitForResponse((r) => r.url().endsWith('/api/raporlar') && r.request().method() === 'POST'),
    (async () => {
      await page.getByRole('button', { name: 'Rapor Olarak Kaydet' }).click();
      const d = page.getByRole('dialog');
      await d.getByPlaceholder('ör. Şehir bazında satış').fill(raporAd);
      await d.getByRole('button', { name: 'Kaydet' }).click();
    })(),
  ]);

  // --- 2) Abonelik kur ---
  await page.goto('/zamanlanmis-raporlar');
  await expect(page.getByText('Zamanlanmış Raporlar').first()).toBeVisible({ timeout: 20_000 });
  await page.getByRole('button', { name: 'Yeni Abonelik' }).first().click();

  await page.getByLabel('Abonelik adı').fill('E2E Abonelik');
  // Rapor seç (çekmecedeki tek combobox) → yeni oluşturulan rapor.
  await page.getByRole('combobox').click();
  await page.getByRole('option', { name: raporAd }).first().click();
  // Alıcı ekle (Enter ile).
  await page.getByPlaceholder('e-posta ekle (Enter)').fill('e2e@firma.com');
  await page.getByPlaceholder('e-posta ekle (Enter)').press('Enter');
  await expect(page.getByText('e2e@firma.com')).toBeVisible();

  const [kaydet] = await Promise.all([
    page.waitForResponse((r) => r.url().endsWith('/api/abonelikler') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Kaydet' }).click(),
  ]);
  expect(kaydet.status(), await kaydet.text()).toBe(200);

  // --- 3) Kart göründü → Şimdi Gönder → başarılı ---
  await expect(page.getByText('E2E Abonelik').first()).toBeVisible({ timeout: 15_000 });
  // Düğme testin KENDİ kartından seçilir. Önce `.first()` kullanılıyordu ve liste son gönderime
  // göre sıralı olduğu için BAŞKA bir aboneliğe basabiliyordu — test kendi kurduğunu değil
  // rastgele bir kaydı sınıyordu. 20 Ağu 2026'da ortaya çıktı: eski kayıtlar (sahip kimliği
  // saklanmadan önce kurulmuş) RLS kapısına takılınca test düştü; hata gerçekti ama testin
  // ölçtüğü şey yanlıştı.
  const kart = page.locator('.MuiPaper-root').filter({ hasText: 'E2E Abonelik' }).first();
  const [gonder] = await Promise.all([
    page.waitForResponse((r) => /\/api\/abonelikler\/[0-9a-fA-F-]+\/gonder$/.test(r.url()) && r.request().method() === 'POST'),
    kart.getByRole('button', { name: 'Şimdi Gönder' }).click(),
  ]);
  expect(gonder.status(), await gonder.text()).toBe(200);
  const sonuc = await gonder.json();
  expect(sonuc.aliciSayisi).toBe(1);
  await expect(page.getByText(/gönderildi/).first()).toBeVisible({ timeout: 10_000 });
});
