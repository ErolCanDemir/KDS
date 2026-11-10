import { test, expect } from './fixtures';

// 2026-08-11 kullanıcı test turu (roadmap "Kullanıcı test turu" M1-M32) düzeltmelerinin
// regresyon testleri — her madde kapatılırken buraya canlı doğrulaması eklenir.

/** Bu dosyanın ÖN KOŞULU: çok tablolu demo haritası (Adresler · Ilceler · KisiAdresleri · Kisiler ·
 *  Araclar · Markalar · Talepler · TalepParcalari) — join/kanvas testleri bunlara dayanır. */
const HARITA = 'KDSDemoHarita2';

/**
 * Tasarımcıda ön koşul haritasını seçer.
 *
 * Neden ayrı yardımcı: testler doğrudan `KDSDemoHarita1`'i seçiyordu; o harita ortadan kalkınca
 * (yeniden adlandırılmış) beş test **60–90 saniye boyunca görünmeyen bir seçeneği bekleyip** zaman
 * aşımıyla düştü ve hata mesajı eksik fixture'ı değil yalnızca "locator bulunamadı" dedi. Artık ön
 * koşul açıkça doğrulanır: harita yoksa test saniyeler içinde ve NEDENİNİ söyleyerek düşer.
 */
async function haritaSec(page: import('@playwright/test').Page) {
  await page.getByRole('combobox').first().click();
  const secenek = page.getByRole('option', { name: HARITA, exact: true });
  await expect(secenek, `Ön koşul eksik: "${HARITA}" veri haritası bulunamadı. Bu dosyadaki `
    + 'join/kanvas testleri çok tablolu demo haritasına dayanır — haritayı oluşturun ya da '
    + 'HARITA sabitini mevcut bir haritaya güncelleyin.')
    .toBeVisible({ timeout: 10_000 });
  await secenek.click();
}

// M1: Veri haritası "Sil" onayı LİSTE görünümünde çıkmalı. Kök neden: confirmDialog yalnız
// editör görünümünün return'ünde render ediliyordu → listede Sil'e basınca hiç tepki yoktu,
// diyalog ancak editöre girince beliriyordu.
test('M1: harita sil onayı liste ekranında çıkar; Vazgeç korur, onay siler', async ({ page }) => {
  test.setTimeout(60_000);
  await page.goto('/veri-haritalari');
  await expect(page.getByRole('heading', { name: 'Veri Haritaları' })).toBeVisible({ timeout: 20_000 });

  // Kendi haritamızı kur (boş harita kaydedilebilir: yalnız ad+bağlantı zorunlu) → listeye dön.
  await page.getByRole('button', { name: 'Yeni Harita' }).click();
  const yeni = page.getByRole('dialog');
  await yeni.getByLabel('Harita adı').fill('E2E M1 SilTesti');
  await yeni.getByRole('button', { name: 'Oluştur' }).click();
  const [kayit] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/api/haritalar') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Kaydet' }).click(),
  ]);
  expect(kayit.status(), await kayit.text()).toBe(200);
  await page.locator('button:has(svg[data-testid="ArrowBackIcon"])').first().click();

  const kart = page.locator('.MuiPaper-root').filter({ hasText: 'E2E M1 SilTesti' }).first();
  await expect(kart).toBeVisible({ timeout: 15_000 });

  // M1'in özü: Sil → onay diyaloğu LİSTEDEYKEN görünmeli.
  await kart.locator('button:has(svg[data-testid="DeleteOutlinedIcon"])').click();
  const onay = page.getByRole('dialog');
  await expect(onay.getByText('silinsin mi?')).toBeVisible({ timeout: 5_000 });

  // Vazgeç → kart durur (yanlışlıkla silme yok).
  await onay.getByRole('button', { name: 'Vazgeç' }).click();
  await expect(kart).toBeVisible();

  // Tekrar Sil → onayla → kart listeden gider (test kendi verisini temizler).
  await kart.locator('button:has(svg[data-testid="DeleteOutlinedIcon"])').click();
  const [silYanit] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/api/haritalar/') && r.request().method() === 'DELETE'),
    page.getByRole('dialog').getByRole('button', { name: 'Sil' }).click(),
  ]);
  expect(silYanit.status()).toBeLessThan(300);
  await expect(page.locator('.MuiPaper-root').filter({ hasText: 'E2E M1 SilTesti' })).toHaveCount(0, { timeout: 10_000 });
});

