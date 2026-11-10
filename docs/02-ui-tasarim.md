# 02 · Ekran (UI) Tasarımı — LST-KDS

> **Belgenin niteliği (retroaktif).** S1'in ikinci yarısı. Ekranlar kurulduktan sonra **gerçek
> uygulamadan** belgelendi: tasarım dili değerleri [frontend/src/theme.ts](../frontend/src/theme.ts)
> ile birebir; menü gerekçesi [05-menu-arastirma.md](05-menu-arastirma.md); gereksinim eşleşmesi
> [01-gereksinim-analizi.md](01-gereksinim-analizi.md). Amaç: yeni ekran eklerken tutarlılık kılavuzu.

## 1. Tasarım dili

**Kimlik.** Koyu lacivert + teal (login teması / analiz kapağı ile aynı). Ölçülü, "kurumsal ama
canlı" — BI aracına yakışan yoğun ama sakin bir yüzey.

| Rol | Değer | Kullanım |
|---|---|---|
| primary | `#0f2d4e` (lacivert) | Başlık şeridi, birincil aksiyon |
| secondary | `#0e6f7a` (teal) | Vurgu, sayı kartı rakamı, ikincil aksiyon |
| success | `#1c9d6b` | Başarı durumları |
| zemin / kağıt | `#f4f7fa` / `#ffffff` | Sayfa arkası / paneller |
| panel sınırı | `#e2e9f0` + çok hafif lacivert gölge | Outlined Paper'lar "yüzer" |

**Gradyanlar.** Birincil dolgulu buton ve sol marka rayı **lacivert→teal** gradyanı taşır
(`linear-gradient(135deg,#0f2d4e,#0e6f7a)`). KPI kartları 4'lü gradyan paleti (teal/amber/mor/mercan).

**Üst bant (`bantStili`, S43).** Bandın zemini ve üstündeki HER öğenin rengi (metin, solgun metin,
ikon kutusu, avatar) tek takımdan gelir; öğe renkleri ayrı ayrı `#fff` sabitlenirse açık zeminli
bir varyantta metin görünmez olur. İki tema **belirgin** ayrışır — kullanıcı bulgusu, eski iki
gradyan gözle seçilemiyordu:

| Tema | Bant zemini |
|---|---|
| açık | `linear-gradient(90deg, #106e86, #2ec4b6)` — canlı teal |
| koyu | `linear-gradient(90deg, #0b1c2c, #0d7a70)` — gece laciverti→teal |

Açık temada bant, rayın bittiği lacivertten değil doğrudan teal'den başlar: ray–bant köşesindeki
kaynaşma yalnız **koyu** temada geçerlidir, açıkta orada bilinçli bir sınır vardır.

**Yarıçap token'ları (net px — oval yasağı).** `sx` çarpanı oval çıkardığından sabit px kullanılır:
- `boxRadius = 10px` → kart · panel · diyalog · tablo kutusu
- `smallRadius = 8px` → buton · liste satırı · input

**Yoğunluk standardı (2026-08-06, kullanıcı: "yazılar çok büyük, kalabalık").** Taban 13px; başlıklar
küçük (h5 18 / h6 15.5); tüm kontroller `size="small"` + dar dolgu. **Tüm ekranlar aynı yoğunlukta.**

**Tipografi.** `"Segoe UI", Roboto, Arial` — ayrı webfont yok (sistem yığını; hız + tutarlılık).

## 2. Sayfa iskeleti ve navigasyon

**Kabuk.** `UygulamaKabugu` = üst AppBar + sol menü + içerik. Sol menü [05-menu-arastirma §3](05-menu-arastirma.md)
kümelerini uygular; öğeler **role göre** gösterilir/gizlenir. Alt bölgede kullanıcı bloğu (ad/rol/tenant + çıkış).

**Menü kümeleri (rol kapıları):**
1. **Çalışma alanı** (herkes) — Panolar · Raporlar · Soru Sor*
2. **Oluştur & Analiz** (G+A) — Sorgu Tasarımcısı · Ham SQL · Metrikler*
3. **Veri** (G+A) — Veri Haritaları · Bağlantılar
4. **Otomasyon** (G+A) — İçe Alım · Zamanlanmış Raporlar · Uyarılar*
5. **Yönetim** (yalnız A, akordeon) — Kimlik/Kullanıcılar · Denetim · Roles* · Satır Güvenliği* · Kuruluş* · Gömme* · Kullanım*

`*` = placeholder (BlankPage — ne zaman geleceği ekranda yazılı; "yarım özellik yok" ilkesi).

**Başlık şeridi (PageHeader).** Her ekranın tepesinde markalı band: sol ikon + başlık + açıklama satırı
(ör. "benim raporum · ekiple paylaşılan · 128 satır · 14 ms") + sağda aksiyon butonları. Band üstü butonlar
iki stil paylaşır: `bandButton` (outline, şeffaf) ve `bandButtonWhite` (beyaz dolgulu birincil).

## 3. Ekran envanteri (canlı)

