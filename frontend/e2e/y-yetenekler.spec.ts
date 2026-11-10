import { test, expect } from './fixtures';

// S17/S18 tasarımcı yetenekleri (Y1–Y16) — UÇTAN UCA. Birim/altın-örnek testleri SQL ÜRETİMİNİ
// zaten kaplıyor; buradaki risk BAĞLANTI HATTIdır: ekran durumu → model → SQL → kaydet → tasarımcıda
// tekrar aç. Daha önce tam burada gerçek bir veri kaybı çıkmıştı (M36: rapor yüklemesi join
// koşulunu düşürüyordu), bu yüzden "kaydet→aç" turu ayrı bir test olarak duruyor.
// Fixture: KDSDemoHarita3 (Araclar · Markalar · Talepler — KdsDemo).

/** Haritayı seçip iki alan ekler: Markalar.Ad ve Talepler.ToplamTutar. */
async function temelSorgu(page: import('@playwright/test').Page) {
  await page.goto('/tasarimci');
  await expect(page.getByText('Sorgu Tasarımcısı').first()).toBeVisible({ timeout: 20_000 });
  await page.getByRole('combobox').first().click();
  await page.getByRole('option', { name: 'KDSDemoHarita3', exact: true }).click();

  await page.getByText('Markalar', { exact: true }).click();
  await page.getByText('Ad', { exact: true }).first().click();
  await page.getByText('Talepler', { exact: true }).click();
  await page.getByText('ToplamTutar', { exact: true }).first().click();
}

/** "Kolonlar & Ölçüler" adımına geçer (yeni yeteneklerin çoğu orada). */
async function kolonlarAdimi(page: import('@playwright/test').Page) {
  await page.getByRole('button', { name: '3 Kolonlar & Ölçüler' }).click();
  await expect(page.getByText('Kolonlar & Ölçüler').first()).toBeVisible({ timeout: 15_000 });
}

test('Y12/Y16: DISTINCT ve sayfalama üretilen SQL’e yansır', async ({ page }) => {
  test.setTimeout(90_000);
  await temelSorgu(page);
  await kolonlarAdimi(page);

  // Sıralama gerekir: sayfalama sırasız çalışmaz (üretici açık hata verir).
  await page.getByRole('button', { name: 'Sıra ekle' }).click();
  await page.getByLabel('Limit / sayfa boyu').fill('25');
  await page.getByLabel('Atla (kaçıncı satırdan)').fill('50');
  await page.getByText('Yinelenen satırları ele (DISTINCT)').click();

  await page.getByRole('button', { name: '4 Sonuç & SQL' }).click();
  const [uretim] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/api/sorgu/sql') && r.request().method() === 'POST'),
    page.getByRole('button', { name: "SQL'i Yenile" }).click(),
  ]);
  expect(uretim.status(), await uretim.text()).toBe(200);
  const sql = (await uretim.json()).sql as string;

  expect(sql, 'DISTINCT üretilmedi').toContain('SELECT DISTINCT');
  expect(sql, 'sayfalama üretilmedi').toContain('OFFSET (@atla) ROWS');
  expect(sql, 'sayfa boyu FETCH ile gitmeli').toContain('FETCH NEXT (@satir) ROWS ONLY');
  expect(sql, 'TOP ile OFFSET aynı sorguda olamaz').not.toContain('TOP (@satir)');
});

