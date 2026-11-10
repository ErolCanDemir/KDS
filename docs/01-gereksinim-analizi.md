# 01 · Gereksinim Analizi — LST-KDS

> **Belgenin niteliği (retroaktif).** S1 dilimi (gereksinim + ekran tasarımı), kullanıcı kararıyla
> S2'nin (backend iskelet) ardına alınmıştı (roadmap karar günlüğü, 2026-08-04). Bu belge, v1 büyük
> ölçüde tamamlandıktan sonra **kurulmuş üründen türetilerek** yazıldı: her fonksiyonel gereksinim
> (FG) gerçekte teslim edilmiş bir dilime bağlanır → sonraki dilimler ve testler için **izlenebilirlik**
> sağlar. Vizyon/kararların kaynağı [00-analiz.md](00-analiz.md); rakip çerçevesi
> [06-rakip-analizi.md](06-rakip-analizi.md); ekran gerekçesi [05-menu-arastirma.md](05-menu-arastirma.md);
> ekran tasarımı ayrıntısı [02-ui-tasarim.md](02-ui-tasarim.md).

## 1. Amaç ve kapsam

**Ürün özeti (00-analiz §1).** SQL bilmeyen kullanıcının, müşterinin verisi üzerinde **sürükle-bırakla
sorgu tasarlayıp** sonucu tablo/grafik/panoya bağlayabildiği; SQL bilen kullanıcının aynı yerde ham SQL
çalıştırabildiği, **çok kiracılı (SaaS)** raporlama ürünü. Turnusol testi: *"Bu rapor işi için IT'ye
gitmem gerekir mi?"* → her v1 özelliği bu cevabı "hayır"a yaklaştırır.

**İlke — "yarım özellik yok".** Bir yetenek ya tamamdır ya da UI'da **gizlidir** (SQLST geleneği). Sol
menüdeki henüz gelmemiş öğeler bilinçli "placeholder" ekranlarla, ne zaman geleceği yazılı olarak durur.

## 2. Aktörler ve roller

| Aktör | Rol (Keycloak) | Özet yetki |
|---|---|---|
| Yönetici | `admin` | Her şey + Yönetim (kullanıcı/rol/denetim) |
| Geliştirici/Analist | `gelistirici` | Bağlantı/harita/sorgu/rapor/pano/içe-alım tasarımı; Yönetim yok |
| İzleyici/Müşteri | `musteri` | Yalnız kendine görünür rapor/panoları görüntüler + çalıştırır; tasarım yok |
| Kiracı (Tenant) | — | Veri izolasyon sınırı: her kayıt `TenantId` ile filtrelenir |

Roller **hiyerarşik değil**, yetenek-kapılıdır (menü ve uçlar rol listesine göre gösterilir/gizlenir).

## 3. Fonksiyonel gereksinimler

Durum: ✅ teslim · 🟡 kısmi/borçlu · ⬜ v1 sonrası. "Dilim" = roadmap S-no. FG-ID biçimi `FG-<alan>-<n>`.

### 3.1 Kimlik ve kiracılık (FG-KIM) — Dilim S3

| ID | Gereksinim | Dilim | Durum |
|---|---|---|---|
| FG-KIM-1 | Keycloak OIDC ile giriş (PKCE, public client); markalı Türkçe login teması | S3 | ✅ |
| FG-KIM-2 | Üç rol (admin/gelistirici/musteri); rol-bazlı menü + uç koruması | S3 | ✅ |
| FG-KIM-3 | Kiracı izolasyonu: `TenantId` global sorgu filtresi — bir tenant diğerinin verisini göremez | S3 | ✅ |
| FG-KIM-4 | İlk girişte kullanıcının yerel metadata'ya **JIT provizyonu** (yarış-güvenli, idempotent) | S3 | ✅ |
| FG-KIM-5 | Token deposu `sessionStorage`; 401 → yeniden giriş; ROPC (password grant) kapalı | S3 | ✅ |

### 3.2 Bağlantılar (FG-BAG) — Dilim S4

