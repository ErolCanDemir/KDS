import { test, expect } from './fixtures';

// FLAGSHIP (Task 2 — "editör ile ve sürükle-bırak ile sorgu oluştur, sonuçları kıyasla"):
// Aynı mantıksal sorgu iki bağımsız yolla oluşturulur ve YÜRÜTME sonuçları kıyaslanır:
//   A) Sürükle-bırak tasarımcı → model → POST /api/sorgu/calistir
//   B) SQL editörü → ham SQL     → POST /api/sorgu/ham
// Örnek: KDSDemoHarita3 haritasındaki Markalar.Ad (KdsDemo, şema "ortak"). İki yanıtın `satirlar`
// dizisi API'den yakalanıp sıralamadan bağımsız kıyaslanır. Aynı tabloya iki yol → aynı satırlar.
// (Fixture 2026-08-13: eski "harita1"/EdinmeTurleri demo kurulumu servis senaryosuyla değişti.)
test('sürükle-bırak sonucu = SQL editör sonucu (Markalar.Ad)', async ({ page }) => {
  // --- A) Sürükle-bırak tasarımcı ---
  await page.goto('/tasarimci');
  await expect(page.getByText('Sorgu Tasarımcısı').first()).toBeVisible({ timeout: 20_000 });

  await page.getByRole('combobox').first().click();
  await page.getByRole('option', { name: 'KDSDemoHarita3', exact: true }).click();

  // Tabloyu aç → kolonu tıkla (kanvasa ekler).
  await page.getByText('Markalar', { exact: true }).click();
  await page.getByText('Ad', { exact: true }).first().click();

  // Son adım → Çalıştır; /api/sorgu/calistir yanıtını yakala.
  await page.getByRole('button', { name: /Sonuç & SQL/ }).click();
  const [calistir] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/api/sorgu/calistir') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Çalıştır' }).click(),
  ]);
  expect(calistir.status(), await calistir.text()).toBe(200);
  const dragRows = ((await calistir.json()).satirlar as unknown[][]).map((r) => String(r[0]));

  // --- B) SQL editörü (eşdeğer ham SQL, KDSDemoHarita3'ün bağlantısı = KDSDemo) ---
  await page.goto('/sql');
  await expect(page.getByText('SQL Editörü').first()).toBeVisible({ timeout: 20_000 });

  await page.getByRole('combobox').first().click();
  await page.getByRole('option', { name: 'KDSDemo', exact: true }).click();

  const editor = page.locator('.monaco-editor').first();
  await expect(editor).toBeVisible({ timeout: 20_000 });
  await editor.click();
  await page.keyboard.press('Control+A');
  await page.keyboard.insertText('SELECT [Ad] FROM [ortak].[Markalar]');

  const [ham] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/api/sorgu/ham') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Çalıştır' }).click(),
  ]);
  expect(ham.status(), await ham.text()).toBe(200);
  const sqlRows = ((await ham.json()).satirlar as unknown[][]).map((r) => String(r[0]));

  // --- Kıyas: aynı satır kümesi (sıralamadan bağımsız) ---
  expect(dragRows.length, 'sürükle-bırak sonucu boş').toBeGreaterThan(0);
  expect([...dragRows].sort()).toEqual([...sqlRows].sort());
});