// M2: kanvasa yeni eklenen tablo, mevcut kümenin YAKININA düşmeli ve görünür olmalı.
// Eski davranış: konum orijine çapalı indeks-slotuydu — kullanıcı düğümleri taşıyınca yeni
// tablo çok uzağa düşüyordu (JoinEkranGoruntusu_2). (Fixture: KDSDemoHarita3 — Araclar/Markalar.)
test('M2: yeni düğüm mevcut kümenin yakınına gelir ve kadraja girer', async ({ page }) => {
  test.setTimeout(90_000);
  await page.goto('/tasarimci');
  await expect(page.getByText('Sorgu Tasarımcısı').first()).toBeVisible({ timeout: 20_000 });
  await page.getByRole('combobox').first().click();
  await page.getByRole('option', { name: 'KDSDemoHarita3', exact: true }).click();

  // 1. tablo: Araclar → Plaka ekle → düğüm oluşur.
  await page.getByText('Araclar', { exact: true }).first().click();
  await page.getByText('Plaka', { exact: true }).first().click();
  const dugumAraclar = page.locator('.react-flow__node').filter({ hasText: 'Araclar' });
  await expect(dugumAraclar).toBeVisible({ timeout: 10_000 });

  // Düğümü kasıtlı olarak UZAĞA sürükle (kullanıcının yerleşim kurması senaryosu).
  const kutu = (await dugumAraclar.boundingBox())!;
  await page.mouse.move(kutu.x + kutu.width / 2, kutu.y + 10);
  await page.mouse.down();
  await page.mouse.move(kutu.x + kutu.width / 2 + 650, kutu.y + 10 + 320, { steps: 12 });
  await page.mouse.up();

  // 2. tablo: Markalar → Ad ekle → yeni düğüm.
  await page.getByText('Markalar', { exact: true }).first().click();
  await page.getByText('Ad', { exact: true }).first().click();
  const dugumMarkalar = page.locator('.react-flow__node').filter({ hasText: 'Markalar' });
  await expect(dugumMarkalar).toBeVisible({ timeout: 10_000 });

  // Akış-koordinatları transform'dan okunur.
  const konum = async (d: typeof dugumAraclar) => {
    const stil = (await d.getAttribute('style')) ?? '';
    const m = /translate\(([-\d.]+)px,\s*([-\d.]+)px\)/.exec(stil)!;
    return { x: parseFloat(m[1]), y: parseFloat(m[2]) };
  };
  const k1 = await konum(dugumAraclar);
  const k2 = await konum(dugumMarkalar);

  // Yakınlık: komşu sütun (200 genişlik + 160 oluk = 360px), aynı satır — eski davranışta
  // yeni düğüm orijin slotuna (taşınan kümeden uzağa) düşerdi.
  expect(Math.abs(k2.x - k1.x), `k1=${JSON.stringify(k1)} k2=${JSON.stringify(k2)}`).toBeLessThanOrEqual(380);
  expect(k2.y, 'yeni düğüm kümenin üstüne/gerisine düşmemeli').toBeGreaterThanOrEqual(k1.y - 20);
  expect(Math.abs(k2.y - k1.y)).toBeLessThanOrEqual(400);

  // Görünürlük: ekleme sonrası kadraj iki düğümü de kapsar (fitView).
  await expect(dugumMarkalar).toBeInViewport({ ratio: 0.5 });
  await expect(dugumAraclar).toBeInViewport({ ratio: 0.5 });
});

