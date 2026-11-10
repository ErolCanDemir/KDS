import { test, expect } from './fixtures';
import { hamSqlRaporVarEt } from './apiYardimci';

// S20 Uyarılar — uçtan uca: uyarı kur → listede kuralıyla görün → "Şimdi Dene" ölçsün → sil.
// Birim testler karar mantığını (özet/eşik/bildirim kuralı) zaten kaplıyor; buradaki risk BAĞLANTI
// HATTIdır: ekran → API → rapor koşumu → ölçüm → ekrana dönen mesaj.
//
// "Şimdi Dene"nin e-posta ATMADIĞI da burada doğrulanır: kurulum sırasında deneme yapan kişi
// alıcılara posta yağdırmamalı — bu, özelliğin kullanılabilirliğini belirleyen bir davranış.

const RAPOR = 'E2E Uyari Kaynak';
const UYARI = 'E2E Uyari';

test('uyarı: kur → listede kuralıyla görün → Şimdi Dene ölçsün (posta atmadan) → sil', async ({ page, request }) => {
  test.setTimeout(90_000);
  await hamSqlRaporVarEt(request, RAPOR);

  await page.goto('/uyarilar');
  await expect(page.getByRole('heading', { name: 'Uyarılar' })).toBeVisible({ timeout: 20_000 });

  await page.getByRole('button', { name: 'Yeni Uyarı' }).click();
  const d = page.getByRole('dialog').or(page.locator('.MuiDrawer-paper')).first();
  await d.getByLabel('Uyarı adı').fill(UYARI);

  // MUI Select role="combobox" verir (getByLabel eşlemez). Çekmecedeki ilk combobox = rapor seçimi.
  await d.getByRole('combobox').first().click();
  await page.getByRole('option', { name: RAPOR, exact: true }).click();

  // Alıcı zorunlu: alıcısız uyarı kimseye haber veremez, bu yüzden hem sunucu doğrulaması hem
  // Kaydet düğmesi bunu şart koşar. (Düğmenin sunucu kuralını yansıttığını da burada sınıyoruz.)
  await expect(d.getByRole('button', { name: 'Kaydet' })).toBeDisabled();
  await d.getByPlaceholder('ad@firma.com').fill('e2e@kds.local');
  await d.getByRole('button', { name: 'Ekle' }).click();

  // Varsayılan kural "Satır sayısı > 0" — en yaygın biçim ("kayıt VARSA haber ver").
  await d.getByRole('button', { name: 'Kaydet' }).click();

  // Listede kural CÜMLE gibi okunur; ekranın işi eşiği anlaşılır kılmaktır.
  const kart = page.locator('div').filter({ hasText: UYARI }).first();
  await expect(kart).toBeVisible({ timeout: 15_000 });
  await expect(page.getByText('Satır sayısı > 0').first()).toBeVisible({ timeout: 10_000 });
  await expect(page.getByText('Hiç kontrol edilmedi').first()).toBeVisible();

  // Şimdi Dene: ölçer, sonucu yazar ve POSTA ATMADIĞINI açıkça söyler.
  // Düğme testin KENDİ kartından seçilir — `.first()` listedeki başka bir uyarıya basabilirdi
  // (aynı tuzak abonelik testinde de vardı; 20 Ağu 2026'da yakalandı).
  // Kart seçimi iki koşulla daraltılır: adı geçen VE 'Şimdi Dene' düğmesi olan Paper. Yalnız
  // hasText yetmiyordu — bilgi şeridi (MuiAlert) de bir Paper'dır ve mesajında uyarının adı geçer,
  // DOM'da kartlardan da önce gelir; '.first()' onu yakalayıp tıklama zaman aşımına düşüyordu.
  const kendiKart = page.locator('.MuiPaper-root')
    .filter({ hasText: UYARI })
    .filter({ has: page.getByRole('button', { name: 'Şimdi Dene' }) })
    .first();
  await kendiKart.getByRole('button', { name: 'Şimdi Dene' }).click();
  await expect(page.getByText(/ölçülen değer/)).toBeVisible({ timeout: 60_000 });
  await expect(page.getByText(/e-posta gönderilmedi/)).toBeVisible();
  // 5 satırlık kaynak → eşik (0) aşılır.
  await expect(page.getByText('Tetiklendi').first()).toBeVisible({ timeout: 10_000 });

  // Sil (onay diyaloğu) — teardown zaten "E2E " önekini temizler ama silme yolu da sınanır.
  // Silme de KENDİ kartından — `.last()` listedeki başka bir uyarıyı silebilirdi.
  await kendiKart.getByRole('button', { name: 'Sil' }).click();
  await page.getByRole('button', { name: 'Sil', exact: true }).last().click();
  // KART gitmeli — metin değil: uyarının adı, "Şimdi Dene" sonrası bilgi şeridinde hâlâ yazılı
  // olduğu için metne bakan iddia silme başarılı olsa bile düşerdi.
  await expect(kendiKart).toHaveCount(0, { timeout: 15_000 });
});
