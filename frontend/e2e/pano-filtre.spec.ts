import { test, expect } from './fixtures';

// Pano-düzeyi filtreler uçtan uca: parametreli bir rapor tasarla+kaydet → panoya koy → pano
// görüntüleyicide raporun parametresi birleşik FİLTRE ŞERİDİ olarak çıkar → değer ver + "Uygula"
// → kartın /calistir çağrısı o parametre değerini taşır. (KDSDemoHarita3 = KdsDemo.)
test('pano-düzeyi filtre: parametreli rapor → panoya koy → şeritten değer ver → kart o değerle koşar', async ({ page }) => {
  test.setTimeout(120_000);

  // --- 1) Parametreli rapor (tasarımcı) ---
  await page.goto('/tasarimci');
  await expect(page.getByText('Sorgu Tasarımcısı').first()).toBeVisible({ timeout: 20_000 });
  await page.getByRole('combobox').first().click();
  await page.getByRole('option', { name: 'KDSDemoHarita3', exact: true }).click();
  await page.getByText('Markalar', { exact: true }).click();
  await page.getByText('Ad', { exact: true }).first().click();

  await page.getByRole('button', { name: /Kolonlar & Ölçüler/ }).click();
  await page.getByRole('button', { name: 'Parametre ekle' }).click();
  await page.getByPlaceholder('Etiket').first().fill('Arama');

  await page.getByRole('button', { name: 'Filtre ekle' }).click();
  const filtreSatiri = page.locator('div').filter({ has: page.getByRole('button', { name: 'Filtreyi kaldır' }) }).last();
  await filtreSatiri.getByRole('combobox').first().click();
  await page.getByRole('option', { name: 'Markalar · Ad', exact: true }).click();
  await filtreSatiri.getByRole('combobox').nth(1).click();
  await page.getByRole('option', { name: 'içerir', exact: true }).click();
  await filtreSatiri.getByRole('combobox').last().click();
  await page.getByRole('option', { name: /Arama/ }).click();

  // Sonuç adımına geç ("Rapor Olarak Kaydet" orada; çalıştırma şart değil, alan yeter).
  await page.getByRole('button', { name: /Sonuç & SQL/ }).first().click();

  // Kaydet
  const [kaydet] = await Promise.all([
    page.waitForResponse((r) => r.url().endsWith('/api/raporlar') && r.request().method() === 'POST'),
    (async () => {
      await page.getByRole('button', { name: 'Rapor Olarak Kaydet' }).click();
      const d = page.getByRole('dialog');
      await d.getByPlaceholder('ör. Şehir bazında satış').fill('E2E PanoFiltre Rapor');
      await d.getByRole('button', { name: 'Kaydet' }).click();
    })(),
  ]);
  expect(kaydet.status(), await kaydet.text()).toBe(200);
  const raporId = (await kaydet.json()).id as string;

  // --- 2) Panoya koy ---
  await page.goto('/panolar');
  await expect(page.getByRole('heading', { name: 'Panolar' })).toBeVisible({ timeout: 20_000 });
  await page.getByRole('button', { name: 'Yeni Pano' }).click();
  const pd = page.getByRole('dialog');
  await pd.getByLabel('Pano adı').fill('E2E PanoFiltre');
  await pd.getByText('E2E PanoFiltre Rapor').first().click();
  await pd.getByRole('button', { name: 'Kaydet' }).click();

  await expect(page.getByText('E2E PanoFiltre').first()).toBeVisible({ timeout: 15_000 });
  await page.getByText('E2E PanoFiltre').first().click();
  await expect(page).toHaveURL(/\/panolar\/[0-9a-fA-F-]+$/, { timeout: 10_000 });

  // --- 3) Filtre şeridi çıkar (raporun parametresi birleşik) ---
  await expect(page.getByText('Arama', { exact: true })).toBeVisible({ timeout: 20_000 });

  // --- 4) Değer ver + Uygula → kartın /calistir'i o değeri taşır ---
  await page.getByText('Arama', { exact: true }).locator('..').getByRole('textbox').fill('a');
  // Yalnız Uygula ile tetiklenen ('a' değeri taşıyan) isteği yakala — açılıştaki varsayılan (boş)
  // koşumu değil. Parametre anahtarı tasarımcıda üretilir (p1_<hex>) → değer üzerinden eşle.
  const [calistir] = await Promise.all([
    page.waitForResponse((r) => {
      if (!r.url().includes(`/api/raporlar/${raporId}/calistir`) || r.request().method() !== 'POST') return false;
      try { return Object.values(JSON.parse(r.request().postData() || '{}')).includes('a'); } catch { return false; }
    }),
    page.getByRole('button', { name: 'Uygula' }).click(),
  ]);
  expect(calistir.status()).toBe(200);
  const govde = JSON.parse(calistir.request().postData() || '{}');
  expect(Object.values(govde), 'pano filtresi kartın çalıştırmasına gitmedi').toContain('a');
});
