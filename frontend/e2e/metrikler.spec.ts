import { test, expect } from './fixtures';

// S22 Metrikler — uçtan uca: katalogda metrik kur (formül doğrulamalı) → listede formülüyle görün
// → Ölç değeri hesaplasın → sil. Tasarımcı entegrasyonu (referans çözümü, kırılım, tek doğruluk
// kaynağı) birim testler + canlı API doğrulamasıyla kanıtlı; buradaki risk EKRAN hattı.

const AD = 'E2E Toplam Tutar';

test('metrik: kur → katalogda görün → Ölç → sil', async ({ page }) => {
  test.setTimeout(90_000);

  await page.goto('/metrikler');
  await expect(page.getByRole('heading', { name: 'Metrikler' })).toBeVisible({ timeout: 20_000 });

  // Retry güvenliği: ad kiracıda BENZERSİZ (bilinçli ürün kuralı) — önceki denemeden kalan aynı
  // adlı kayıt kaydetmeyi düşürür; varsa önce temizle.
  const eski = page.locator('.MuiPaper-root').filter({ hasText: AD })
    .filter({ has: page.getByRole('button', { name: 'Ölç' }) });
  if (await eski.count()) {
    await eski.first().locator('button[title="Sil"]').click();
    await page.getByRole('button', { name: 'Sil', exact: true }).last().click();
    await expect(eski).toHaveCount(0, { timeout: 15_000 });
  }

  // --- Kur ---
  await page.getByRole('button', { name: 'Yeni Metrik' }).first().click();
  const d = page.locator('.MuiDrawer-paper');
  await d.getByLabel('Metrik adı').fill(AD);
  await d.getByRole('combobox').first().click();
  await page.getByRole('option', { name: 'KDSDemoHarita3', exact: true }).click();
  await d.getByLabel(/Formül/).fill('SUM({Talepler.ToplamTutar})');
  // Biçim: Para — Ölç sonrası değerin ₺ ile biçimlenmesini de sınarız.
  await d.getByRole('combobox').nth(1).click();
  await page.getByRole('option', { name: '₺ Para' }).click();
  await d.getByRole('button', { name: 'Kaydet' }).click();

  // Katalogda: ad + formül cümle gibi okunur.
  const kart = page.locator('.MuiPaper-root')
    .filter({ hasText: AD })
    .filter({ has: page.getByRole('button', { name: 'Ölç' }) })
    .first();
  await expect(kart).toBeVisible({ timeout: 15_000 });
  await expect(kart.getByText('SUM({Talepler.ToplamTutar})')).toBeVisible();
  await expect(kart.getByText('henüz ölçülmedi')).toBeVisible();

  // --- Ölç: gerçek değer hesaplanır (normal koşum hattı: RLS/sınır/denetim uygulanır) ---
  await kart.getByRole('button', { name: 'Ölç' }).click();
  await expect(kart.getByText(/ms'de ölçüldü/)).toBeVisible({ timeout: 60_000 });
  // Para biçimi: değer ₺ ile biter (sayının kendisi veriye bağlı, biçim sabittir).
  await expect(kart.getByText(/₺/).first()).toBeVisible();

  // --- Sil (kendi kartından; teardown zaten "E2E " önekini süpürür ama silme yolu da sınanır) ---
  await kart.locator('button[title="Sil"]').click();
  await page.getByRole('button', { name: 'Sil', exact: true }).last().click();
  await expect(kart).toHaveCount(0, { timeout: 15_000 });
});
