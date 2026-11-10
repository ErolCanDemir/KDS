import { test, expect } from './fixtures';

// Tüm ana ekranlar kimlikli açılıyor mu + sayfada JS hatası var mı. İngilizce kod geçişinin hiçbir
// ekranı bozmadığını doğrudan doğrular ("bu iş bir mantığımızı bozdu mu").
const ekranlar: { yol: string; baslik: string }[] = [
  { yol: '/baglantilar', baslik: 'Bağlantılar' },
  { yol: '/veri-haritalari', baslik: 'Veri Haritaları' },
  { yol: '/tasarimci', baslik: 'Sorgu Tasarımcısı' },
  { yol: '/sql', baslik: 'SQL Editörü' },
  { yol: '/raporlar', baslik: 'Raporlar' },
  { yol: '/panolar', baslik: 'Panolar' },
  { yol: '/yonetim', baslik: 'Yönetim' },
  { yol: '/yonetim/kurulus', baslik: 'Kuruluş Ayarları' },   // S35: yer tutucu değil, gerçek ekran
  { yol: '/yonetim/kullanim', baslik: 'Kullanım' },          // S41: v1 menüsünün son maddesi
  { yol: '/yonetim/gomme', baslik: 'Gömme & Marka' },        // S42: sol menünün son yer tutucusu
  { yol: '/yonetim/ajanlar', baslik: 'Ajanlar' },            // S45: KDS Ajanı (push tabanlı içe alım)
];

test('ana ekran (home) kimlikli açılıyor', async ({ page }) => {
  const hatalar: string[] = [];
  page.on('pageerror', (e) => hatalar.push(e.message));
  await page.goto('/');
  await expect(page).toHaveURL(/localhost:5173\/?$/, { timeout: 20_000 });
  await page.waitForLoadState('networkidle');
  expect(hatalar, 'sayfa JS hatası:\n' + hatalar.join('\n')).toHaveLength(0);
});

for (const e of ekranlar) {
  test(`ekran açılıyor: ${e.baslik} (${e.yol})`, async ({ page }) => {
    const hatalar: string[] = [];
    page.on('pageerror', (err) => hatalar.push(err.message));
    await page.goto(e.yol);
    await expect(page.getByText(e.baslik, { exact: false }).first()).toBeVisible({ timeout: 20_000 });
    expect(hatalar, 'sayfa JS hatası:\n' + hatalar.join('\n')).toHaveLength(0);
  });
}