// M2b: oluk ORANTILI — yatay boşluk düğüm genişliğinin %80'i (160/200), dikey boşluk üstteki
// düğüm BOYUNUN %80'i; böylece iki eksen görsel olarak aynı oranda açık. (Fixture: KDSDemoHarita2.)
test('M2b: alta sarmada dikey oluk yatayla aynı ORANDA (%80)', async ({ page }) => {
  test.setTimeout(90_000);
  await page.goto('/tasarimci');
  await expect(page.getByText('Sorgu Tasarımcısı').first()).toBeVisible({ timeout: 20_000 });
  await haritaSec(page);

  // 5 farklı tablodan birer alan → 4 sütun dolar, 5. alta sarar.
  const alanlar: [string, string][] = [
    ['Araclar', 'Plaka'], ['Markalar', 'UlkeAdi'], ['Talepler', 'TalepNo'],
    ['Ilceler', 'NufusTahmini'], ['GetirmeSebepleri', 'PlanliMi'],
  ];
  for (const [tablo, kolon] of alanlar) {
    await page.getByText(tablo, { exact: true }).first().click();   // paneli aç
    await page.getByText(kolon, { exact: true }).first().click();   // alanı ekle
    await expect(page.locator('.react-flow__node').filter({ hasText: tablo })).toBeVisible({ timeout: 10_000 });
  }

  const konum = async (tablo: string) => {
    const stil = (await page.locator('.react-flow__node').filter({ hasText: tablo }).getAttribute('style')) ?? '';
    const m = /translate\(([-\d.]+)px,\s*([-\d.]+)px\)/.exec(stil)!;
    return { x: parseFloat(m[1]), y: parseFloat(m[2]) };
  };
  const k1 = await konum('Araclar');           // satır 1, sütun 1
  const k2 = await konum('Markalar');          // satır 1, sütun 2
  const k5 = await konum('GetirmeSebepleri');  // satır 2, sütun 1 (sarma)

  // Yatay oluk: 200 + 160 = 360px sütun adımı (boşluk/genişlik = %80).
  expect(k2.x - k1.x, `k1=${JSON.stringify(k1)} k2=${JSON.stringify(k2)}`).toBeGreaterThanOrEqual(355);
  expect(k2.x - k1.x).toBeLessThanOrEqual(365);
  // Dikey: üst düğüm boyu (~65-85) + boyunun %80'i → 1.8×boy ≈ 117-150 (sabit 160+ değil).
  expect(k5.x, 'saran düğüm 1. sütuna iner').toBeCloseTo(k1.x, 0);
  expect(k5.y - k1.y, `k5=${JSON.stringify(k5)}`).toBeGreaterThanOrEqual(105);
  expect(k5.y - k1.y).toBeLessThanOrEqual(160);
});

// M3: alan panelindeki arama Türkçe i/ı duyarsız olmalı — "ilceler" (ve "ılceler") yazınca
// "Ilceler" tablosu gelmeli. Eskiden tr-lowercase "Ilceler"i "ılceler"e çevirip "ilceler"
// aramasını boş bırakıyordu. (Fixture: KDSDemoHarita2 — ortak.Ilceler.)
test('M3: alan araması Türkçe i/ı varyantlarını eşler', async ({ page }) => {
  test.setTimeout(60_000);
  await page.goto('/tasarimci');
  await expect(page.getByText('Sorgu Tasarımcısı').first()).toBeVisible({ timeout: 20_000 });
  await haritaSec(page);

  const arama = page.getByPlaceholder('tablo / kolon ara');
  await expect(arama).toBeVisible({ timeout: 10_000 });

  // Noktalı küçük i ile → "Ilceler" bulunur (kullanıcının senaryosu).
  await arama.fill('ilceler');
  await expect(page.getByText('Ilceler', { exact: true })).toBeVisible({ timeout: 5_000 });

  // Noktasız ı ile de bulunur (ters varyant).
  await arama.fill('ılceler');
  await expect(page.getByText('Ilceler', { exact: true })).toBeVisible();

  // Kontrol: uydurma arama gerçekten filtreliyor (her şeyi göstermiyoruz).
  await arama.fill('xqzy');
  await expect(page.getByText('Ilceler', { exact: true })).toHaveCount(0);
});

