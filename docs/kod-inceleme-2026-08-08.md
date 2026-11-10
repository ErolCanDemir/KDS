# Kod İncelemesi — 2026-08-08 (v1 gövdesi, S4–S10 + tasarımcı genişletmeleri)

> **DURUM (aynı gün kapatıldı):** K1+O6 `d5104b3` · Y1 `9a5a1f7` (+test `46d8959`) · Y2 `6dfb151` ·
> Y3 `685b863` · O1 `38b726e` · O5 `ca2c184` · O2+O3 `5bd61d1` · O4 `92d1c5e` · O7 `d35f0fd` ·
> O8 `7ec6653`+`afd8dde` · O9 `b28aa72` · D1–D6 `a9d3d01` · test borçları `46d8959` (105/105).
> Y4 (tasarımcı refactor'u, 4 dilim): ortak+Kanvas `99abc22` · saf model.ts `e17190d` ·
> vitest altyapısı + 9 birim testi `b5ee12d` · 5 diyalog → Diyaloglar.tsx `e0866a5`
> (ana dosya 1860 → 1222 satır). **AÇIK MADDE KALMADI — inceleme tümüyle kapandı.**

Kapsam: backend (Api/Business/Calistirma/Dal/Contracts), frontend (React), testler, infra.
Yöntem: bu oturumda satır-satır çalışılan çekirdek + hedefli okuma (Program.cs, SorguController,
ScriptDomSorguBekcisi, SorguModelCalistirHandler, SorguDenetimDavranisi, HttpKullaniciBaglami,
istemci.ts, menu, csproj'lar, test envanteri). **Hiçbir düzeltme yapılmadı — yalnız rapor.**

## KRİTİK

- **K1 — Müşteri rolü raporu ÇALIŞTIRAMAZ.** `SorguController.cs:16` sınıf düzeyinde
  `[Authorize(Roles=Admin,Gelistirici)]`; oysa `RaporlarController` görüntülemeyi TÜM rollere açar.
  Senaryo: Ekip'le paylaşılan raporu `musteri` açar → Çalıştır/Yenile `/api/sorgu/calistir|ham`'a
  gider → 403. Ürünün ana vaadi (müşteri rapor tüketir) fiilen çalışmaz. Çözüm yönü: rapor-kimliğiyle
  koşan, RAPOR görünürlüğünü denetleyen ayrı uç (`/api/raporlar/{id}/calistir`).

## YÜKSEK

- **Y1 — Çifte satır sınırı tutarsızlığı (model yolu).** `SorguModelCalistirHandler.cs` yürütmeye
  `SatirSiniri:null` geçer → `SorguYurutHandler` varsayılan 1000 okur. Model `SatirLimiti=5000`
  ise SQL `TOP(5000)` üretilir ama okuma 1000'de kesilir; kullanıcı 1000 üstü limiti hiç alamaz.
- **Y2 — Para/decimal değerler double bağlanıyor.** `TipeCevir` money/decimal/numeric → `double`;
  `JsonCoz` sayıları long/double çözer. İkili-kayan hassasiyeti para karşılaştırmalarında sapma ve
  SqlParameter float çıkarımı → örtük dönüşümle indeks verimsizliği riski. `decimal` olmalıydı.
- **Y3 — ElleKosul yer tutucu değişimi düz `string.Replace`.** (a) İki tablo aynı `DostcaAd`'ı
  taşıyorsa `FirstOrDefault` sessizce ilkini seçer; (b) SQL string literal'i İÇİNDEKİ `{X.Y}` metni
  de değiştirilir → sessiz yanlış SQL.
- **Y4 — Tasarımcı monoliti.** `SorguTasarimcisiSayfasi.tsx` ~1900 satır: 4 adım, kanvas, 6 diyalog,
  kalıcılık tek dosyada. Bu oturumda bile aynı-dosya çakışmaları yaşandı; bölünmezse her yeni özellik
  regresyon yüzeyini büyütüyor.

## ORTA

- **O1 —** `KosulluOlcu` BenzersizSay + Kolon=null → `COUNT(DISTINCT 1)` ≤ 1 (anlamsız); üreticide
  guard yok, UI da engellemiyor.
- **O2 —** `kds.tasarimci.durum` sessionStorage şema kayması: type-assert'li parse yalnız JSON
  hatasını yakalar; alan TİPİ değişimlerinde (geçmişte `joinSecim` string→obje değişti) eski oturum
  verisi sessiz bozuk davranış üretir (ör. kenar etiketi `undefined`).
- **O3 —** Rapor sonuç önbelleği (`kds.rapor.sonuc.<id>`) tahliyesiz büyür; kota dolunca yeni
  raporlar sessizce önbelleksiz kalır — kullanıcı "son çalıştırma" kaybını fark etmez.
- **O4 —** Join ağacı sıra duyarlılığı: frontend BFS keşif sırası ilişki-listesi sırasına bağlı,
  backend eşit-derinlikte Guid'e sabitlendi → nadir durumlarda kanvasta gösterilen otomatik ebeveyn
  ile SQL'deki farklılaşabilir (görsel ≠ icra).
- **O5 —** Denetim kaydı parametre DEĞERLERİNİ saklamıyor (yalnız SQL şablonu) → "ne arandı" izi yok.
  Ayrıca scope'lu DbContext'te `SaveChangesAsync` o ana dek TRACKED tüm değişiklikleri de yazar
  (bugün pratikte boş; gelecek için tuzak).
- **O6 —** `HataYakalayici` SqlException mesajını istemciye aynen döner — bugün yalnız
  admin/geliştirici gördüğü için kabul edilmişti; K1 çözülüp müşteri koşumu açıldığında bilgi
  sızıntısına dönüşür (K1 ile birlikte ele alınmalı).
- **O7 —** Bekçi sözdizimseldir; "bağlantı login'i salt-okunur olmalı" varsayımı hiçbir yerde
  ZORLANMIYOR (Bağlantılar ekranında uyarı/doğrulama yok). Geniş yetkili login ile SELECT komşu
  DB/şemaları okuyabilir.
- **O8 —** `istemci.ts`'te AbortController yok: sayfadan ayrılınca uçuştaki istek unmounted state'e
  yazmaya çalışır (Rapor görünümünde Yenile→navigasyon senaryosu).
- **O9 —** Oturum yenileme: 401'de `oturumYenile()` var; otomatik sessiz yenileme (silent renew)
  yapılandırması bu incelemede doğrulanamadı — uzun oturumda tam yeniden-girişe düşme riski.

## DÜŞÜK

- **D1 —** Tasarım durumu kaydetme effect'i her tuş vuruşunda tam `JSON.stringify` (debounce yok).
- **D2 —** `kopukTablolar` denetimi yalnız kanvas alanlarını kapsar; elle koşul/bileşik/özetle gelen
  tablolar için erken uyarı yok (backend anlaşılır hata veriyor — UX eksiği).
- **D3 —** Grid istemci-taraflı (10k satıra kadar bellekte) — bilinçli; dokümante edilmeli.
- **D4 —** NCCI kolon listeleri elle bakım ister; rapora yeni kolon girince indekse ekleme unutulabilir.
- **D5 —** `kds-realm.json` dev kimlikleri (admin/admin…) repo'da — prod realm'in ayrı olduğu
  hiçbir yerde işaretli değil.
- **D6 —** StrictMode'da mount effect'leri dev'de 2× (harita fetch, önbellek okuma) — idempotent
  görünüyor, yine de bilinçli olunmalı.

## Eksik test listesi (öneri)

- Bekçi: `SorguBekcisiTestleri`nde `[Fact]`=0 (muhtemelen Theory) — CTE, `EXEC`, `INSERT..SELECT`,
  çoklu ifade, `OPENROWSET`, `fn_trace_gettable`, `SELECT INTO` şekillerinin tek tek kapsandığı teyit edilmeli.
- ElleKosul: literal-içi yer tutucu; DostcaAd çakışması.
- KosulluOlcu: BenzersizSay+kolonsuz reddi/uyarısı.
- JoinAyarlari × joinSecim (kolon override) kombinasyonu; KokTabloId × elle Gruplamalar; eşit-derinlik
  JOIN sırası golden'ı.
- Rapor mod geçişi: model→sql→model güncellemesinde karşı alanların temizlendiği.
- Y1 senaryosu: model SatirLimiti > 1000 iken dönen satır sayısı.
- Tenant çapraz-okuma NEGATİF testleri (başka tenant'ın rapor/bağlantı/harita Id'siyle istek).
- Frontend: hiç test yok (en azından modelKur/agac saf fonksiyonlaştırılıp birim test).

## Doğrulananlar (güçlü yanlar)

- İki-dünya ayrımı derlemede zorlanıyor (Calistirma → Dal referansı yok; yalnız Contracts).
- `FallbackPolicy=RequireAuthenticatedUser` — [Authorize] unutulan uç anonim AÇILMAZ; CORS tek origin;
  eksik config sessiz varsayılana düşmek yerine açıkça patlıyor.
- Bekçi fail-closed BEYAZ-liste (tablo kaynakları) + tek ifade + INTO reddi; üreticide kimlikler
  daima köşeli-parantez/escape, değerler DAİMA parametre.
- Rapor dual-mode kayıtta karşı alanlar temizleniyor; SQL raporu kayıt ANINDA da bekçiden geçiyor.
- Denetim davranışı hatayı yutmuyor, iptalden bağımsız yazıyor; `Hata` 1000'de kırpılıyor; `Sql`
  kolonu max (kırpılma yok).
- Tenant global query filter'ları tüm tenant varlıklarında; JIT kullanıcı sağlama middleware'de.
- 65 birim test yeşil (üretici 40: golden + güvenlik + yeni özellik senaryoları); Harita concurrency
  düzeltmesi regresyon testli.

## Önerilen ele alma sırası

1) K1 (+O6 birlikte) → 2) Y1 → 3) Y2+Y3 → 4) O1, O5 → 5) O2/O3/O4 → 6) Y4 (refactor, ayrı sakin dilim)
→ 7) kalan Düşükler + test borçları.