| ID | Gereksinim | Dilim | Durum |
|---|---|---|---|
| FG-BAG-1 | Bağlantı CRUD (sunucu/veritabanı/kullanıcı/mod) | S4 | ✅ |
| FG-BAG-2 | Parola **Data Protection** ile şifreli saklanır; API yanıtında **sızmaz** | S4 | ✅ |
| FG-BAG-3 | "Test Et" = gerçek ADO.NET `SELECT 1` (kullanıcı boşsa entegre kimlik) | S4 | ✅ |
| FG-BAG-4 | Veri alma modu bağlantıda seçilir: **Canlı** \| **İçe Alım** (K11) | S4 | ✅ |

### 3.3 Veri haritası (FG-HAR) — Dilim S6

| ID | Gereksinim | Dilim | Durum |
|---|---|---|---|
| FG-HAR-1 | Şema keşfinden **yaz-bul** tablo ekleme (600+ tablo → server-side LIKE + top-N) | S6 | ✅ |
| FG-HAR-2 | Tembel kolon yükleme; dostça (kullanıcı-dostu) tablo/kolon adları | S6 | ✅ |
| FG-HAR-3 | İlişki (join): FK'dan **otomatik öneri** + elle düzenleme | S6 | ✅ |
| FG-HAR-4 | Kolon rolü: kırılım (boyut) / sayısal (ölçü) / anahtar / gizli | S6 | ✅ |
| FG-HAR-5 | Mod bağlantıdan **devralınır** (harita override'ı kaldırıldı — K11 revize) | S6 | ✅ |

### 3.4 Sorgu ve çalıştırma (FG-SOR) — Dilimler S5/S7/S8/S9

| ID | Gereksinim | Dilim | Durum |
|---|---|---|---|
| FG-SOR-1 | **SELECT-only bekçisi** (ScriptDom AST, beyaz-liste, fail-closed) — tek kapı | S5 | ✅ |
| FG-SOR-2 | Çalıştırıcı: timeout + satır sınırı + iptal; her sorgu **denetime** yazılır | S5 | ✅ |
| FG-SOR-3 | JSON sorgu modeli (AST): kolon/ölçü, **filtre ağacı (AND/OR)**, GROUP BY, sıralama, TOP | S7 | ✅ |
| FG-SOR-4 | SQL üretici: nesne adları QUOTENAME, değerler **parametreli (@pN)**; join'ler haritadan BFS | S7 | ✅ |
| FG-SOR-5 | Sürükle-bırak tasarımcı ekranı: alan paneli + kolon/ölçü + filtre + gruplama + sıralama + limit | S8 | ✅ |
| FG-SOR-6 | "SQL'i Göster" (şeffaflık) + "Çalıştır" → Üretilen SQL / Sonuç grid sekmeleri | S8 | ✅ |
| FG-SOR-7 | Ham SQL ekranı (Monaco), Ctrl+Enter; aynı SELECT-only tek kapıdan geçer | S9 | ✅ |

### 3.5 Rapor ve görselleştirme (FG-RAP) — Dilim S10 (+ parametreli 2026-08-10)

| ID | Gereksinim | Dilim | Durum |
|---|---|---|---|
| FG-RAP-1 | Rapor = sorgu modeli **veya** ham SQL + görselleştirme tercihi; klasör + favori | S10 | ✅ |
| FG-RAP-2 | Görselleştirme: tablo / çubuk / çizgi / pasta / sayı kartı (ECharts) | S10 | ✅ |
| FG-RAP-3 | Paylaşım: Özel (sahip+admin) \| Ekip (tenant); müşteri paylaşılanı çalıştırır | S10 | ✅ |
| FG-RAP-4 | Görüntüleyici: son çalıştırma sonucu tarayıcıda önbellekli; "Yenile" ile taze | S10 | ✅ |
| FG-RAP-5 | **Parametreli rapor**: çalıştırma-anı değerler (metin/sayı/tarih/**tarih aralığı**/evet-hayır/liste); filtre bir parametreye bağlanır | S10+ | ✅ |
| FG-RAP-6 | Rapor tasarımcıya `?rapor=id` ile geri yüklenir (param-bağı dahil) | S10+ | ✅ |

### 3.6 Panolar (FG-PANO) — Dilim S11 (+ yerleşim/filtre)

| ID | Gereksinim | Dilim | Durum |
|---|---|---|---|
| FG-PANO-1 | Pano = adlandırılmış **sıralı rapor koleksiyonu**; CRUD; Özel/Ekip görünürlük | S11 | ✅ |
| FG-PANO-2 | Görüntüleyici: raporlar canlı kart ızgarasında; her kart bağımsız yükler | S11 | ✅ |
| FG-PANO-3 | **Widget yerleşimi**: react-grid-layout ile sürükle-bırak konum/boyut, kalıcı | S11+ (caeaa24) | ✅ |
| FG-PANO-4 | **Pano-düzeyi filtreler**: raporların parametreleri birleşik şeritte → "Uygula" her karta taşır | S11+ (2026-08-10) | ✅ |

### 3.7 İçe alım modu (FG-ICE) — Dilim S12

| ID | Gereksinim | Dilim | Durum |
|---|---|---|---|
| FG-ICE-1 | İçe alım işi CRUD; kaynak = İçe-Alım modlu bağlantı → tablo/kolon seçimi | S12 | ✅ |
| FG-ICE-2 | Çalıştırıcı: depo DB'yi (`KdsDepo_<tenant>`) otomatik yaratır, tabloyu introspection'la türetir, **SqlBulkCopy** ile tam yükler | S12 | ✅ |
| FG-ICE-3 | **Cron zamanlama** (Cronos, DST-doğru); BackgroundService tüm tenant'ların vadesi geleni tetikler | S12 | ✅ |
| FG-ICE-4 | Rapor yönlendirme: bağlantı İçe-Alım ise sorgu **depoya** gider (aynı üretici/bekçi/audit; başarı ölçütü #3) | S12 | ✅ |
| FG-ICE-5 | Elle "Şimdi Çalıştır" + durum çipi (satır/süre/hata) | S12 | ✅ |

### 3.8 Dışa aktarım (FG-DIS) — Dilim S13-1

| ID | Gereksinim | Dilim | Durum |
|---|---|---|---|
| FG-DIS-1 | Sonuç dışa aktarımı: **CSV** (UTF-8 BOM) / **Excel** (ClosedXML) / **PDF** (PDFsharp-MigraDoc, MIT) | S13 | ✅ |
| FG-DIS-2 | Export tek kapıdan (audited) geçer; dosya adı `Content-Disposition` ile korunur | S13 | ✅ |

> Not: PDF export 00-analiz §11'de başta **DIŞI** idi; v1 sırasında (S13) kapsama alındı — bkz. §6.

### 3.9 Denetim ve olay günlüğü (FG-DEN / FG-LOG) — Dilimler S13-2 / S14

| ID | Gereksinim | Dilim | Durum |
|---|---|---|---|
| FG-DEN-1 | Denetim ekranı (Admin): kim/SQL/zaman/durum/satır/süre; filtre + sunucu-tarafı sayfalama; detay çekmecesi | S13 | ✅ |
| FG-DEN-2 | "Kim" alanı Keycloak sub → yerel kullanıcıya çözülür (eskiden boştu) | S13 | ✅ |
| FG-LOG-1 | Olay günlüğü (Mongo `KdsLog`): Action/Exception/DataChange, `ActionId` korelasyonlu; istek bloklanmaz | S14 | ✅ |

### 3.10 Zamanlanmış raporlar / abonelik (FG-ABONE) — Dilim S15

| ID | Gereksinim | Dilim | Durum |
|---|---|---|---|
| FG-ABONE-1 | Rapor aboneliği CRUD: rapor + biçim (PDF/Excel/CSV) + alıcı e-postalar + cron/elle + aktif | S15 | ✅ |
| FG-ABONE-2 | Elle "Şimdi Gönder": rapor koşulur → biçime çevrilir → alıcılara e-postalanır; durum yazılır | S15 | ✅ |
| FG-ABONE-3 | Zamanlayıcı: dakikada bir vadesi gelen aktif+cron'lu abonelikleri tenant adına gönderir (Cronos, İçe Alım deseni) | S15 | ✅ |
| FG-ABONE-4 | E-posta taşıma takılabilir: SMTP yapılandırılmışsa gerçek gönderim, değilse (dev) günlüğe yazan gönderici | S15 | ✅ |

### 3.11 v1 sonrası / placeholder (FG-GELECEK)

Sol menüde **görünür ama ekranı boş** (BlankPage) — ne zaman geleceği yazılı: Soru Sor (NLQ), Metrikler
(ölçüt kataloğu), **Uyarılar** (eşik/anomali — S15 zamanlayıcı altyapısını paylaşacak), Yönetim alt-öğeleri
(Roles & Yetkiler, Satır Güvenliği, Kuruluş Ayarları, Gömme & Marka, Kullanım). Ayrıntı:
[05-menu-arastirma.md](05-menu-arastirma.md) + araştırma kuyruğu.

## 4. Fonksiyonel-olmayan gereksinimler (NFG)

| ID | Gereksinim | Kanıt / dilim |
|---|---|---|
| NFG-GUV-1 | **SELECT dışı hiçbir komut** hiçbir yoldan koşamaz — bekçi **+ read-only DB kullanıcı** iki ayrı katman (başarı ölçütü #4) | S5 bekçi 28 vaka + S13 `readonly-user.sql`/`ReadOnlyKullaniciTests` |
| NFG-GUV-2 | SQL enjeksiyonu: tüm değerler parametreli; nesne adları yalnız haritadan + QUOTENAME | S7 adversarial review |
| NFG-GUV-3 | Kiracı sızıntısı yok: global filter + tenant-başına depo DB + audit | S3 izolasyon testleri |
| NFG-PERF-1 | Canlı modda 5M satırda ilk sayfa **< 3 sn**; hiçbir sorgu sınırsız koşamaz | S13 `PerformansTests`: ilk sayfa 1402 ms, agregasyon 788 ms (KDS_PERF=1) |
| NFG-LIS-1 | Her paket lisans denetiminden geçer; ticari-eşik/GPL yok (MediatR 12.x sabit, QuestPDF yerine MIT PDFsharp) | 00-analiz §9 + Directory.Build.props NuGetAudit |
| NFG-I18N-1 | Arayüz + wire Türkçe; kolon/rota/menü İngilizce'ye kaymaz (regresyon guard) | S11 whoami `roller` alan testi |
| NFG-DAG-1 | Tek-VM Docker Compose ile yayın; migration yayında otomatik | S13-4 (compose yazıldı; canlı koşu Docker'a bağlı 🟡) |

## 5. İzlenebilirlik (başarı ölçütü → FG)

| Başarı ölçütü (00-analiz §12) | Karşılayan FG'ler |
|---|---|
| #1 SQL'siz join+grup+filtre rapor → grafik → pano | FG-HAR-3, FG-SOR-3/5, FG-RAP-2, FG-PANO-1 |
| #2 5M satırda ilk sayfa < 3 sn; sınırsız yok | NFG-PERF-1, FG-SOR-2 |
| #3 İçe alım aynı raporu depodan aynı sonuçla; mod geçişi bozmaz | FG-ICE-2/3/4 |
| #4 SELECT dışı hiçbir komut koşamaz (test kanıtlı, iki katman) | NFG-GUV-1, FG-SOR-1 |

## 6. Kapsam evrimi (v1 sırasında bilinçli değişiklikler)

- **PDF export** 00-analiz §11'de DIŞI iken S13'te **içeri** alındı (MIT PDFsharp ile lisans-güvenli).
- **Otomasyon** (zamanlanmış rapor e-postası + uyarılar) araştırma kuyruğundan **v1 önerisine** alındı
  (2026-08-05, rakip analizi Kademe 1) — menüde placeholder olarak duruyor.
- **Harita mod override'ı** kaldırıldı; mod yalnız bağlantıda (K11 revize, S6).
- **Parametreli rapor** ve **pano-düzeyi filtreler** başlangıç kapsamında yoktu; kullanıcı isteğiyle
  eklendi (2026-08-10) — FG-RAP-5/6, FG-PANO-4.

Değişmeyen çekirdek kararlar için [00-analiz §2 (K1–K11)](00-analiz.md); pazar konumu için
[06-rakip-analizi §8 boşluk analizi](06-rakip-analizi.md).