// M4/M13: Kisiler→KisiAdresleri→Adresler→Ilceler zinciri — kanvasta ÜÇ kenar da çizilmeli.
// Eski kör BFS, Adresler'i kanvasta olmayan bir ara tablo üzerinden keşfedip kenarı hiç
// çizmeyebiliyordu (JoinEkranGoruntusu). (Fixture: KDSDemoHarita2.)
test('M4: adres zinciri kanvasta eksiksiz bağlanır (üç kenar da çizilir)', async ({ page }) => {
  test.setTimeout(90_000);
  await page.goto('/tasarimci');
  await expect(page.getByText('Sorgu Tasarımcısı').first()).toBeVisible({ timeout: 20_000 });
  await haritaSec(page);

  // Kolon adları haritada BENZERSİZ seçilir: panelde açılan tablolar açık kalıyor, bu yüzden
  // getByText(kolon).first() birden çok tabloda geçen bir adı yanlış tablodan yakalayabilir.
  // "AktifMi" tam bu tuzağa düştü — haritada 4 tabloda var (Kisiler·Araclar·Servisler·
  // KisiAdresleri) ve test KisiAdresleri yerine Kisiler'in kolonunu tıklıyordu; sonuçta
  // KisiAdresleri düğümü hiç eklenmiyordu. "BeyanTuru" yalnız KisiAdresleri'nde geçer.
  const alanlar: [string, string][] = [
    ['Kisiler', 'Soyad'], ['KisiAdresleri', 'BeyanTuru'], ['Adresler', 'AdresTipiId'], ['Ilceler', 'NufusTahmini'],
  ];
  for (const [tablo, kolon] of alanlar) {
    await page.getByText(tablo, { exact: true }).first().click();
    await page.getByText(kolon, { exact: true }).first().click();
    // hasText alt-dize eşler ("KisiAdresleri" ⊃ "Adresler") → exact metinli has filtresi.
    await expect(page.locator('.react-flow__node').filter({ has: page.getByText(tablo, { exact: true }) }))
      .toBeVisible({ timeout: 10_000 });
  }

  // Üç join kenarı da kurulur — hiçbir tablo kopuk/ara kalmaz.
  // Etiket YÖNE DUYARSIZ aranır: join ağacı hangi tablodan kök alırsa etiket o yönde yazılıyor
  // ("A.x = B.y" ya da "B.y = A.x"). Testin iddiası bağlantının KURULDUĞU; yazım yönü değil —
  // tek yöne çakılı eşleşme, ağacın kökü değişince sebepsiz kırılıyordu.
  const kenar = (a: string, b: string) =>
    page.getByText(new RegExp(`(${a}\\s*=\\s*${b}|${b}\\s*=\\s*${a})`)).first();
  await expect(kenar('KisiAdresleri\\.KisiId', 'Kisiler\\.Id')).toBeVisible({ timeout: 10_000 });
  await expect(kenar('KisiAdresleri\\.AdresId', 'Adresler\\.Id')).toBeVisible();
  await expect(kenar('Adresler\\.IlceId', 'Ilceler\\.Id')).toBeVisible();
  // Kopukluk uyarısı yok.
  await expect(page.getByText('join yolu yok')).toHaveCount(0);
});

