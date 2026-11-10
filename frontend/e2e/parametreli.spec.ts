import { test, expect } from './fixtures';

// Parametreli rapor uçtan uca: tasarımcıda parametre tanımla + filtreyi ona bağla → kaydet →
// görüntüleyicide parametre paneli çıkar, değer verip Uygula → çalışır. (KDSDemoHarita3 = KdsDemo.)
test('parametreli rapor: tasarımcıda tanımla → görüntüleyicide değer ver → çalıştır', async ({ page }) => {
  test.setTimeout(120_000);

  // --- Tasarımcı ---
  await page.goto('/tasarimci');
  await expect(page.getByText('Sorgu Tasarımcısı').first()).toBeVisible({ timeout: 20_000 });
  await page.getByRole('combobox').first().click();
  await page.getByRole('option', { name: 'KDSDemoHarita3', exact: true }).click();
  await page.getByText('Markalar', { exact: true }).click();
  await page.getByText('Ad', { exact: true }).first().click();

  // Kolonlar & Ölçüler adımı (filtreler + parametreler burada).
  await page.getByRole('button', { name: /Kolonlar & Ölçüler/ }).click();

  // Parametre ekle → etiket "Arama" (tip Metin varsayılan).
  await page.getByRole('button', { name: 'Parametre ekle' }).click();
  const etiket = page.getByPlaceholder('Etiket').first();
  await etiket.fill('Arama');

  // Filtre ekle → kolon Ad → op "içerir" → değeri "⟶ Arama" parametresine bağla.
  await page.getByRole('button', { name: 'Filtre ekle' }).click();
  const filtreSatiri = page.locator('div').filter({ has: page.getByRole('button', { name: 'Filtreyi kaldır' }) }).last();
  await filtreSatiri.getByRole('combobox').first().click();
  await page.getByRole('option', { name: 'Markalar · Ad', exact: true }).click();
  await filtreSatiri.getByRole('combobox').nth(1).click();
  await page.getByRole('option', { name: 'içerir', exact: true }).click();
  await filtreSatiri.getByRole('combobox').last().click();
  await page.getByRole('option', { name: /Arama/ }).click();

  // Sonuç adımı → parametre paneli çıkar → değer ver → Çalıştır.
  await page.getByRole('button', { name: /Sonuç & SQL/ }).first().click();
  await expect(page.getByText('Parametre değerleri (test)')).toBeVisible({ timeout: 10_000 });
  await page.getByText('Arama', { exact: true }).locator('..').getByRole('textbox').fill('a');
  const [calistir] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/api/sorgu/calistir') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Çalıştır' }).click(),
  ]);
  expect(calistir.status(), await calistir.text()).toBe(200);
  // Gövdede parametre değeri gitti mi (wire).
  const govde = JSON.parse(calistir.request().postData() || '{}');
  expect(govde.parametreDegerleri, 'parametre değerleri gönderilmedi').toBeTruthy();

  // --- Kaydet ---
  const [kaydet] = await Promise.all([
    page.waitForResponse((r) => r.url().endsWith('/api/raporlar') && r.request().method() === 'POST'),
    (async () => {
      await page.getByRole('button', { name: 'Rapor Olarak Kaydet' }).click();
      const d = page.getByRole('dialog');
      await d.getByPlaceholder('ör. Şehir bazında satış').fill('E2E Parametreli');
      await d.getByRole('button', { name: 'Kaydet' }).click();
    })(),
  ]);
  expect(kaydet.status(), await kaydet.text()).toBe(200);
  const raporId = (await kaydet.json()).id as string;

  // --- Görüntüleyici: parametre paneli + Uygula ---
  await page.goto(`/raporlar/${raporId}`);
  await expect(page.getByText('Arama').first()).toBeVisible({ timeout: 15_000 });
  const [rapKos] = await Promise.all([
    page.waitForResponse((r) => r.url().includes(`/api/raporlar/${raporId}/calistir`) && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Uygula' }).click(),
  ]);
  expect(rapKos.status(), await rapKos.text()).toBe(200);
});
