import { test, expect } from './fixtures';

// S12 Faz 4 — İçe Alım İşleri ekranı: kaynak(İçe Alım) bağlantı → iş oluştur (tablo seç) →
// Şimdi Çalıştır (gerçek KdsDemo→KdsDepo senkronu) → düzenle → sil. Doğrudan URL ile.

test.describe('İçe Alım İşleri', () => {
  test('İçe Alım bağlantı → iş oluştur → çalıştır → düzenle → sil', async ({ page }) => {
    // 1) İçe Alım modlu kaynak bağlantı (yoksa iş için kaynak seçilemez).
    await page.goto('/baglantilar');
    await expect(page.getByRole('heading', { name: 'Bağlantılar' })).toBeVisible({ timeout: 20_000 });
    await page.getByRole('button', { name: 'Yeni Bağlantı' }).first().click();
    const bd = page.getByRole('dialog');
    await bd.getByLabel('Bağlantı adı').fill('E2E İçe Alım Kaynak');
    await bd.getByLabel('Sunucu').fill('(localdb)\\MSSQLLocalDB');
    await bd.getByLabel('Veritabanı').fill('KdsDemo');
    await bd.getByRole('button', { name: 'İçe Alım' }).click();
    await bd.getByRole('button', { name: 'Kaydet' }).click();
    await expect(page.getByText('E2E İçe Alım Kaynak').first()).toBeVisible({ timeout: 15_000 });

    // 2) İçe Alım İşleri → Yeni İş
    await page.goto('/ice-alim');
    await expect(page.getByRole('heading', { name: 'İçe Alım İşleri' })).toBeVisible({ timeout: 20_000 });
    await page.getByRole('button', { name: 'Yeni İş' }).first().click();

    const editor = page.locator('.MuiDrawer-paper');
    await editor.getByLabel('İş adı').fill('E2E Senkron');
    // Kaynak bağlantıyı seç (MUI Select → listbox portal'da açılır).
    await editor.getByRole('combobox').click();
    await page.getByRole('option', { name: /E2E İçe Alım Kaynak/ }).first().click();

    // Tablo ara + ekle (KdsDemo'da ortak.YagTipleri).
    await editor.getByPlaceholder(/Tablo ara/).fill('Yag');
    await editor.getByRole('button', { name: 'Ara' }).click();
    await editor.getByText('ortak.YagTipleri').first().click();
    await expect(editor.getByText('Tüm kolonlar')).toBeVisible({ timeout: 10_000 });

    // Zamanlama: M22a/M23'te çıplak cron kutusu yerine CronSecici geldi (14 Ağu 2026, d8de088) —
    // artık "Günlük" kipi seçilip saat giriliyor. Test 19 Ağu 2026'ya kadar eski "Günlük 03:00"
    // düğmesini arıyordu: 5 gündür kırıktı ve kimse görmemişti, çünkü süit hiç sonuna kadar
    // koşmuyordu (kimlik 15. dakikada ölüyordu).
    await editor.getByRole('button', { name: 'Günlük', exact: true }).click();
    await editor.locator('input[type=time]').first().fill('03:00');
    // Seçici ne ürettiğini insan diliyle yazar → kipin GERÇEKTEN uygulandığını burada doğrularız
    // (eski test yalnız düğmeye basıyordu; sonucu kontrol etmiyordu).
    await expect(editor.getByText(/Her gün saat 03:00'de çalışır/)).toBeVisible({ timeout: 10_000 });
    const [olustur] = await Promise.all([
      page.waitForResponse((r) => r.url().endsWith('/api/ice-alim') && r.request().method() === 'POST'),
      editor.getByRole('button', { name: 'Kaydet' }).click(),
    ]);
    expect(olustur.status(), await olustur.text()).toBe(200);
    await expect(page.getByText('E2E Senkron').first()).toBeVisible({ timeout: 15_000 });

    // 3) Şimdi Çalıştır → gerçek senkron (calistir 200) → kartta "Başarılı".
    const [kosu] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/api/ice-alim/') && r.url().endsWith('/calistir') && r.request().method() === 'POST'),
      page.getByRole('button', { name: 'Şimdi Çalıştır' }).first().click(),
    ]);
    expect(kosu.status(), await kosu.text()).toBe(200);
    await expect(page.getByText(/Başarılı/).first()).toBeVisible({ timeout: 30_000 });

    // 4) Düzenle (yeniden adlandır) → PUT 200.
    await page.getByRole('button', { name: 'Düzenle' }).first().click();
    await editor.getByLabel('İş adı').fill('E2E Senkron 2');
    const [guncelle] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/api/ice-alim/') && r.request().method() === 'PUT'),
      editor.getByRole('button', { name: 'Kaydet' }).click(),
    ]);
    expect(guncelle.status(), await guncelle.text()).toBe(200);
    await expect(page.getByText('E2E Senkron 2').first()).toBeVisible({ timeout: 15_000 });

    // 5) Sil (onay).
    await page.getByRole('button', { name: 'Sil' }).first().click();
    const onay = page.getByRole('dialog');
    await onay.getByRole('button', { name: /Sil|Onayla|Evet/ }).click();
    await expect(page.getByText('E2E Senkron 2')).toHaveCount(0, { timeout: 15_000 });
  });
});