// M5: çok-tablolu join koşulu — join koşulları İKİ AYRI tabloya başvurur. Kullanıcının birebir
// ServisStoklari deseni backend altın-örnek testinde (2M'lik tablolarda canlı koşum LocalDB'de
// zaman aşımına yatkın); buradaki canlı senaryo aynı yeteneğin küçük-tablolu eşi:
// LEFT JOIN arac.Araclar a ON a.MarkaId = mk.Id AND a.ModelId = md.Id (Markalar 40 · Modeller 600).
test('M5: join koşulları iki ayrı tabloya başvurur (Araclar: Markalar + Modeller)', async ({ page }) => {
  test.setTimeout(120_000);
  await page.goto('/tasarimci');
  await expect(page.getByText('Sorgu Tasarımcısı').first()).toBeVisible({ timeout: 20_000 });
  await haritaSec(page);

  // Alanlar: Markalar.UlkeAdi (kök), Modeller.SegmentKodu, Araclar.Plaka.
  for (const [tablo, kolon] of [['Markalar', 'UlkeAdi'], ['Modeller', 'SegmentKodu'], ['Araclar', 'Plaka']] as const) {
    await page.getByText(tablo, { exact: true }).first().click();
    await page.getByText(kolon, { exact: true }).first().click();
  }

  // Join Yolları: Araclar satırı → ebeveyn Markalar + LEFT + iki koşul.
  await page.getByRole('button', { name: /Join Yolları/ }).first().click();
  const satir = page.locator('.MuiPaper-root').filter({ has: page.getByText('Araclar', { exact: true }) }).last();
  await satir.getByRole('combobox').nth(0).click();
  await page.getByRole('option', { name: 'Markalar', exact: true }).click();
  await satir.getByRole('combobox').nth(1).click();
  await page.getByRole('option', { name: /LEFT/ }).click();

  // Koşul 1: MarkaId = Markalar.Id (koşul tablosu varsayılan = ana ebeveyn).
  await satir.getByRole('button', { name: 'Koşul ekle' }).click();
  await satir.getByRole('combobox').nth(2).click();
  await page.getByRole('option', { name: 'MarkaId', exact: true }).click();
  await satir.getByRole('combobox').nth(4).click();
  await page.getByRole('option', { name: 'Id', exact: true }).first().click();

  // Koşul 2: ModelId = Modeller.Id (koşul tablosu DEĞİŞTİRİLİYOR — M5'in özü).
  await satir.getByRole('button', { name: 'Koşul ekle' }).click();
  await satir.getByRole('combobox').nth(5).click();
  await page.getByRole('option', { name: 'ModelId', exact: true }).click();
  await satir.getByRole('combobox').nth(6).click();
  await page.getByRole('option', { name: 'Modeller', exact: true }).click();
  await satir.getByRole('combobox').nth(7).click();
  await page.getByRole('option', { name: 'Id', exact: true }).first().click();

  // Satır limitini küçült (TOP satır-hedefi ile hızlı biter).
  await page.getByRole('button', { name: /Kolonlar & Ölçüler/ }).first().click();
  await page.getByRole('spinbutton').first().fill('50');

  // Sonuç & SQL → SQL'i Yenile: üretilen SQL iki koşullu LEFT JOIN taşımalı.
  await page.getByRole('button', { name: /Sonuç & SQL/ }).first().click();
  const [sqlYanit] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/api/sorgu/sql') && r.request().method() === 'POST'),
    page.getByRole('button', { name: "SQL'i Yenile" }).click(),
  ]);
  expect(sqlYanit.status(), await sqlYanit.text()).toBe(200);
  const sql = ((await sqlYanit.json()) as { sql: string }).sql;
  expect(sql).toContain(
    'LEFT JOIN [arac].[Araclar] AS [a] ON [a].[MarkaId] = [m].[Id] AND [a].[ModelId] = [m1].[Id]');

  // Çalıştır → gerçek DB'de koşar (canlı kanıt).
  const [kosum] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/api/sorgu/calistir') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Çalıştır' }).click(),
  ]);
  expect(kosum.status(), await kosum.text()).toBe(200);
});

// M6: "Bağlı Tablo Özeti (alt sorgu)" ne işe yarar — diyaloğun İÇİNDE örnekle anlatılır:
// SELECT'e özet kolon ekler (WHERE koşulu değildir), satır filtrelemez/çoğaltmaz.
test('M6: Bağlı Tablo Özeti diyaloğu ne olduğunu örnekle anlatır', async ({ page }) => {
  test.setTimeout(60_000);
  await page.goto('/tasarimci');
  await expect(page.getByText('Sorgu Tasarımcısı').first()).toBeVisible({ timeout: 20_000 });
  await haritaSec(page);
  await page.getByText('Talepler', { exact: true }).first().click();
  await page.getByText('TalepNo', { exact: true }).first().click();

  // Kolonlar & Ölçüler adımı → "Özet alan" → diyalog.
  await page.getByRole('button', { name: /Kolonlar & Ölçüler/ }).first().click();
  await page.getByRole('button', { name: 'Özet alan' }).click();
  const d = page.getByRole('dialog');
  await expect(d.getByText(/SELECT'e alt sorgu eklemenizi sağlar/)).toBeVisible({ timeout: 5_000 });
  await expect(d.getByText('WHERE koşulu değildir')).toBeVisible();
  await expect(d.getByText('Örnek:')).toBeVisible();
  await expect(d.getByText('TalepParcalari').first()).toBeVisible();

  // Seçim tamamlanınca canlı özet cümlesi çıkar ("… kolonu ekler … Satır sayısı değişmez").
  await d.getByRole('combobox').nth(0).click();
  await page.getByRole('option', { name: 'TalepParcalari', exact: true }).click();
  await d.getByRole('combobox').nth(3).click();
  await page.getByRole('option', { name: 'Id', exact: true }).first().click();
  await expect(d.getByText('Satır sayısı değişmez')).toBeVisible();
});
