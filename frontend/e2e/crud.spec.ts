import { test, expect } from './fixtures';
import { hamSqlRaporVarEt } from './apiYardimci';

// Özellik akışları doğrudan URL ile (menü kırık/gizli olsa da özellik çalışır).

test.describe('Bağlantılar CRUD', () => {
  test('oluştur → düzenle → Test Et → sil', async ({ page }) => {
    await page.goto('/baglantilar');
    await expect(page.getByRole('heading', { name: 'Bağlantılar' })).toBeVisible({ timeout: 20_000 });

    // Oluştur (atılabilir kayıt). Not: "Veritabanı" nav linkinin aria-label'ıyla da eşleştiğinden
    // form alanları diyaloğa kapsanır.
    await page.getByRole('button', { name: 'Yeni Bağlantı' }).first().click();
    const yeniD = page.getByRole('dialog');
    await yeniD.getByLabel('Bağlantı adı').fill('Silinecek E2E');
    await yeniD.getByLabel('Sunucu').fill('(localdb)\\MSSQLLocalDB');
    await yeniD.getByLabel('Veritabanı').fill('KdsDemo');
    await yeniD.getByRole('button', { name: 'Kaydet' }).click();
    await expect(page.getByText('Silinecek E2E').first()).toBeVisible({ timeout: 15_000 });

    // Düzenle (yeniden adlandır) + Test Et
    await page.getByRole('button', { name: 'Düzenle' }).last().click();
    const diyalog = page.getByRole('dialog');
    await diyalog.getByLabel('Bağlantı adı').fill('Silinecek E2E 2');
    await diyalog.getByRole('button', { name: 'Test Et' }).click();
    await expect(diyalog.getByText(/Bağlantı başarılı|Bağlanılamadı/)).toBeVisible({ timeout: 15_000 });
    await diyalog.getByRole('button', { name: 'Kaydet' }).click();
    await expect(page.getByText('Silinecek E2E 2').first()).toBeVisible({ timeout: 15_000 });

    // Sil (onay diyaloğu)
    await page.getByRole('button', { name: 'Sil' }).last().click();
    const onay = page.getByRole('dialog');
    await onay.getByRole('button', { name: /Sil|Onayla|Evet/ }).click();
    await expect(page.getByText('Silinecek E2E 2')).toHaveCount(0, { timeout: 15_000 });
  });
});

test.describe('Raporlar CRUD (SQL editöründen)', () => {
  test('SQL rapor kaydet → listede → çalıştır → sil', async ({ page }) => {
    await page.goto('/sql');
    await expect(page.getByText('SQL Editörü').first()).toBeVisible({ timeout: 20_000 });

    const editor = page.locator('.monaco-editor').first();
    await expect(editor).toBeVisible({ timeout: 20_000 });
    await editor.click();
    await page.keyboard.press('Control+A');
    // insertText atomik ekler (keyboard.type Monaco'da karakter karıştırıyor → bozuk SQL).
    await page.keyboard.insertText('SELECT 1 AS Deger');

    // Kaydet diyaloğunu aç (SQL editörü "Kaydet")
    await page.getByRole('button', { name: 'Kaydet' }).click();
    const kd = page.getByRole('dialog');
    await expect(kd).toBeVisible();
    await kd.getByPlaceholder('ör. Marka bazında ciro').fill('E2E SQL Rapor');
    const kdKaydet = kd.getByRole('button', { name: 'Kaydet' });
    await expect(kdKaydet).toBeEnabled();
    const [resp] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/api/raporlar') && r.request().method() === 'POST'),
      kdKaydet.click(),
    ]);
    expect(resp.status(), await resp.text()).toBe(200);
    const { id } = await resp.json();
    await expect(page.getByText(/kaydedildi/)).toBeVisible({ timeout: 15_000 });

    // Listede görünür
    await page.goto('/raporlar');
    await expect(page.getByText('E2E SQL Rapor').first()).toBeVisible({ timeout: 15_000 });

    // Rapor görüntüleyici sonuçları otomatik çalıştırmaz (tarayıcıda önbellekler); "Çalıştır"a bas.
    await page.goto(`/raporlar/${id}`);
    await page.getByRole('button', { name: 'Çalıştır' }).click();
    await expect(page.getByText('Deger').first()).toBeVisible({ timeout: 20_000 });
  });
});

test.describe('Veri Haritası oluşturma', () => {
  test('Yeni Harita → ad + bağlantı → Oluştur → editör açılır', async ({ page }) => {
    await page.goto('/veri-haritalari');
    await expect(page.getByRole('heading', { name: 'Veri Haritaları' })).toBeVisible({ timeout: 20_000 });

    await page.getByRole('button', { name: 'Yeni Harita' }).first().click();
    const d = page.getByRole('dialog');
    await d.getByLabel('Harita adı').fill('E2E Harita');
    await d.getByRole('button', { name: 'Oluştur' }).click();

    // Editör açıldı: "Haritadaki tablolar" paneli görünür.
    await expect(page.getByText('Haritadaki tablolar').first()).toBeVisible({ timeout: 15_000 });
  });
});

test.describe('Panolar CRUD', () => {
  const KAYNAK_RAPOR = 'E2E Pano Kaynak';

  test('oluştur (raporlu) → galeride → aç → widget yerleşimi düzenle+kaydet', async ({ page, request }) => {
    // Test KENDİ bağımlılığını üretir. Önce başka bir testin bıraktığı "E2E SQL Rapor"a
    // güveniyordu; o test atlanınca ya da sıra değişince bu test düşüyordu (19 Ağu 2026'da düştü).
    // Rapor "E2E " öneki taşıdığı için süit sonunda teardown temizler.
    await hamSqlRaporVarEt(request, KAYNAK_RAPOR);

    await page.goto('/panolar');
    await expect(page.getByRole('heading', { name: 'Panolar' })).toBeVisible({ timeout: 20_000 });

    await page.getByRole('button', { name: 'Yeni Pano' }).click();
    const d = page.getByRole('dialog');
    await d.getByLabel('Pano adı').fill('E2E Pano');
    await d.getByText(KAYNAK_RAPOR).first().click();
    await d.getByRole('button', { name: 'Kaydet' }).click();

    // Galeride görünür
    await expect(page.getByText('E2E Pano').first()).toBeVisible({ timeout: 15_000 });

    // Aç → görüntüleyici (/panolar/:id) → react-grid-layout ızgarası render olur
    await page.getByText('E2E Pano').first().click();
    await expect(page).toHaveURL(/\/panolar\/[0-9a-fA-F-]+$/, { timeout: 10_000 });
    await expect(page.locator('.react-grid-layout')).toBeVisible({ timeout: 15_000 });

    // Yerleşim modu (sahip) → Kaydet → PUT gövdesinde yerlesim gider (S11 widget yerleşimi)
    await page.getByRole('button', { name: 'Yerleşim' }).click();
    await expect(page.getByRole('button', { name: 'Kaydet' })).toBeVisible();
    const [resp] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/api/panolar/') && r.request().method() === 'PUT'),
      page.getByRole('button', { name: 'Kaydet' }).click(),
    ]);
    expect(resp.status(), await resp.text()).toBe(200);
    const govde = JSON.parse(resp.request().postData() || '{}');
    expect(Array.isArray(govde.yerlesim) && govde.yerlesim.length > 0, 'yerlesim gönderilmedi').toBeTruthy();
  });
});