| Ekran | Rota | Rol | Amaç ve ana bileşenler |
|---|---|---|---|
| **Panolar (galeri)** | `/panolar` | herkes | Pano kartları; oluştur/aç/düzenle/sil |
| **Pano görüntüleyici** | `/panolar/:id` | herkes (görünür) | react-grid-layout kart ızgarası; "Yerleşim" düzenleme modu; **pano-düzeyi filtre şeridi + "Uygula"**; "Yenile" |
| **Raporlar** | `/raporlar` (+ `/klasorler` `/favoriler` `/paylasilan`) | herkes | Klasör rayı (Tümü/Favoriler/Ekiple paylaşılan/klasörler + sayaç) + kartlar + arama + yıldız |
| **Rapor görüntüleyici** | `/raporlar/:id` | herkes (görünür) | Açılışta önbellekli sonuç; ECharts tür değiştirici; **parametre şeridi**; Yenile · Dışa Aktar · Tasarımcıda Aç |
| **Sorgu Tasarımcısı** | `/tasarimci` | G+A | Harita seçici + alan paneli (rol renkleri) + Kolonlar/Ölçüler + filtreler + **parametreler** + gruplama/sıralama/limit; React Flow ilişki kanvası; SQL'i Göster / Çalıştır sekmeleri; "Rapor Olarak Kaydet" |
| **Ham SQL** | `/sql` | G+A | Monaco SQL editörü (lazy-load, kendi chunk'ı) + bağlantı seçici + satır limiti + Ctrl+Enter + Dışa Aktar |
| **Veri Haritaları** | `/veri-haritalari` | G+A | Harita CRUD + editör: yaz-bul tablo, tembel kolon, FK-otomatik ilişki, dostça ad, kolon rolü |
| **Bağlantılar** | `/baglantilar` | G+A | Kart/tablo geçişli liste; yeni=modal, düzenle=çekmece; **Test Et**; veri modu |
| **İçe Alım İşleri** | `/ice-alim` | G+A | İş listesi (durum çipi: satır·sn / hata / hiç çalışmadı) + "Şimdi Çalıştır" + editör çekmecesi (kaynak→tablo/kolon→cron preset) |
| **Zamanlanmış Raporlar** | `/zamanlanmis-raporlar` | G+A | Abonelik kartları (durum çipi: alıcı·satır / hata / hiç gönderilmedi) + **"Şimdi Gönder"** + editör çekmecesi (rapor + biçim toggle + alıcı chip'leri + cron preset) |
| **Yönetim / Kimlik** | `/yonetim` | A | Kimlik + canlı rol denetimi + tenant kullanıcı listesi |
| **Yönetim / Denetim** | `/yonetim/denetim` | A | kim/SQL/zaman/durum/satır/süre; filtre + sunucu-tarafı sayfalama; satır→detay çekmecesi (tam SQL/parametre/hata) |

## 4. Ortak (paylaşılan) bileşenler

| Bileşen | Görev | Nerede |
|---|---|---|
| `PageHeader` (+ `bandButton`/`bandButtonWhite`) | Markalı başlık şeridi + aksiyon alanı | Tüm ekranlar |
| `ParametrePanel` (+ `ilkParametreDegerleri`) | Parametre tipine göre giriş (tarih/aralık/sayı/metin/evet-hayır/liste) | Tasarımcı önizleme · Rapor görüntüleyici · **Pano filtre şeridi** |
| `DashboardCard` | Raporu çalıştırıp görselleştirir; bağımsız yükleme/hata; `tazeleme`+`parametreDegerleri` ile yeniden koşar | Pano görüntüleyici · Galeri |
| `ResultTable` / `EChart` | Sonuç grid'i / grafikler (ECharts tree-shaken) | Görüntüleyici · Tasarımcı |
| `ExportMenu` (+ `apiDownload`) | CSV/Excel/PDF indirme; `Content-Disposition` dosya adı | Rapor görüntüleyici · Ham SQL |
| `LoadingCurtain` / `RunningIndicator` | Sayfa yükleme perdesi / koşum göstergesi | Genel |
| `reportChartOption` / `scoreCardValue` | Sonuç→grafik/sayı kartı mantığı (kart + görüntüleyici paylaşır) | reportVisual |

## 5. Etkileşim desenleri

- **Kart ↔ tablo geçişi** (Bağlantılar): yoğunluk tercihi kullanıcıda.
- **Yeni = modal, düzenle = çekmece (drawer)**: kısa oluşturma odaklı; düzenleme bağlamı korur.
- **Sekmeli sonuç** (Tasarımcı/SQL): "Üretilen SQL" ve "Sonuç" — şeffaflık (SQL'i Göster) ilkesi.
- **Önbellekli görüntüleyici**: ağır rapor ekranı kilitlemesin diye açılışta SON çalıştırma sonucu
  localStorage'tan gelir; taze veri "Yenile" ile. LRU + kota geri-kazanımı `reportCache`'te.
- **Uçuştaki koşumu iptal**: sayfadan ayrılınca `AbortController` ile sorgu iptal (unmounted setState yok).
- **Durum çipleri** (İçe Alım/Denetim): Başarılı·satır·sn / Hata (tooltip) / Hiç çalışmadı — renkle okunur.
- **Filtre "Uygula" akışı** (parametreli rapor + pano): giriş değeri ayrı, **uygulanan** değer ayrı;
  Uygula bastıkça uygulanan güncellenir → koşum tetiklenir (her tuşta değil).

## 6. Erişilebilirlik / responsive / tema

- Tüm kontroller klavye erişilebilir (MUI tabanı); band butonları odak halkasını korur.
- Izgara ekranlar (pano) `useContainerWidth` ile genişliğe uyum; geniş içerik (tablo/SQL) kendi
  `overflow-x` kabında kayar — sayfa gövdesi yatay kaymaz.
- Uygulama **tek tema** (aydınlık, marka renkleri) hedefler — v1'de koyu tema yok (bilinçli).

## 7. Yeni ekran eklerken

`.claude/skills/yeni-ekran` akışını izle; bu belgedeki **token'lara** (boxRadius/smallRadius, band
header, size="small" yoğunluğu, gradient birincil buton) uy. Yeni menü öğesi henüz tamamlanmadıysa
BlankPage placeholder + "ne zaman geleceği" metniyle ekle — asla yarım özellik gösterme.
