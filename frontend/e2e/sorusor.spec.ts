import { test, expect } from './fixtures';

// S23 Soru Sor — EKRAN hattı testleri. Gerçek yerel model CPU'da 15-50 sn düşünür; süite o
// bekleme konmaz (kırılganlık + süre). Gerçek Ollama'lı uçtan uca akış canlı doğrulamayla
// kanıtlandı; burada ağ KESİŞTİRİLİR ve ekranın taahhütleri sınanır.
//
// S31 (tezgâh düzeni): ekran artık solda konuşma / sağda SONUÇ TUVALİ. Testler bu düzenin
// taşıyıcı parçalarını tutar: sohbet balonu özeti, tuvalde sonuç, durum rayı, kanıt şeridi,
// takip çipiyle düzeltme ve eski cevaba dönüş.

const CEVAP = {
  sonuc: {
    kolonlar: [{ ad: 'Servis No', veriTipi: 'int' }, { ad: 'Toplam Talep Tutari', veriTipi: 'money' }],
    satirlar: [[3, 2437627], [2, 2210914], [1, 1981200]],
    satirSiniriUygulandi: false, sureMs: 641,
  },
  model: {
    haritaId: '00000000-0000-0000-0000-000000000001',
    alanlar: [{ tabloId: 't1', kolon: 'ServisNo', toplama: 'Yok', takmaAd: null, kova: null }],
    filtre: null, siralama: [], satirLimiti: 3,
  },
  nasilHesaplandi: ['Metrik: Toplam Talep Tutari (katalogdan; formül koşum anında çözüldü)', 'Kırılım: Servis No', 'Limit: ilk 3 satır'],
  gidenIstem: 'Sen KDS raporlama ürününün sorgu kurucususun. [TEST İSTEMİ]',
  modelCiktisi: '{"metrikler":["Toplam Talep Tutari"]}',
  ceviriSureMs: 14200,
  oneriGorsellestirme: 'Tablo',
  durum: {
    haritaAd: 'Test Haritası',
    tablolar: ['Talepler'],
    metrikler: [{ id: 'm1', ad: 'Toplam Talep Tutari', sonDegisiklik: '2026-08-21T10:00:00+03:00' }],
    cipler: [
      { tur: 'metrik', etiket: 'Toplam Talep Tutari', kaldirilabilir: false },
      { tur: 'kirilim', etiket: 'Servis No', kaldirilabilir: true },
      { tur: 'limit', etiket: 'ilk 3 satır', kaldirilabilir: true },
    ],
  },
};

test('soru sor: sohbet özeti + tuvalde sonuç + durum rayı + kanıt şeridi', async ({ page }) => {
  test.setTimeout(60_000);

  await page.route('**/api/soru/durum', (r) =>
    r.fulfill({ json: { hazir: true, model: 'qwen2.5-coder:7b', sebep: null } }));
  await page.route('**/api/soru', (r) => r.fulfill({ json: CEVAP }));

  await page.goto('/soru-sor');
  await expect(page.getByRole('heading', { name: 'Soru Sor' })).toBeVisible({ timeout: 20_000 });
  await expect(page.getByTestId('nlq-durum')).toHaveText('Yerel model hazır');

  // Boş durum ürünün ne yapabildiğini anlatır (yönlendirmesiz boş kutu anti-kalıbı).
  await expect(page.getByText('Ne öğrenmek istersin?')).toBeVisible();

  // Örnek çip soruyu sorar.
  await page.getByText('marka bazında talep sayısı').click();

  // Sohbette ÖZET cümle, tuvalde SONUÇ — veri dar sohbet sütununda çizilmez.
  await expect(page.getByTestId('cevap-balonu')).toBeVisible({ timeout: 15_000 });
  await expect(page.getByTestId('cevap-balonu')).toContainText('3 satır buldum');
  await expect(page.getByTestId('nlq-cevap')).toBeVisible();
  await expect(page.getByTestId('nlq-cevap').getByText('2.437.627')).toBeVisible();

  // Durum rayı: sorgunun hâli sürekli görünür (unutulmuş filtre/limit hatasının panzehiri).
  // Limit çipi ROL ile hedeflenir: aynı metin kanıt şeridinde de geçiyor (kapalı Collapse'ta).
  await expect(page.getByText('Sorgunun hâli')).toBeVisible();
  await expect(page.getByRole('button', { name: /Satır limiti/ })).toContainText('ilk 3 satır');

  // Atıf satırı daima açık: neye dayanıyor + RLS.
  await expect(page.getByText('satır kuralları (RLS) sizin yetkinizle uygulandı', { exact: false })).toBeVisible();

  // Kanıt şeridi: dört şeffaflık kaydı tek katlanır alanda.
  await page.getByTestId('kanit-serit').click();
  await expect(page.getByText('Metrik: Toplam Talep Tutari', { exact: false })).toBeVisible();
  await page.getByRole('tab', { name: 'Modele giden istem' }).click();
  await expect(page.getByText('[TEST İSTEMİ]', { exact: false })).toBeVisible();
  // Model JSON'u kendi sekmesinde durur…
  await page.getByRole('tab', { name: 'Sorgu modeli' }).click();
  await expect(page.getByText('"satirLimiti": 3', { exact: false })).toBeVisible();
  /* …ve "Çalışan SQL" GERÇEKTEN SQL gösterir (kullanıcı bulgusu 24 Ağu 2026: sekme "üretilen
     sorgu" diyip JSON basıyordu). SQL ayrı uçtan tembel çekilir; burada sahte yanıt verilir. */
  await page.route('**/api/sorgu/sql', (r) =>
    r.fulfill({ json: { sql: 'SELECT [t].[Ad] FROM [dbo].[Talepler] AS [t]', parametreler: {} } }));
  await page.getByRole('tab', { name: 'Çalışan SQL' }).click();
  await expect(page.getByTestId('nlq-sql')).toContainText('SELECT');
  await expect(page.getByTestId('nlq-sql')).not.toContainText('satirLimiti');
});