test('Y4/Y1: tarih kırılımı ve hesaplanan kolon SQL’e yansır', async ({ page }) => {
  test.setTimeout(90_000);
  await temelSorgu(page);

  // Tarih kolonu ekle (kırılım seçicisi yalnız tarih kolonlarında çıkar).
  await page.getByText('GirisTarihi', { exact: true }).first().click();
  await kolonlarAdimi(page);

  // Y4: GirisTarihi satırındaki kırılım seçicisi → "Ay (dönem başı)".
  const kirilim = page.getByTitle(/Tarihi yıla\/aya\/haftaya indirger/).first();
  await kirilim.click();
  await page.getByRole('option', { name: /Ay \(dönem başı\)/ }).click();

  // Y1: hesaplanan kolon — KDV benzeri basit bir formül.
  await page.getByRole('button', { name: 'Formül ekle' }).click();
  const diyalog = page.getByRole('dialog');
  await diyalog.getByPlaceholder('ör. Fark').fill('Kdv');
  await diyalog.getByPlaceholder(/Odemeler.Tutar/).fill('{Talepler.ToplamTutar} * 0.20');
  await diyalog.getByRole('button', { name: 'Uygula' }).click();

  await page.getByRole('button', { name: '4 Sonuç & SQL' }).click();
  const [uretim] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/api/sorgu/sql') && r.request().method() === 'POST'),
    page.getByRole('button', { name: "SQL'i Yenile" }).click(),
  ]);
  expect(uretim.status(), await uretim.text()).toBe(200);
  const sql = (await uretim.json()).sql as string;

  expect(sql, 'tarih kırılımı üretilmedi').toContain('DATEADD(month, DATEDIFF(month, 0,');
  expect(sql, 'hesaplanan kolon üretilmedi').toContain('AS [Kdv]');
  expect(sql, 'formül yer tutucusu çözülmedi').toContain('* 0.20');
});

// EN KRİTİK TUR: yeni alanlar KAYDEDİLİP tasarımcıda geri yükleniyor mu? (M36 sınıfı hata avı:
// modelde olan bir alanın yükleme yolunda sessizce düşmesi.)
test('kaydet → tasarımcıda tekrar aç: yeni alanlar kayıpsız geri gelir', async ({ page }) => {
  test.setTimeout(120_000);
  const ad = `E2E Y-Yetenek ${Date.now()}`;

  await temelSorgu(page);
  await page.getByText('GirisTarihi', { exact: true }).first().click();
  await kolonlarAdimi(page);

  // Y4 kırılım + Y12 DISTINCT + Y16 sayfalama: üçü de farklı yerlerde saklanıyor.
  await page.getByTitle(/Tarihi yıla\/aya\/haftaya indirger/).first().click();
  await page.getByRole('option', { name: /Ay \(dönem başı\)/ }).click();
  await page.getByLabel('Atla (kaçıncı satırdan)').fill('30');
  await page.getByText('Yinelenen satırları ele (DISTINCT)').click();

  // Kaydet.
  await page.getByRole('button', { name: '4 Sonuç & SQL' }).click();
  await page.getByRole('button', { name: 'Rapor Olarak Kaydet' }).click();
  const kayitDiyalog = page.getByRole('dialog');
  await kayitDiyalog.getByPlaceholder('ör. Şehir bazında satış').fill(ad);
  const [kayit] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/api/raporlar') && r.request().method() === 'POST'),
    kayitDiyalog.getByRole('button', { name: 'Kaydet' }).click(),
  ]);
  expect(kayit.status(), await kayit.text()).toBe(200);
  const raporId = (await kayit.json()).id as string;

  // Tasarımcıyı TEMİZ aç, sonra raporu yükle (oturum taslağı değil, RAPOR yolu sınansın).
  await page.goto(`/tasarimci?rapor=${raporId}`);
  await expect(page.getByText('Sorgu Tasarımcısı').first()).toBeVisible({ timeout: 20_000 });
  await kolonlarAdimi(page);

  // Geri gelmeli: kırılım "Ay", DISTINCT açık, atla 30.
  await expect(page.getByText('⤵ Ay (dönem başı)').first()).toBeVisible({ timeout: 15_000 });
  await expect(page.getByLabel('Atla (kaçıncı satırdan)')).toHaveValue('30');
  await expect(page.getByRole('switch', { name: /Yinelenen satırları ele/ })).toBeChecked();

  // Temizlik: testin kendi verisi kalmasın.
  await page.evaluate(async (id) => {
    const anahtar = Object.keys(sessionStorage).find((k) => k.startsWith('oidc.'));
    const token = anahtar ? JSON.parse(sessionStorage.getItem(anahtar)!).access_token : '';
    await fetch(`http://localhost:5210/api/raporlar/${id}`, {
      method: 'DELETE', headers: { Authorization: `Bearer ${token}` },
    });
  }, raporId);
});