test('soru sor: takip çipi düzeltme sorar, eski cevaba dönülebilir', async ({ page }) => {
  test.setTimeout(60_000);

  await page.route('**/api/soru/durum', (r) =>
    r.fulfill({ json: { hazir: true, model: 'qwen2.5-coder:7b', sebep: null } }));

  const istekler: string[] = [];
  await page.route('**/api/soru', (r) => {
    const govde = JSON.parse(r.request().postData() ?? '{}') as { soru?: string; oncekiModelJson?: string | null };
    istekler.push(govde.oncekiModelJson ? `DUZELTME:${govde.soru}` : `SORU:${govde.soru}`);
    r.fulfill({ json: CEVAP });
  });

  await page.goto('/soru-sor');
  await page.getByText('marka bazında talep sayısı').click();
  await expect(page.getByTestId('cevap-balonu')).toBeVisible({ timeout: 15_000 });

  // Takip çipi: yazı yazmadan düzeltme — istek ÖNCEKİ MODELLE gider (çok turlu düzeltme).
  const cip = page.getByTestId('takip-cip').first();
  await expect(cip).toBeVisible();
  await cip.click();
  await expect(page.getByTestId('cevap-balonu')).toHaveCount(2, { timeout: 15_000 });
  expect(istekler.filter((i) => i.startsWith('DUZELTME:'))).toHaveLength(1);

  // Eski cevaba tıklayınca tuval ona döner — "önceki grafik yukarı kayıp gitti" sorununun çözümü.
  await page.getByTestId('cevap-balonu').first().click();
  await expect(page.getByTestId('nlq-cevap')).toBeVisible();
});

test('soru sor: Ollama yoksa ürün çökmez — yönerge kartı + giriş kapalı', async ({ page }) => {
  await page.route('**/api/soru/durum', (r) =>
    r.fulfill({ json: { hazir: false, model: 'qwen2.5-coder:7b', sebep: "Yerel modele ulaşılamadı (http://127.0.0.1:11434). Ollama kurulu değilse ollama.com'dan indirin; kuruluysa servisi başlatın, sonra: ollama pull qwen2.5-coder:7b" } }));

  await page.goto('/soru-sor');
  await expect(page.getByTestId('nlq-durum')).toHaveText('Yerel modele ulaşılamadı', { timeout: 20_000 });
  await expect(page.getByText("Yerel AI'ya ulaşılamadı", { exact: false })).toBeVisible();
  await expect(page.getByText('Ürünün geri kalanı bundan etkilenmez', { exact: false })).toBeVisible();

  // Giriş kilitli — 300 sn'lik boşa bekleme hiç başlamaz.
  await expect(page.getByPlaceholder(/Sorunu yaz/)).toBeDisabled();
});
