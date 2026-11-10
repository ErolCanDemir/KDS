# KDS · Yol Haritası ve İş Sırası

> **Çalışma kuralı (SQLST ile aynı):** İşler bu sırayla yapılır; sıra değişecekse önce
> burada değiştirilir. Roadmap TEK iş takip yeridir — biten her iş ve tüm gelecek işler
> buraya yazılır; işi nasıl yaptığımız da dilim satırına işlenir.
> **Bağımlılık kuralı:** Bağımlı iş, dayandığı iş bitmeden başlatılmaz; yeni iş eklerken
> önce bağımlılıklar tespit edilir ve plana öyle yerleştirilir.
> Her dilimin bitişi = derleme temiz + testler yeşil + uygulama çalıştırılıp elle
> doğrulandı + **onaylı** commit. Durum: ⬜ Bekliyor · 🔵 Devam ediyor · ✅ Tamam.
> Analiz ve bağlayıcı kararlar (K1–K11): [00-analiz.md](00-analiz.md).

> **Sıradaki gündem (kullanıcı, 2026-08-04):**
> 1. ~~Veritabanı~~ ✅ (metadata çekirdeği — [04-veri-modeli.md](04-veri-modeli.md)).
> 2. ~~Site tasarımı~~ ✅ (kullanıcı 10 mockup'tan **07 gradyan ray + 04 renkli KPI** karmasını
>    seçti; gerçek MUI uygulamasına uygulandı — gradyan sol ray + LST-KDS + ikonlu menü + alt
>    kullanıcı bloğu + renkli KPI'lı Panolar dashboard'u).
> 3. ~~Sol/iç menü~~ ✅ (**akordeon** iç menü [A] + **tablo** iç sayfa deseni [A]; rol-bazlı
>    görünürlük korunur — musteri yalnız Panolar+Raporlar). Login/app markası **LST-KDS**.
> 4. ~~Rakip/piyasa analizi~~ ✅ ([06-rakip-analizi.md](06-rakip-analizi.md)) → eksik özellikler
>    menüye eklendi (Soru Sor/NLQ · Otomasyon: Zamanlanmış Raporlar+Uyarılar · RLS · Gömme).
>    Öncelik: RLS/rapor/uyarı **v1**, NLQ **v1.x**, gömme **v2** (06 §9).
> 1-3 canlı (admin/musteri) + hepsinde build/lint temiz doğrulandı.

## v1 — "İlk uçtan uca rapor" (hedef sıra)

| # | Dilim | Kapsam | Durum |
|---|---|---|---|
| S0 | **Analiz + roadmap** | [00-analiz.md](00-analiz.md): vizyon, K1–K11 kararları, iki çalıştırma modu analizi (DirectQuery/Import), iki veri dünyası, güvenlik çerçevesi, teknoloji/lisans denetimi, K8s kararı (v1'de yok), riskler + bu roadmap; analiz PDF'i + pdf-uret.ps1 (Edge headless, SQLST'den uyarlama) | ✅ (2d46cf4) |
| S1 | **Gereksinim + ekran tasarımı** | **✅ (2026-08-10 — retroaktif; S2'den sonra yapıldı, kullanıcı kararı):** [01-gereksinim-analizi.md](01-gereksinim-analizi.md) (FG-ID'li fonksiyonel + NFG gereksinimler → her biri teslim edilmiş dilime + başarı ölçütüne **izlenebilir**; kapsam evrimi) + [02-ui-tasarim.md](02-ui-tasarim.md) (tasarım dili token'ları theme.ts ile birebir + ekran envanteri + ortak bileşenler + etkileşim desenleri). Rakip incelemesi ayrı derinleşti → [06-rakip-analizi.md](06-rakip-analizi.md). Belgeler kurulmuş üründen türetildi (ürün v1'i büyük ölçüde bitmişti). | ✅ |
| S2 | **Mimari + solution iskeleti** | **Backend ✅ (2026-08-04 — kullanıcı kararıyla S1'den önce yapıldı):** [03-mimari.md](03-mimari.md) + KDS.slnx: Api / Business (MediatR **12.5.0 sabit** + FluentValidation + DogrulamaDavranisi pipeline) / Dal (EF Core 10 + ince UnitOfWork, internal) / Contracts (IKomut/ISorgu işaretleri) / **Calistirma** (çalıştırma dünyası sözleşmeleri — Dal'a referans yasağı derlemede); Directory.Build.props (net10.0 + TreatWarningsAsErrors — NU1903 güvenlik denetimi iki geçişli paketi yamaya sabitletti: Microsoft.OpenApi 2.11.0, SQLitePCLRaw 2.1.12); nuget.config yalnız nuget.org; dikey kanıt `GET /api/durum` canlı doğrulandı + OpenAPI belgesi; **5/5 test yeşil** (CQRS hattı, doğrulama davranışı ret+geçiş, UoW transaction+hata). **Frontend + altyapı ✅ (2026-08-04):** React 19 + Vite + TS + **MUI** (kullanıcı kararı) — tema (ürün kimliği renkleri), UygulamaKabugu (AppBar + 7 ekranlı menü), yönlendirme, `apiGetir` istemcisi, Panolar'da `/api/durum` canlı kanıt kartı; **React Flow (@xyflow/react, MIT) sorgu tasarımcısı kanvası çalışır durumda** (palet → sürükle-bırak → TabloDugumu kartı → kolon tutamaçlarından JOIN kenarı; S6/S8'de gerçek modele bağlanır); kimlik altyapısı react-oidc-context (KimlikSaglayici/KorumaliAlan, geçici `VITE_KIMLIK_KAPALI` bayrağı); docker-compose (Keycloak 26.3 + MSSQL 2022); backend'e Keycloak JWT bearer + KeycloakRolDonusumu + Roller sabitleri + `[Authorize]` dikey kanıtı (`/api/durum/kimlik` token'sız 401 canlı doğrulandı) + CORS; frontend build temiz, backend 5/5 test. **Borç:** Docker bu makinede yok → compose canlı doğrulaması + gerçek Keycloak login'i S3'te | ✅ (1eebb9b) |
| S3 | **Kimlik + tenant temeli** *(bağımlı: S2)* | **Login + rol ✅ (2026-08-04):** Keycloak **Docker'sız** kuruldu (ZIP + JDK 17 — Docker yok; `infra/keycloak-indir.ps1`+`keycloak-baslat.ps1`, saf ASCII); deklaratif `kds` realm'i (`infra/kds-realm.json`) — `kds-web` (public+PKCE, `kds-api` audience + `tenant_id` claim mapper), `kds-api` (bearer-only), roller admin/gelistirici/musteri, tenant nitelikli 3 test kullanıcısı; `VITE_KIMLIK_KAPALI` varsayılanı false; frontend jeton köprüsü + oturum bağlamı (rol/tenant rozetleri) + Yönetim ekranı (kimlik + canlı rol denetimi); backend audience + `tenant_id` claim + `[Authorize(Roles=admin)]` uç. **Canlı kanıt:** token'da aud/roller/tenant doğru; API token'sız 401, geçerli 200, rol zorlaması 200/403; **Edge'de admin ve musteri login akışları uçtan uca** (ekran görüntülü). **Kod inceleme sertleştirmesi ✅ (2026-08-04, 7 bulgu):** güvensiz-dev değerleri `appsettings.Development.json`'a ayrıldı (taban güvenli; eksik config açılışta patlar), fallback auth policy (RequireAuthenticatedUser), `kds-web`'de **ROPC kapatıldı** (password grant `unauthorized_client` — canlı), token deposu `sessionStorage`, rol-bazlı menü (musteri yalnız Panolar+Raporlar — canlı), savunmacı rol dönüşümü, 401→yeniden giriş; realm dev-only olarak belgelendi. Backend 5/5 + build/lint temiz. **Rol/kullanıcı adları + login teması ✅ (2026-08-04):** roller yonetici/tasarimci/izleyici → **admin/gelistirici/musteri**; özel **KDS login teması** (marka gradyanı + teal buton + wordmark) + **Türkçe arayüz** (realm i18n); canlı doğrulandı. **Login yeniden tasarım + marka ✅ (2026-08-04):** kullanıcı 5 mockup arasından "Panel Böl" (split) yönünü seçti → `template.ftl` override ile iki sütunlu login (sol marka paneli: wordmark+grafik+maddeler, sağ form, lacivert buton); tüm görünür marka **LST-KDS** (login wordmark + sekme başlığı + app bar + app sekmesi); canlı doğrulandı. **Metadata veritabanı ✅ (2026-08-04):** [04-veri-modeli.md](04-veri-modeli.md); EF Core entity'ler (`Varlik`/`ITenantVarlik` temeli, `Tenant`, `Kullanici`) + `TenantId` global query filter + `ITenantBaglami` (token'dan tenant) + **JIT provisioning** (ilk girişte Keycloak kullanıcısı local'e, yarış-güvenli) + migration `IlkMetadata` (LocalDB'ye dev'de otomatik uygulanır, 2 tohum tenant) + Yönetim ekranında tenant kullanıcı listesi. `MapInboundClaims=false` (sub/tenant_id ham). **Canlı + test kanıtı:** admin/gelistirici (Tenant A) birbirini görür, musteri (Tenant B) yalnız kendini (izolasyon); **7/7 test** (global-filter izolasyonu + JIT idempotensi dahil). **S3 tamam.** | ✅ |
| S4 | **Bağlantı yönetimi** *(bağımlı: S3)* | **✅ (2026-08-05):** `Baglanti` entity + `BaglantiEkle` migration (LocalDB `Baglantilar`); CQRS (listele/ekle/güncelle/sil/test-form/test-by-id); parola **Data Protection** ile şifreli (`ISifreKoruyucu`→Api `SifreKoruyucu`), `BaglantiOzeti` parolayı döndürmez; **"Test Et" = gerçek ADO.NET** (`Kds.Calistirma/SqlBaglantiDenetci`, Microsoft.Data.SqlClient, `SELECT 1`; kullanıcı boşsa entegre kimlik → LocalDB); **"Veri alma modu"** (K11, string kolon; ad kullanıcı kararı); uçlar Admin+Geliştirici; Api JSON string enum; `Dal→Contracts` (paylaşılan enum). Frontend: kart/tablo geçişli ekran + yeni=modal + düzenle=çekmece + Test Et; kaydetten sonra otomatik test-by-id (durum ana ekrana yansır). **Canlı: LocalDB'ye gerçek bağlantı doğrulandı; 12/12 test.** | ✅ |
| S5 | **Çalıştırma çekirdeği** *(bağımlı: S4)* | **✅ (2026-08-05):** SELECT-only bekçisi (`ScriptDomSorguBekcisi` — ScriptDom AST, **beyaz-liste**, fail-closed) + `SqlSorguCalistirici` (saf ADO.NET, **tek kapı**: timeout/satır sınırı/iptal) + `SqlSemaKesfi` (`sys.*`) + **denetim** (`DenetimKaydi` + migration + pipeline davranışı: başarılı+engellenmiş yazar, yutmaz) + `SorguYurutHandler` orkestratör. **Code review** (düşmanca): tablo-değerli fonksiyon açığı (`fn_get_audit_file`/`fn_trace_gettable` → dosya/UNC okuma) beyaz-listeyle kapatıldı; kalan riskler (kiracı-kapsam=RLS, skaler CLR-UDF) belgelendi. **48/48 test** (28 bekçi vakası — başarı ölçütü #4) + LocalDB canlı. UI yok (S8/S9 çağırır). | ✅ |
| S6 | **Veri haritası** *(bağımlı: S5)* | **✅ (2026-08-06):** Harita CRUD + editör: şema keşfinden **yaz-bul** tablo ekleme (600+ tablo → sunucu-tarafı `sys.tables` LIKE + top-N), **tembel** kolon yükleme, **FK'dan otomatik** ilişki (join) önerisi, özelleştirilmiş (dostça) adlar, kolon **kullanımı** (kırılım/sayısal/anahtar/gizli — son kullanıcıya sade dille). `VeriHaritasi` aggregate (kök tenant-filtreli + cascade; ilişkiler yumuşak ref) + `VeriHaritasiEkle` migration + CQRS (kaydet transaction'da alt varlıkları tümüyle değiştirir). **Mod override çıkarıldı → bağlantıdan devralınır** (kullanıcı gereksiz buldu; K11 revize). Yalnız Admin+Geliştirici. ⚠ **Kaydetme concurrency hatası** (güncellemede istemci-Id'li alt varlıklar EF'te Modified sanılıp UPDATE→0 satır) bulundu+düzeltildi (açık `Add`) → LocalDB round-trip regresyon testi. **50/50 test.** *(Eski adı: veri modeli / semantik katman.)* | ✅ |
| S7 | **Sorgu modeli + SQL üretici** *(bağımlı: S6)* | **✅ (2026-08-06):** JSON sorgu modeli (AST) — kolon/ölçü seçimi, **filtre ağacı (AND/OR)**, GROUP BY + aggregate (SUM/AVG/MIN/MAX/COUNT/DISTINCT), sıralama, TOP limit; join'ler **haritanın ilişkilerinden BFS** ile türetilir. `MssqlSorguUretici` (Kds.Calistirma): nesne adları **QUOTENAME** (`]`→`]]`) parantezli, değerler **parametreli** (`@p0…`), yalnız haritadaki tablo/kolon. Üretilen SQL yine S5 SELECT-only bekçisinden geçer (tek kapı, testle kanıtlı). "SQL'i Göster" ucu (`POST /api/sorgu/sql`). **Adversarial code review:** enjeksiyon açığı yok; robustluk bulguları (aralık-dışı enum, fantom ara-tablo, LIKE joker kaçışı, GROUP BY dedup) düzeltildi + global 400/404 handler. **63/63 test.** *(v1 dışı: alt sorgu/UNION/window; self-join + aynı çift arası çoklu ilişki disambiguasyonu.)* | ✅ |
| S8 | **Sorgu tasarımcısı UI** *(bağımlı: S7)* | **✅ (2026-08-06):** Gerçek tasarımcı ekranı — Veri Haritası seçici + alan paneli (rol renkleri, tıkla-ekle) + Kolonlar/Ölçüler (kolon başına toplama) + **filtreler** (alan/op/değer, hepsi VE) + otomatik gruplama + sıralama + limit + "Kullanılan tablolar & ilişkiler" şeridi (join'ler haritadan). **"SQL'i Göster"** (S7 üretici) + **"Çalıştır"** (S5 çalıştırıcı) → Üretilen SQL / Sonuç grid sekmeleri. Backend: `POST /api/sorgu/calistir` (üret→çalıştır; `SorguYurutKomutu`'na parametre eklendi; JSON değerleri CLR'a çözülür). **Wire testi** polimorfik filtre + string enum deserialize'ı kilitledi (canlıya kaçacak `FiltreKosulu` çift-constructor deserialize hatasını yakalayıp düzeltti). **React Flow ilişki kanvası** (mockup'taki gibi): yalnız sorguya giren tablolar düğüm olur, join kenarları haritadan; **ilişkisiz tablo kırmızı "join yolu yok"** uyarısı alır ve istek atılmadan anlaşılır hata gösterilir. Soldan **sürükle-bırak** + tıkla-ekle. **64/64 test.** *(v1: filtre düz VE listesi; react-querybuilder/TanStack yerine sade MUI.)* | ✅ |
| S9 | **Ham SQL ekranı** *(bağımlı: S5)* | **✅ (2026-08-06):** `/sql` ekranı — **Monaco 0.56** SQL editörü (kök import; worker `editor.worker.start.js?worker`; sayfa **lazy-load** → Monaco kendi ~1.2MB chunk'ında, ana bundle şişmez) + bağlantı seçici + satır limiti + **Ctrl+Enter**; ortak `SonucTablosu`. Backend: `POST /api/sorgu/ham` → mevcut `SorguYurutKomutu` (S5 **tek kapı**: SELECT-only bekçi + satır sınırı + zaman aşımı + denetim) + `SorguYurutDogrulayici` + **`SorguGuvenlikException`→400** eşlemesi (bekçi reti insana okunur döner). Yalnız Admin+Geliştirici. **70/70 test.** | ✅ |
| S10 | **Rapor kaydetme + grafikler** *(bağımlı: S8)* | **✅ (2026-08-06):** Rapor = **JSON sorgu modeli + görselleştirme tercihi** (`Rapor`/`RaporKlasoru`/`RaporFavori` + `RaporEkle` migration; harita/klasör/sahip yumuşak ref, favori cascade). Tasarımcı 3. adıma **"Rapor Olarak Kaydet"** diyaloğu (ad + klasör[+yeni] + Özel/Ekip + 5 görselleştirme kutucuğu, otomatik öneri) ve `?rapor=id` ile **modeli tasarımcıya geri yükleme**. **Raporlar ekranı:** klasör rayı (Tümü/Favoriler/Ekiple paylaşılan/klasörler + sayaç) + kartlar + arama + yıldız. **Görüntüleyici** (`/raporlar/:id`): açılınca çalışır, **ECharts** (tree-shaken; tablo/çubuk/çizgi/pasta/sayı kartı) + oturumluk tür değiştirici + Yenile + Tasarımcıda Aç; **musteri rolüne de açık** (yalnız görünürler). Görünürlük: Özel=sahip+admin, Ekip=tenant (yeni `IKullaniciBaglami`). **Başarı ölçütü #1 kapandı** (kaydet→tekrar aç). **70/70 test.** | ✅ |
| S11 | **Dashboard** *(bağımlı: S10)* | **✅ (2026-08-09):** Pano = adlandırılmış **SIRALI rapor koleksiyonu** (`Dashboard` entity + `Panolar` tablosu + migration + CRUD; görünürlük rapordaki gibi Özel/Ekip) + `/panolar` galeri (oluştur/aç/düzenle/sil) + `/panolar/:id` görüntüleyici (raporlar canlı kart ızgarası) + `PanoEditor`; grafik mantığı ortak yardımcıya çıkarıldı (`reportChartOption`/`scoreCardValue`). **#282 gösterge paneli performansı** (FormData trend indeksi + kullanıcı-başı panel cache). E2E ile CRUD doğrulandı. **Sonraki dilimlerde tamamlandı:** react-grid-layout **widget yerleşimi/boyutu** (caeaa24) + **pano-düzeyi filtreler** (2026-08-10 — panonun raporlarının parametreleri anahtara göre birleşik filtre şeridinde toplanır, "Uygula" değerleri her karta taşır). Detay: karar günlüğü. | ✅ |
| S12 | **İçe alım modu** *(bağımlı: S6)* | **Faz 1 ✅ (2026-08-09) — backend dilimi:** `IngestJob` entity + `IceAlimIsleri` tablosu + `AddIngestJobs` migration; Contracts (`Kds.Contracts/Ingest`) + CRUD/Run handler'ları (`Kds.Business/Ingest`) + `IngestJobsController` (`api/ice-alim`, Admin+Geliştirici). **Çalıştırıcı** `SqlIngestRunner` (`Kds.Execution`, saf ADO.NET): depo DB'yi yoksa **otomatik yaratır**, her tablo için kaynak şemasından depo tablosunu **otomatik türetip** (introspection → CREATE) DROP+CREATE eder, **SqlBulkCopy** ile tam yükler (batch 10000, kolon adına göre eşleme). Tenant başına depo DB `KdsDepo_<tenant>` (`ConnectionStrings:KdsDepo` sunucusunda). **Elle "Şimdi Çalıştır"** ucu → tablo/satır/süre. **Canlı kanıt:** `KdsDemo.ortak.YagTipleri` (6 satır) → otomatik yaratılan `KdsDepo_proof` (şema `Id int`/`Ad nvarchar(40)` kaynaktan türedi, 6 satır SqlBulkCopy) sqlcmd ile doğrulandı. **126 test** (CRUD 2 yeni). **Faz 2 ✅ (2026-08-09) — cron zamanlama:** **Hangfire YERİNE hafif yol** (zamanlama durumu zaten işin satırında yaşıyor → ayrı job deposu gereksiz; ağır/lisanslı bağımlılıktan kaçınma çizgisi). Cron ayrıştırma **Cronos** (MIT, DST-doğru); `CronZamanlama` saf yardımcı (5/6 alan, vade hesabı taban=SonSenkron ?? oluşturma) + kaydet doğrulaması cron'u denetler. `IceAlimZamanlayici` **BackgroundService** dakikada bir TÜM tenant'ların aktif+cron'lu işlerini tarar, vadesi geleni ilgili tenant adına (`AmbientTenant` AsyncLocal → HttpTenantContext) **aynı `RunIngestJobRequest` handler'ıyla** çalıştırır (tek doğruluk kaynağı). **Canlı kanıt:** cron `* * * * *` işi tohumlandı → zamanlayıcı ~1 dk'da **kendiliğinden** tetikledi → `KdsDepo_<tenantA>` 6 satır, iş durumu `Basarili` (log + sqlcmd). **141 test** (+15 cron). **Faz 3 ✅ (2026-08-09) — rapor yönlendirme (başarı ölçütü #3):** sorgu yürütmenin TEK KAPISI `RunQueryHandler` mod-farkında oldu: bağlantı `DataMode.IceAlim` ise sorgu CANLI kaynağa değil tenant deposuna (`KdsDepo_<tenant>`) gider — üretilen SQL, SELECT-only bekçisi ve audit AYNI; yalnız bağlantı dizesi değişir. Depo adı/dizesi tek yerde (`IceAlimAyarlari.DepoDbAdi` + `SqlConnectionString.VeritabaniIle`; içe alım yükleme ile paylaşılır). Depo yapılandırılmamışsa net hata. **Canlı kanıt:** aynı sorgu aynı bağlantıyla Canlı→kaynak (6 satır), İçe Alım'a çevrilince depo sentinel'i (1 satır `DEPODAN`) — mod geçişi raporu bozmadı. **143 test** (+2 yönlendirme). **Faz 4 ✅ (2026-08-09) — İçe Alım İşleri ekranı:** `/ice-alim` (Gel+Admin) — iş listesi (durum çipi: Başarılı·satır·sn / Hata·tooltip / Hiç çalışmadı, zamanlama+aktif çipleri, son senkron) + **"Şimdi Çalıştır"** (elle tetik) + editör çekmecesi: kaynak (yalnız **İçe Alım modlu** bağlantılar) → **tablo seçici** (S6 şema uçları: yaz-ara → ekle → tablo başına kolon seçimi, tümü/seçili) → **cron preset**'leri (Elle/Saatlik/Günlük/Haftalık) + özel cron alanı → aktif. `api/ingest.ts` + menü zaten vardı (BlankPage→ekran). tsc/lint/build temiz. **E2E (Playwright):** İçe Alım bağlantı → iş oluştur (tablo seç) → **Şimdi Çalıştır (gerçek KdsDemo→KdsDepo senkronu, durum Başarılı)** → düzenle → sil; **tam süit 24/24**. **S12 TAMAM** (4 fazın dördü canlı kanıtlı; başarı ölçütü #3 kapalı). | ✅ |
| S13 | **Export + cila + yayın** *(bağımlı: S11, S12)* | **Part 1 ✅ (2026-08-09) — Export (Excel/CSV/PDF):** yeni `Kds.Export` projesi — `IResultExporter`/`ResultExporter`: **CSV** (UTF-8 BOM, kaçışlı), **Excel** (ClosedXML, sayı/tarih yerel tiple, başlık donuk), **PDF** (PDFsharp-MigraDoc — **MIT**, QuestPDF lisans eşiğinden kaçınıldı; cross-platform font çözücü `SistemFontCozucu` OS'ten TTF bulur → Docker'da da çalışır). Uçlar tek kapıdan geçer (audited): `GET /api/raporlar/{id}/disa-aktar?bicim=` + `POST /api/sorgu/ham/disa-aktar`. Frontend `apiDownload` (blob) + `ExportMenu` → **Rapor görüntüleyici** ve **SQL editörü**. CORS `Content-Disposition` exposed (dosya adı korunur). **Kanıt:** 3 birim test (CSV/XLSX/PDF imza) + canlı E2E indirme (sorgu.xlsx PK + sorgu.pdf %PDF). **146 test**, tam E2E 24/24. **Part 2 ✅ (2026-08-09) — Audit ekranı:** `/yonetim/denetim` (Admin) — kim/SQL/zaman/durum/satır/süre, filtre (Hepsi/Başarılı/Engellenmiş + SQL'de arama), sunucu-tarafı **sayfalama** (en yeni önce), satır→detay çekmecesi (tam SQL/parametreler/hata). `ListAuditRequest`/`AuditPage`/`ListAuditHandler` (kullanıcı/bağlantı adı ilişkili alt-sorguyla) + `AuditController` (`/api/denetim`). **`QueryAuditBehavior` düzeltmesi:** "kim" artık dolduruluyor (Keycloak sub → local User.Id; eskiden hep boştu). `AuditListTests` gerçek SQL Server'a karşı (LocalDB — SQLite DateTimeOffset ORDER BY yok). **Kanıt:** canlı — sorgu çalıştır → ekranda `admin · KdsDemo · 14ms · 1 satır · Başarılı`, detay tam SQL. **147 test**, E2E 24/24. **Part 3 ✅ (2026-08-10) — Performans (başarı ölçütü #2):** `PerformansTests` gerçek SQL Server'a (LocalDB) karşı geçici DB'ye **5.000.000 satır** + nonclustered columnstore tohumlar, `SqlQueryExecutor` ile ölçer, düşürür. **Canlı ölçüm:** ilk sayfa (filtre+sıralı+TOP 1000) **1402 ms**, agregasyon (GROUP BY, columnstore) **788 ms** — ikisi de **< 3 sn** ✓; TOP'suz `SELECT *` bile varsayılan sınırla **1000'de kesilir** (`SatirSiniriUygulandi`, runaway koruması) → "hiçbir sorgu sınırsız koşamaz". Test `[PerfFact]` ile **env-kapılı** (`KDS_PERF=1`; tohum ~65s, varsayılan süit hızlı kalır → 147 geçti/1 atlandı). **Part 4 🟡 (2026-08-10) — Yayın (tek-VM Compose) + read-only (ölçüt #4-2):** **Yazıldı + statik doğrulandı; canlı container koşusu Docker tamamlanınca.** `infra/deploy/docker-compose.yml` (MSSQL+Keycloak realm-import+Mongo+API+önyüz) + API `Dockerfile` (net10 çok-aşamalı + PDF için DejaVu font) + önyüz `Dockerfile`+`nginx.conf` (SPA) + `.env.example` + `README`. Program.cs container-dostu: token issuer PUBLIC URL iken Keycloak metadata İÇ ağdan (`Kimlik:MetadataAdresi`), yayında migration otomatik (`MigrationUygula`). **Ölçüt #4 ikinci katman:** `readonly-user.sql` (kaynak DB'de `db_datareader`) + `ReadOnlyKullaniciTests` (LocalDB EXECUTE AS — okur ama INSERT/UPDATE/DELETE/DROP reddedilir, veri bozulmaz). Statik: `dotnet publish` ✓, önyüz build ✓, YAML temiz. **149 test** (148+1 atlanan). **Docker bu makinede:** winget kurulumu indirdi ama admin/UAC gerektiren backend adımı headless tamamlanamadı → engine ayağa kalkmadı; kullanıcı Docker'ı bitirince `docker compose up` canlı doğrulanacak. **Başarı ölçütlerinin DÖRDÜ de karşılandı** (#1 S8/10/11 · #2 5M perf · #3 S12 · #4 bekçi+read-only).<br>**Part 4 CANLI ✅ (17 Ağu 2026):** Docker ayağa kalkınca yığın belgelenen portlarda uçtan uca koşturuldu (geliştirme yığını geçici durduruldu, sonra geri getirildi). `docker compose up -d --build` → **5 servis de Up** (mssql healthy). Doğrulananlar: **API `/api/durum` 200** · **`/api/durum/kimlik` token'sız 401** (kimlik hattı bağlı) · **Keycloak `kds` realm + OIDC discovery 200** · **önyüz nginx 200 ve derin yolda SPA fallback 200** · **EF migration konteynerde otomatik uygulandı** (KdsMetadata'da 16 tablo, son migration `AddSistemAyarlari`) · veri dizini kalıcı birimde. Ardından `docker compose down` ile temizlendi; `.env` git'e girmez (şablon `.env.example`). | ✅ |
| S14 | **Olay günlüğü (KdsLog Mongo)** *(çapraz-kesen)* | **✅ (2026-08-09) — üretici tarafı:** KDS'e ÖZEL Mongo DB (`KdsLog`, QMS'ten ayrı; `mongodb://localhost:27017`), **3 koleksiyon**: `Action` / `Exception` / `DataChange`. Exception+DataChange, ait oldukları isteğe **`ActionId` ile bağlanır** (korelasyon; `Action._id`=ActionId). Yazım: `IEventLog` → dayanıklı arka plan kanalı + hosted service (istek bloklanmaz, Mongo düşse istek bozulmaz, bağlantı yoksa no-op). Kaynaklar: **DataChange** = EF `SaveChangesInterceptor` (eski→yeni, şifre maskeli), **Action** = `ActionLogMiddleware` (istek özeti+süre), **Exception** = `ExceptionHandler`. `AppId=lst-kds` (çok-uygulama ayrımı). MongoDB.Driver 3.10 (Guid=Standard). Uçtan-uca kanıt: bağlantı ekle → `KdsLog.DataChange` (ActionId→Action). **Tüketici (aktivite/denetim ekranı) ayrı/gelecek uygulama.** 124 test. | ✅ |
| S15 | **Zamanlanmış Raporlar (rapor aboneliği)** *(bağımlı: S10, S13)* | **✅ (2026-08-10):** Abonelik = bir raporu periyodik (cron) ya da elle çalıştırıp sonucu **PDF/Excel/CSV** ekiyle **e-postayla** teslim. `ReportSubscription` entity + `RaporAbonelikleri` migration + CRUD/Run handler'ları (`Kds.Business/Subscriptions`) + `SubscriptionsController` (`api/abonelikler`, Admin+Gel). **Zamanlayıcı** `AbonelikZamanlayici` (İçe Alım'ın kardeşi: `BackgroundService`+**Cronos**, dakikada bir tüm tenant'ların vadesi gelen aktif aboneliklerini `AmbientTenant` ile kendi adına gönderir; durum abonelik satırında — ayrı job deposu yok). Çalıştırma boru hattı: raporu **görünürlük-kapısız** koşar (aynı bekçi+sınır+denetim zinciri) → `IResultExporter` (S13) ile biçime çevirir → `IEpostaGonderici`'ye verir. **E-posta:** SMTP yapılandırılmışsa (`Eposta:*`) `SmtpEpostaGonderici` (System.Net.Mail — yeni paket yok), değilse (dev/test) `GunlukEpostaGonderici` (günlüğe yazar; boru hattı SMTP'siz de uçtan uca çalışır — "yarım özellik yok"). Sol menü placeholder → **gerçek ekran** (`/zamanlanmis-raporlar`: kart listesi + durum çipi + "Şimdi Gönder" + editör çekmecesi: rapor/biçim/alıcı-chip/cron preset). **Kanıt:** 3 birim test (CRUD + **run pipeline**: rapor→PDF→e-posta yakalama→durum) + **E2E `abonelik.spec`** canlıya karşı (rapor oluştur→abone ol→Şimdi Gönder→200, dev e-posta günlüğünde `E2E Abonelik.pdf` 59 KB). **Backend 157 test** (+3), **tam E2E 27/27**. *(v1 kapsam: rapor aboneliği. **Pano aboneliği: 18 Ağu 2026 kullanıcı kararıyla KAPSAM DIŞI** — "gerekirse ayrı ayrı mail atılır"; borç sayılmaz. Kalan tek parça: **SMTP canlı doğrulaması**.)* | ✅ |
| S16 | **Kurulum sihirbazı + Sistem Ayarları (ürünleşme)** *(kullanıcı kararı 14 Ağu 2026: "bağımsız olsun, kodda connection string olmasın, her yerde çalışsın — bu bir ürün")* | **Tasarım ✅ → [07-kurulum-ve-sistem-ayarlari.md](07-kurulum-ve-sistem-ayarlari.md).** Özet: yapılandırma kaynak sırası (env → şifreli `kurulum.json` → appsettings geçiş → **kurulum modu**); bağlantı artık açılışta closure'a hapsedilmiyor, `IMetadataBaglantiSaglayici` ile **çalışma zamanında** çözülüyor (kurulum sonrası restart yok); `/api/kurulum/*` + tek kullanımlık **kurulum anahtarı**; önyüzde `/kurulum` sihirbazı (DB → kimlik → ilk kiracı/admin → kur); `SistemAyarlari` tablosu + Yönetim → Sistem Ayarları ekranı (depo · SMTP · sorgu zaman aşımı/limitleri · M33 tanı eşikleri · Mongo); sırlar Data Protection ile şifreli, anahtarlar kalıcı dizinde. **Aşamalar:** A1 önyükleme çekirdeği → A2 sihirbaz → A3 ayar ekranı → A4 kılavuz+E2E. Mevcut dev/Compose kurulumları göç yoluyla korunur.<br>**A1 ✅ (14 Ağu 2026):** `VeriDizini` (env `KDS_VERI_DIZINI` → `%ProgramData%\LST\KDS` / `/var/lib/kds`), `KurulumDeposu` (kaynak sırası + **şifreli** `kurulum.json`; kurulum halkası uygulama halkasından AYRI — parolalar bozulmasın), `KurulumDurumu` (= `IMetadataBaglantiSaglayici`), `AddKdsData()` artık **dizesiz** (bağlantı her scope'ta çözülür → kurulum sonrası restart yok), `KurulumGerekliException`→**503**, Program.cs kurulum-farkında (kimlik/CORS/migration kurulum modunda güvenli davranır), `KdsDbContextFactory`'deki **sabit LocalDB dizesi kaldırıldı** (artık `KDS_METADATA_BAGLANTI` env'i; yoksa açık hata). **Kanıt:** (a) mevcut appsettings ile API normal çalışıyor + "eski yapılandırma" uyarısı loglanıyor; (b) **hiçbir yapılandırma olmadan** API çöküyor değil — `KDS KURULUM MODUNDA` logu + dinlemeye devam + veri dizini/anahtarlar oluşuyor; (c) **176 test yeşil**.<br>**A2 ✅ (14 Ağu 2026) — kurulum uçları + sihirbaz:** `KurulumAnahtari` (açılışta üretilir, loga + `kurulum-anahtari.txt`'ye yazılır; sabit-zamanlı doğrulama, 5 hatada kilit, kurulum bitince dosya silinir), `KurulumHizmeti` (DB testi: sürüm + **DDL yetkisi** + "veritabanı yok → oluşturulabilir mi"; kimlik testi: OpenID keşif belgesi; tamamla: gerekirse **CREATE DATABASE** → `Migrate()` → ilk kiracı → şifreli yapılandırma yazımı — sıra bilinçli: hata hâlinde uygulama kurulum modunda kalır), `KurulumController` (`/api/kurulum`, anonim + anahtar; kurulu sistemde **410 Gone**). Önyüz: `KurulumKapisi` (açılışta durum sorar; kurulmamışsa kimlik sağlayıcıya HİÇ gitmeden sihirbazı açar) + `KurulumPage` (5 adım: anahtar → veritabanı(test) → kimlik(test) → kiracı/adresler → kur; sonunda üretilen **kiracı kimliği** Keycloak `tenant_id` için gösterilir). **Kanıt (canlı):** durum ucu anonim çalıştı; anahtarsız/yanlış anahtar **401**; var olmayan `KdsKurulumTest` için "oluşturulacak" + gerçek sürüm bilgisi; `tamamla` → **15 tablo + kiracı kaydı** (SQL ile doğrulandı) + şifreli `kurulum.json` (düz metin değil) + anahtar dosyası silindi + uçlar **410**; önyüz taze kurulum-modu API'sine bağlanınca **sihirbaz açıldı**, 1→2→3. adımlar tarayıcıda sürüldü (konsol hatası yok, ekran görüntüsü alındı). Test DB'si ve geçici betikler temizlendi.<br>**A3 (1/2) ✅ (14 Ağu 2026) — Sistem Ayarları altyapısı + Sorgu/Tanı ayarları:** `SystemSetting` entity + `SistemAyarlari` tablosu + `AddSistemAyarlari` migration (kiracı-özel ezme için `TenantId` null'lanabilir; bilerek tenant filtresi YOK — genel satır herkese görünür), `AyarTanimlari` (anahtar/tip/varsayılan/açıklama tek kaynak — ekran buradan çizilir), `ISistemAyarlari` (öncelik: **kiracı → kurulum geneli → koddaki varsayılan**, 30 sn bellek önbelleği, yazımda düşer), `ListSettings`/`SaveSettings` handler'ları (varsayılana eşitlenen ayarın SATIRI SİLİNİR; tip doğrulaması), `SettingsController` (`/api/ayarlar` Admin; `/api/ayarlar/onyuz` dar yüzey — yalnız arayüz eşiği). **Koddan çıkan sabitler:** sorgu zaman aşımı 60 sn · varsayılan satır 1000 · sert tavan 10000 (`RunQueryHandler`), tanı plan zaman aşımı 30 sn + tarama 500K + sıralama 100K (`SqlQueryDiagnostician`/`QueryPlanRules` artık eşiği parametre alır), önyüzdeki "yavaş sorgu" 10 sn (`useYavasEsik` → `/api/ayarlar/onyuz`). Ekran: **Yönetim → Sistem Ayarları** (gruplu, her satırda kaynak rozeti *kiracı/kurulum/varsayılan* + varsayılana döndür). **Kanıt:** migration dev DB'ye uygulandı (`SistemAyarlari` tablosu var), **180 test yeşil** (4 yeni: varsayılan/kiracı ezme/genel/silme+doğrulama), backend+önyüz derlemesi temiz. **A3 (2/2) ✅ (14 Ağu 2026) — depo + e-posta + sırlar:** Ayarlara **Depo** (sunucu bağlantısı *sır* + `KdsDepo_{kiraci}` adı deseni) ve **E-posta** (SMTP sunucu/port/SSL/kullanıcı/parola *sır*/gönderen) grupları eklendi. **Sır yönetimi:** sır tipli ayarlar Data Protection ile **şifreli** saklanır, `/api/ayarlar` yanıtında değer **hiç dönmez** (yalnız `dolu` bayrağı; ekranda parola alanı + "girili" ipucu), okuma katmanı çözer (anahtar bozuksa sızdırmadan boş döner). **Depo çözümü tek yerde** (`DepoCozucu`): öncelik **Sistem Ayarları → kurulum (env/kurulum.json)**; hem içe alım yazımı (`RunIngestJobHandler`) hem İçe Alım modlu okuma (`QueryConnectionResolver`) aynı kaynağı kullanır (yazılan yerden okunur ilkesi korundu), DB adı desenden üretilir. **E-posta:** `EpostaAyarlari`+`SmtpEpostaGonderici`+`GunlukEpostaGonderici` üçlüsü tek `EpostaGonderici` ile değiştirildi — SMTP bilgisi DI zamanında değil **her gönderimde** ayarlardan okunur (ekrandan değişince yeniden başlatma yok); sunucu/gönderen boşsa göndermez, günlüğe yazar (eski dev davranışı korundu; `kds@localhost` varsayılanı kalktı). **Mongo (olay günlüğü)** bilinçli olarak *kurulum* düzeyinde bırakıldı (metadata DB gibi altyapı bağlantısı) ve kurulum sihirbazına alanı eklendi. **Kanıt:** **182 test yeşil** (+3: sır şifreleme/maskeleme/çözme, depo ayarının kurulumu ezmesi + desen, mevcut yönlendirme testleri yeni bağımlılıkla), backend+önyüz derlemesi temiz, dev API sağlıklı (log temiz).<br>**A4 ✅ (14 Ağu 2026) — kılavuz + sıfırdan kurulum testi:** [infra/KURULUM.md](../infra/KURULUM.md) (kaynak sırası + ortam değişkeni tablosu · veri dizini · sihirbaz adımları + gereken DB yetkileri · Sistem Ayarları · **yedekleme: anahtar dizini kaybı = kurulum dosyası okunamaz** + bilinen açık madde *bağlantı parolaları hâlâ varsayılan DP halkasında* · yükseltme · `dotnet ef` için `KDS_METADATA_BAGLANTI`), infra/README'den bağlandı. **Compose ürün yoluna çevrildi:** `ConnectionStrings__*`/`Kimlik__*` → `KDS_*` ortam değişkenleri + veri dizini için **kalıcı birim** (`kds-veri`) — eski değişkenler geriye uyumlu çalışmaya devam eder. **Sıfırdan kurulum entegrasyon testi** (`KurulumHizmetiTests`, gerçek LocalDB): var olmayan DB → "oluşturulacak" → tamamla → **DB yaratıldı + şema kuruldu + kiracı eklendi + durum yapılandırıldı (restart yok) + `kurulum.json` şifreli (DB adı düz metin GEÇMİYOR) + dosyadan yeniden okuma aynı bağlantıyı veriyor**; test kendi DB'sini ve geçici veri dizinini siler. **183 test yeşil.** Playwright yerine entegrasyon testi seçildi (kurulum akışı ikinci bir API örneği + ayrı veri dizini ister; sihirbaz arayüzü A2'de tarayıcıda elle doğrulandı). ⚠ Compose değişikliği **statik** doğrulandı — Docker bu makinede kurulu değil (S13 ile aynı durum). | ✅ |
| S17 | **Tasarımcı yetenek genişlemesi** *(kullanıcı kararı 17 Ağu 2026: "bu raporları tasarlayabilmemiz lazım, çok özellikli olmamız lazım")* | **Analiz ✅ → [08-tasarimci-yetenek-acigi.md](08-tasarimci-yetenek-acigi.md).** 15 örnek raporun tasarımcıyla kurulabilirliği tek tek çıkarıldı: **6 tam · 1 nüanslı · 8 kurulamıyor**. Eksikler maddelendi: **Y1** hesaplanan (ifade) kolon *(6 rapor)* · **Y2** ifadeye göre filtre/sıralama *(3)* · **Y3** HAVING *(1 — tek başına Rapor 11'i açar)* · **Y4** tarih kovalama yıl/ay *(2)* · **Y5** dinamik tarih "bugün−N" *(4)* · **Y6** pencere fonksiyonları RANK/LAG/OVER *(3)* · **Y7** grup içi TOP-N *(1)* · **Y8** ara küme / FROM'da alt sorgu *(1)* · **Y9** PIVOT *(1)* · **Y10** GROUPING SETS/ara toplam *(1)* · **Y11** EXISTS görsel karşılığı *(1 — bugün yalnız elle SQL kutusundan)*. Ayrıca bu raporlarda görünmeyen bilinen sınırlar: DISTINCT · LEFT join ON'una sabit koşul · self-join · UNION · OFFSET. **Önerilen faz sırası:** F1 (Y3+Y5) → F2 (Y1+Y2) → F3 (Y4→Y6→Y7) → F4 (Y8·Y9·Y10·Y11); F1 sonunda 7, F2'de 9, F3'te 12, F4'te 15 rapor kurulabilir olur. **Uygulama başladı — madde madde, her maddede ekrandaki adlandırma da açıklayıcı yazılıyor (kullanıcı isteği 17 Ağu 2026).**<br>**Y3 ✅ (17 Ağu 2026) — HAVING:** sözleşmeye `SummaryCondition` + `QueryModel.OzetKosullar` (toplama · tablo.kolon · op · değerler · bağlaç); üreticide doğrulama (toplamasız koşul reddedilir: "satır bazlı koşullar filtreye yazılır") + `GROUP BY`'dan sonra `HAVING` bloğu. Koşul SQL'i **mevcut `ConditionSql` ile paylaşılır** (op/değer/parametre makinesi tek yerde): sol taraf `AGG(kolon)` olarak sarmalanır; Say/BenzersizSay'da karşılaştırma değeri kolonun tipine değil **sayıya** çevrilir (`COUNT(DISTINCT metinKolon) >= 3` bozulmasın). Bağlaçlar filtre satırlarıyla aynı kuralla (soldan sağa VE/VEYA) birleşir. Önyüz: **Kolonlar & Ölçüler** adımında (yalnız özet varken görünen) **"Özet Filtresi (gruplama sonrası)"** bölümü + **"Özet filtresi ekle (HAVING)"** butonu; başlık açıklaması farkı anlatır. Rapor/oturum kaydı ve geri yüklemesi bağlı. **Kanıt:** 3 yeni altın-örnek test (HAVING üretimi + WHERE'den ayrı kalması · benzersiz sayımda değer çevrimi + VEYA bağlacı · toplamasız koşulun reddi) + 2 model testi (ayrı liste olarak gitmesi · boşsa null) · **188 backend + 29 önyüz testi yeşil**.<br>**Y5 ✅ (17 Ağu 2026) — kayan (dinamik) tarih:** kullanıcı isteğiyle **genel ihtiyaç olarak** tasarlandı (örnek raporlara göre değil): `DinamikTarih` dilbilgisi `$taban[±sayıbirim]` — taban `bugun · haftaBasi · ayBasi · yilBasi`, birim `gun · hafta · ay · yil` (ör. `$bugun-6ay`, `$ayBasi`, `$yilBasi-1yil`, `$bugun-30gun`); hafta başı Pazartesi. Çözüm **tek noktada** (`CozParametre`) yapıldığı için hem doğrudan filtre değerlerinde, hem parametre varsayılan/çalıştırma değerlerinde, hem özet/koşullu ölçü/CASE/HAVING filtrelerinde geçerli. Tanınmayan `$…` metni **aynen kalır** ("$100" bozulmaz). Eski `$bugun` özel-durumu bu genel çözücüye devredildi (M25 davranışı korunur). Önyüz: filtre satırında **"Değer kaynağı"** seçicisine **"Kayan tarih — her çalıştırmada yeniden hesaplanır"** başlıklı grup (13 hazır seçenek); seçilince değer ham belirteç yerine 🗓 etiketiyle gösterilir ("Bugün − 6 ay") ve "sabit değere dön" düğmesi çıkar; **"Arasında"da bitiş ucu** da kayan olabilir ("son 1 yıl" = `$bugun-1yil … $bugun`). **Parametre panelinde** (görüntüleyici/pano/abonelik ortak) Tarih ve TarihAralık alanlarına 🗓 kayan seçici eklendi — M25'in yalnız "bitiş = Bugün" onay kutusu genelleşti. **Kanıt:** 20 birim test (12 dilbilgisi biçimi · tanınmayan değerler · pazar günü hafta başı) + 2 altın-örnek test (filtre değerinde çözülme · "$100" bozulmuyor) · **210 backend + 40 önyüz testi yeşil**.<br>**Y1 ✅ (17 Ağu 2026) — hesaplanan (formül) kolon:** `ComputedColumn` + `QueryModel.HesaplananAlanlar`. Formül dili, elle SQL koşulunun dilidir: kolonlar `{Tablo.Kolon}` yer tutucusuyla yazılır, gerisi hesaptır (`{A.Tutar} - {B.Tutar}` · `SUM({A.X}) * 100.0 / NULLIF(COUNT({A.Id}), 0)`). **Güvenlik + erken hata:** yeni saf `IfadeDenetimi` — `;`, yorum, `SELECT/INSERT/DROP/UNION/EXEC…` reddedilir; kelimeler **beyaz listeye** göre denetlenir (toplama · sayı · metin · tarih fonksiyonları + CAST tip adları), parantez dengesi kontrol edilir, tek-tırnaklı metin sabitleri denetim dışıdır (veri, kod değil). Bekçi son SQL'i zaten denetliyor; buradaki amaç anlaşılır hata + dar yüzey. **Grup/satır ayrımı otomatik:** formül toplama içeriyorsa (SUM/COUNT/AVG/MIN/MAX) sorgu aggregate sayılır ve ifade **GROUP BY'a girmez**; içermiyorsa satır düzeyi boyuttur ve bileşik/CASE kolonlarıyla aynı kuralla GROUP BY'a eklenir. Önyüz: **Kolonlar & Ölçüler** adımında **"Hesaplanan Kolon (formül)"** bölümü + **"Formül ekle"** butonu; diyalogda kolon ekleyici (yer tutucuyu metne basar), `+ − * / ( )` düğmeleri, **6 hazır kalıp** (fark · sıfıra bölünmeye korumalı yüzde · boşsa 0 · yuvarlama · mutlak değer · iki tarih arası gün) ve grup/satır kuralının açıklaması. Rapor/oturum kaydı bağlı. **Kanıt:** 8 altın-örnek test (satır düzeyi GROUP BY'a giriyor · toplamalı ifade GİRMİYOR · 5 geçersiz ifade reddi · metin sabiti serbest) · **218 backend + 40 önyüz testi yeşil** · üretilen biçim gerçek KdsDemo'da çalıştırıldı (marka bazında oran kolonu, 5 satır).<br>**Y2 ✅ (17 Ağu 2026) — formül koşulu + çıkış kolonuna göre sıralama:** kullanıcı isteğiyle **genel ihtiyaç** olarak kurgulandı: "hesabın sonucuna göre ele" ve "sonuçtaki HER kolona göre sırala". Üç parça: **(1) çıkış kolonu atfı** — SELECT'e giren her kolon (alan · koşullu ölçü · CASE · birleşik · bağlı tablo özeti · hesaplanan) `cikisIfadeleri` haritasına yazılır; formüllerde `[Ad]` ile atıf yapılır ve **ifadenin kendisi metin olarak yerine konur** (SQL, WHERE/HAVING'de alias kabul etmez). Atıf toplama taşıyorsa (`[Ciro]` = `SUM(...)`) bu **miras alınır**. Bilinmeyen ad, kullanılabilir kolonları sayan anlaşılır hata verir. Hesaplanan kolon bloğu bu yüzden **en sona** alındı (her çıkışa atıf yapabilsin) ve `aggVar` artık çözülmüş ifadeden hesaplanıyor. **(2) `ExpressionCondition` + `QueryModel.IfadeKosullari`** — sol taraf serbest hesap ya da `[Çıkış]` atfı, sağ taraf her filtre gibi op + değer(ler) (parametre ve kayan tarih dahil, `ConditionSql` paylaşımı). **Nereye gideceğini kullanıcı değil üretici seçer:** ifade toplama içeriyorsa `HAVING`, içermiyorsa `WHERE`. İfade `IfadeDenetimi`'nden geçer (Y1'le aynı beyaz liste). **(3) `SortItem.CikisAdi`** — ORDER BY artık yalnız "seçili alan" değil **herhangi bir çıkış kolonu** olabilir (ORDER BY SELECT'ten sonra çalıştığı için alias'a atıf geçerlidir). Önyüz: **Kolonlar & Ölçüler** adımında **"Formül Filtresi (hesabın sonucuna göre ele)"** bölümü — açıklaması "tek kolona değil bir HESABA göre eler… SUM/COUNT içerirse gruplama sonrası (HAVING), içermezse satır bazında (WHERE) uygulanır — seçimi sistem yapar" —, satırda **"＋ kolon"** (`{Tablo.Kolon}` basar) ve **"＋ çıkış kolonu"** (`[Ad]` basar) ekleyicileri, VE/VEYA bağlacı, parametre bağlama; buton **"Formül filtresi ekle"**, boşken ipucu "Sıradan filtre 'kolon = değer' içindir; burası 'hesabın sonucu şu olsun' içindir". **Sıralama** bölümünün başlığına açıklama eklendi ve seçicisine **"Hesaplanan / özet kolonlar"** grubu geldi (adı olmayan kolonlar için "…sıralayabilmek için o kolona bir ad verin" notu). Rapor/oturum kaydı ve geri yüklemesi bağlı. **Kanıt:** 8 yeni altın-örnek test (toplamasız → WHERE · toplamalı → HAVING · `[Ciro]` atfı toplamayı miras alıyor · bilinmeyen ad hatası · hesap kolonu başka çıkışlara atıf + GROUP BY'a girmiyor · çıkış adıyla sıralama · olmayan ada sıralama reddi · geçersiz formül reddi) + 5 model testi · **226 backend + 45 önyüz testi yeşil** · üretilen biçim (WHERE'de formül + HAVING'de atıf + ORDER BY alias) gerçek KdsDemo'da çalıştırıldı.<br>**Y4 ✅ (17 Ağu 2026) — tarih kırılımı (kova):** genel ihtiyaç olarak alanın bir ÖZELLİĞİ yapıldı (ayrı kolon üretmek yerine): `TarihKovasi` + `SelectedField.Kova` · `GroupingItem.Kova` · `SortItem.Kova`. İki aile: **dönem başlangıcı** (Yıl · Çeyrek · Ay · Hafta · Gün — o dönemin ilk günü, kronolojik sıralanır, grafikte eksen olur) ve **dönem parçası** (Yıl no · Çeyrek no · Ay no · Saat — yıldan bağımsız sayı; mevsimsellik/saatlik desen). SELECT ile GROUP BY **tek yerden** (`AlanIfade`) üretilir ki ifadeler birebir aynı olsun; elle GROUP BY listesi de kovayı taşır. Aynı kolonun iki kovası AYRI çıkış kolonudur (sıralama eşleşmesine de kova girer). **Kova = boyut:** üzerine toplama uygulanması ve tarih olmayan kolonda kullanılması anlaşılır mesajla reddedilir; varsayılan çıkış adı kırılımı taşır (`Tarih_Ay`). **Hafta bilerek farklı üretilir:** yaygın `DATEADD(week, DATEDIFF(week, 0, k), 0)` deseni gerçek SQL Server'da PAZAR gününü bir SONRAKİ pazartesiye atıyordu (2026-08-16 Pazar → 2026-08-17; ölçüldü) — bu Y5'teki `$haftaBasi` (pazartesi) kuralıyla çelişirdi; yerine gün-modülo deseni (`DATEDIFF(day, 0, k) % 7`) kullanıldı: DATEFIRST'ten bağımsız, hafta gerçekten pazartesi başlıyor (KdsDemo'da tüm kovalar Monday döndü). Önyüz: **Kolonlar & Ölçüler** satırında yalnız TARİH kolonlarında çıkan **"kırılım yok / ⤵ …"** seçicisi — grupları **"Dönem başlangıcı — kronolojik sıralanır"** ve **"Dönem parçası — mevsimsellik (yıldan bağımsız)"**, ipucu metni *"Tarihi yıla/aya/haftaya indirger — 'aylık kırılım' için ayrı kolon üretmeye gerek kalmaz"*; kırılım seçilince toplama seçicisi kapanır (*"tarih kırılımı bir gruplama kolonudur"*), Gruplama çipinde kırılım ⤵ etiketiyle görünür. **Kanıt:** 13 yeni test (9 kova biçimi SELECT+GROUP BY eşliği · elle gruplamada kova · aynı kolonun iki kovası + ayrı sıralama · tarih olmayan kolon reddi · toplamayla birlikte reddi) + 1 model testi · **239 backend + 46 önyüz testi yeşil** · aylık/haftalık/mevsimsel kırılım gerçek KdsDemo'da çalıştırıldı.<br>**Y6 ✅ (17 Ağu 2026) — pencere kolonları (OVER):** `WindowColumn` + `WindowSort` + `PencereIslevi` + `QueryModel.PencereKolonlari`. Genel çerçeve: bir pencere kolonu = **işlev + (gerekiyorsa) değer + bölüm (PARTITION BY) + sıra (ORDER BY) + sayı**. Değer/bölüm/sıra alanlarının hepsi **Y1-Y2 formül dilini** kullanır (`{Tablo.Kolon}` ya da `[Çıkış Kolonu]`) — böylece gruplanmış sorguda `[Ciro]` demek `SUM(...)`'ı OVER içine taşır ve ayrı bir "aggregate mi değil mi" makinesi gerekmez. **9 işlev:** sıra no (ROW_NUMBER) · sıralama-atlar (RANK) · sıralama-atlamaz (DENSE_RANK) · dilim (NTILE) · önceki dönem (LAG) · sonraki dönem (LEAD) · koşan toplam (ROWS UNBOUNDED PRECEDING) · **bölüm toplamı** (paya oran için; **bilerek SIRASIZ** — sıra eklenirse SQL Server onu kümülatife çevirir ve "pay %" sessizce yanlış çıkardı) · hareketli ortalama (ROWS BETWEEN n-1 PRECEDING AND CURRENT ROW). **Üretim sırası bilinçli:** pencere kolonları özetlerden SONRA, hesaplanan (Y1) kolonlardan ÖNCE üretilir — yani hesap bir pencere sonucuna atıf yapabilir (`[Ciro] * 100.0 / NULLIF([GenelToplam], 0)` = paya oran), tersi SQL'de döngü olurdu. Pencere ifadeleri **asla GROUP BY'a girmez** (gruplamadan sonra çalışırlar). Kurallar anlaşılır mesajla: sıra gerektiren işlevde sıra zorunlu ("neye göre?"), değer gerektiren işlevde değer zorunlu, sayılar pozitif; NTILE/LAG/LEAD sayıları int doğrulandığı için metne gömülür (enjeksiyon yüzeyi yok). Önyüz: **Kolonlar & Ölçüler** adımında **"Pencere Kolonu (satırı komşularıyla karşılaştır)"** bölümü + **"Pencere kolonu ekle"** butonu; diyalogda işlev listesi **her seçenek ne yaptığını anlatan cümleyle** ("Sıraya göre N satır ÖNCEKİ değeri getirir — 'geçen ayki ciro' gibi"), **"Hangi değer"**, **"Her … içinde ayrı hesapla (isteğe bağlı)"**, **"Neye göre sıralı? (zorunlu)"** alanları — her biri **"＋ kolon" / "＋ çıkış kolonu"** ekleyicileriyle —, işleve göre değişen sayı alanı ("Kaç satır geri", "Dilim sayısı", "Kaç satırlık pencere") ve altta bölüm toplamı/pay % ipuçları. **Kanıt:** 6 yeni altın-örnek test (bölümlü RANK · LAG + koşan toplam · bölüm toplamının sırasız kalması + hesabın ona atfı · hareketli ortalama & NTILE · sırasız işlev reddi · değersiz işlev reddi) + 2 model testi · **245 backend + 48 önyüz testi yeşil** · aylık ciro + geçen ay + kümülatif + genel toplam + pay % ve marka sırası/çeyrekliği gerçek KdsDemo'da çalıştırıldı (LAG değerleri bir önceki satırın cirosuyla birebir eşleşti).<br>**Y7 ✅ (17 Ağu 2026) — grup içi ilk N:** `TopNPerGroup` + `QueryModel.GrupIciIlkN` (sıra · adet · bölüm · eşitleri de al). Satır limiti sonucun TAMAMINI kırpar; bu HER BÖLÜMDEN N satır bırakır ("her markada en çok satan 3 model"). Sıra numarası bir pencere işlevidir ve WHERE'de kullanılamaz → sorgu **sarmalayıcı alt sorguya** alınır, kırpma dış katmanda yapılır; **TOP ve ORDER BY da dışarı taşınır** (iç katmanda TOP kırpmadan ÖNCE keserdi = yanlış sonuç; alt sorguda TOP'suz ORDER BY zaten geçersiz). Yardımcı sıra kolonu dış SELECT'te YOKTUR (kullanıcının sonucunu kirletmez). Beraberlik seçeneği: ROW_NUMBER (tam N) ya da RANK (eşitlerin hepsi). Önyüz: **"Her Grupta İlk N (grup içi kırpma)"** bölümü — açıklaması satır limitinden farkını söyler —, anahtar + "Her bölümden [N] satır kalsın" + **"Bölüm — 'her … için'"** + **"Neye göre 'ilk'? (zorunlu)"** + beraberlik anahtarı. **Kanıt:** 5 test + 1 model testi; "her markada en çok ciro yapan 2 model" gerçek KdsDemo'da (her markadan tam 2 satır).<br>**Y8 ✅ (17 Ağu 2026) — ara küme (ikinci kat):** iki ihtiyacı tek mekanizmayla karşılar. **(a) Pencere kolonuna göre filtre:** SQL'de pencere işlevi ne WHERE'de ne HAVING'de kullanılabilir — "[Ciro] > [OrtalamaCiro]" gibi bir formül koşulu artık **otomatik** olarak ara kümeye taşınır ve dış katmanda çıkış ADLARIYLA uygulanır (kullanıcı bu ayrımı bilmez; eskiden geçersiz SQL üretilirdi). **(b) "Özetin özeti":** `AraKumeGruplari` + `OuterAggregate` — "her müşterinin toplamını bul, sonra bunların ORTALAMASINI al" tek katta yazılamaz (toplama içinde toplama yasak); iç kat kırılımı üretir, dış kat onun üstünde yeniden gruplar. Dış katmanda tablo kolonuna atıf ({T.K}) **açık mesajla** reddedilir (iç tablolar dışarı sızmaz), ara kümenin dışında kalan kolonla sıralama da öyle. Y7 ile aynı sarmalayıcıyı paylaşır. Önyüz: **"Ara Küme (sonucun üstünde ikinci kat)"** bölümü — anahtar, **"İkinci katın kırılımı (boşsa tek satır döner)"** çıkış-kolonu çipleri ve **"İkinci katın özetleri"** (toplama + üstteki kolon + ad); ad verilmemişse uyarı. **Kanıt:** 4 test + 2 model testi; marka başına model toplamlarının ortalaması gerçek KdsDemo'da.<br>**Y9 ✅ (17 Ağu 2026) — çapraz tablo (PIVOT):** `PivotSpec` — başlık kolonu · değer kolonu · sütuna dönecek DEĞERLER · hücre toplaması. Çapraza girmeyen kolonlar satır eksenidir (PIVOT'un klasik tuzağı: sonuca fazladan kolon girerse satırlar bölünür — bu ekranda açıkça yazıldı). **Sütun listesi bilerek SABİT:** dinamik liste çalışma anında SQL üretmek demektir, bu üründe yapılmaz; bunun yerine **"Başlıkları veriden getir"** düğmesi sorguyu çaprazsız bir kez çalıştırıp o kolondaki değerleri (ilk 50, alfabetik) kutuya yazar. Ara küme özeti / grup içi ilk N / pencere koşuluyla birleşimi anlaşılır mesajla reddedilir. Önyüz: **"Çapraz Tablo (değerleri sütuna çevir)"** bölümü, cümle biçiminde kurulum ("Sütun başlığı olacak kolon … hücre: Topla … değer kolonu") + değer listesi + neden sabit olduğunu anlatan not. **Kanıt:** 2 test; yıl-sütunlu çapraz gerçek KdsDemo'da.<br>**Y10 ✅ (17 Ağu 2026) — ara toplam / genel toplam satırı:** `SubtotalSpec` (tür · etiket · etiketle). `GROUPING SETS ((kırılımlar), ())` ile yalnız genel toplam ya da `ROLLUP(...)` ile kademeli ara toplamlar. Özet satırlarında kırılım hücresi NULL gelir; GERÇEK boş değerden ayırmak için her kırılıma **`GROUPING()` bayrağı** eklenir ve dış katman etiketi (varsayılan "(Tümü)") o bayrağa bakarak yazar — etiketleme kapatılabilir (o zaman hücreler boş kalır, kolon tipi değişmez; açıkken kırılım kolonları metne döner ve bu ekranda yazılıdır). Kırılımsız ara toplam ve çapraz tabloyla birleşim reddedilir. Önyüz: **"Ara Toplam / Genel Toplam Satırı"** bölümü — "Yalnız GENEL toplam (en sona tek satır)" / "Kademeli — her kırılım düzeyi için ara toplam" seçimi + etiket anahtarı ve metni. **Kanıt:** 3 test; etiketli genel toplam satırı gerçek KdsDemo'da (68.997.239.400 = tüm markaların toplamı).<br>**Y11 ✅ (17 Ağu 2026) — kaydı var/yok (EXISTS / NOT EXISTS):** `ExistenceCondition` + `ExistenceLink`. Join ile yapılsaydı iki tuzak vardı: çoklu eşleşme ana satırı **ÇOĞALTIR** (toplamlar şişer) ve "hiç yok" sorusu join'le sorulamaz. Aranan tablo ana sorguya **join'lenmez**, alt sorguda kalır: ne satır çoğalır ne kolon eklenir. Bağ kolonları verilmezse Veri Haritası'ndaki ilişkiden türetilir; bağlanılan tablo sorguda değilse açık mesaj verilir. Aranan kayda ek şart yazılabilir ("iptal edilmemiş siparişi olanlar") ve koşullar VE/VEYA ile birleşir. Önyüz: **"Kaydı Var / Yok (ilişkili tabloda ara)"** bölümü — "eşleşen kaydı OLANLAR / OLMAYANLAR" + tablo seçimi + **"Aranan kayda şart ekle"**; altında "bağlantı kolonları Veri Haritası'ndaki ilişkiden bulunur" notu. **Kanıt:** 3 test; talebi olan/olmayan araç sayıları gerçek KdsDemo'da (1.500.000 + 0 = toplam araç).<br>**S17 kapanış:** 11 maddenin tamamı bitti · **262 backend + 52 önyüz testi yeşil** · üretilen SQL biçimlerinin hepsi gerçek KdsDemo verisinde çalıştırılarak doğrulandı.<br>**Kapanış denemesi (17 Ağu 2026):** 15 örnek raporun **tamamı** tasarımcı modeliyle tek tek kuruldu ve gerçek KdsDemo'da çalıştırıldı — analizde ❌ olan 9 raporun hepsi artık kurulabiliyor (R6 Y6+Y1 · R7 Y4+Y5+Y6+Y1 · R8 Y7 · R9 Y11+Y3 · R10 Y9+Y4 · R11 Y3 · R13 Y6+Y1+Y2 · R14 Y10+Y1 · R15 Y1+Y2). R11 gerçek eşiğinde (3+ marka) boş döner — demo veride her servis tek markalıdır, HAVING doğru çalışmaktadır.<br>**Demo DB indeks açığı kapatıldı:** deneme sırasında SQL Server'ın kendi eksik-indeks kayıtları 3 öneri verdi (Randevular·KanalId, AracHasarKayitlari·AracId, ServisMarkaYetkileri·ServisId — kapsayan hâlleriyle eklendi) ve şema taraması 10.000+ satırlı tablolarda ilk anahtarı o kolon olmayan **51 FK kolonu** buldu. `infra/kdsdemo-indeksler.sql` bunları **dinamik ve idempotent** bir blokla kapatacak şekilde genişletildi (yeni tablo/FK eklenirse script'i güncellemek gerekmez) + `sp_updatestats`. Sonuç: **49 FK indeksi + 3 kapsayan indeks, ~917 MB**, kalan FK açığı **0**, kalan eksik-indeks önerisi **0**. Ölçüm: R11 29 ms · R14 111 ms · R9 650 ms. **Dürüst not:** darboğaz sanılan R1 (12 tablolu zincir) indekse bağlı değildi — öncesinde de sonrasında da ~2,1-2,9 sn; denemedeki 45 sn'lik zaman aşımları eşzamanlı test koşumlarının yükündendi. | 🟢 |

**v1 çıkış kriteri:** [00-analiz §12](00-analiz.md)'deki dört başarı ölçütünün dördü de
gerçek bir MSSQL kaynağına karşı elle doğrulanmış olmalı.

### S18 — Kalan borçların kapatılması *(kullanıcı kararı 17 Ağu 2026: "sırasıyla bu maddeleri de bitirelim, her şey tamam iken test yapmak istiyorum")* ✅

Elde kalan üç iş sırayla kapatıldı; hiçbiri yeni bir istekten değil, **kayıtlı açıklardan** geliyordu.

1. **S13 Part 4 canlı koşum ✅** — ayrıntı yukarıda S13 satırında. Özet: 5 servis Up, API/kimlik/
   Keycloak/önyüz uçları doğrulandı, migration konteynerde uygulandı, sonra temizlendi.
2. **Bağlantı parolalarının anahtar halkası ✅** — uygulama geneli Data Protection halkası kalıcı
   veri dizinine alındı (`<veriDizini>/anahtarlar-uygulama`), eski konumdaki anahtarlar açılışta
   **kayıpsız kopyalanıyor**, uygulama adı bilerek değiştirilmedi (yoksa eski şifreli değerler
   çözülemezdi). Kanıt: container silinip yeniden yaratıldıktan sonra **aynı anahtar** yerindeydi;
   dev makinede 1 anahtar göç etti ve mevcut parolalar çözülmeye devam etti. 4 yeni test.
   Ayrıntı: [07-kurulum-ve-sistem-ayarlari.md](07-kurulum-ve-sistem-ayarlari.md).
3. **Tasarımcının bilinen 5 sınırı (Y12–Y16) ✅** — DISTINCT · LEFT join ON'una sabit koşul ·
   self-join (tablo örneği) · UNION · OFFSET/FETCH sayfalama. Tasarım notları ve ekrandaki
   adlandırmalar: [08-tasarimci-yetenek-acigi.md §3](08-tasarimci-yetenek-acigi.md).
   **Kanıt:** 12 altın-örnek + 4 model testi → **274 backend + 55 önyüz testi yeşil**; beş biçim de
   gerçek KdsDemo'da çalıştırıldı (Y13'te LEFT join ana satırları elenmedi).

### S19 — Satır-düzeyi güvenlik (RLS) *(kullanıcı kararı 18 Ağu 2026: "sırayla yapalım" — E2E/konsolidasyon sonrası sıradaki iş)* ✅

Araştırma kuyruğunda **"v1 önerisi"** diye işaretli tek maddeydi. Ayrıntılı tasarım ve güvenlik
kararları: **[09-satir-duzeyi-guvenlik.md](09-satir-duzeyi-guvenlik.md)**.

**Belirleyici analiz bulgusu:** `QueryController` zaten `[Authorize(Roles=admin,gelistirici)]` —
`musteri` rolü tasarımcıya da elle SQL'e de erişemiyor, kayıtlı rapor/pano çalıştırıyor. Bu, RLS'in
doğru yerini belirledi: **sorgu üretimi** (kullanıcının dokunamadığı katman) ve tek boğaz noktası
`RunQueryModelHandler` — tasarımcı "Çalıştır", rapor, pano widget'ı ve abonelik e-postası aynı anda
kapsama girdi.

**Kapatılan üç kaçış yolu (hepsi testli):**
1. **VEYA'lı filtre** — kullanıcı filtresi `A VEYA B` iken düz `AND` eklemek sol dalı kuralsız
   bırakırdı; kullanıcı filtresinin TAMAMI paranteze alınıyor.
2. **UNION** — kurallar birleşim dallarının HER BİRİNE uygulanıyor.
3. **Modelden geçmeyen SQL** — `POST /api/sorgu/ham` ve ham SQL ile kaydedilmiş raporlar kurala
   tabi kullanıcılara kapalı (mesajlar çözümü söylüyor).

**Fail-closed:** kural uygulanacakken değeri çözülemiyorsa (token'da iddia yok) sorgu
ÇALIŞTIRILMIYOR → **403**. Sessizce filtresiz koşmak, kullanıcının her şeyi görmesi ve kimsenin fark
etmemesi demekti.

**Kural modeli:** `SatirKurallari` tablosu (migration `AddSatirKurallari`, uygulandı) — harita
tablosu + kolon + operatör + **değer kaynağı** (token iddiası · sabit · kullanıcı adı · e-posta) +
roller + aktiflik + ad. Değerin token'dan gelmesi bilinçli: kullanıcı nitelikleri için ikinci bir
doğruluk kaynağı yaratmak yerine kimlik sağlayıcı kullanılıyor.

**Ekran:** Yönetim → **Satır Güvenliği (RLS)** (yalnız admin). Kural cümle gibi okunuyor
("KdsDemo · **Talepler** tablosunda `ServisId = Kullanıcının kimlik bilgisinden (servis_kodu)`");
üstteki kutu ne zaman devreye girdiğini, admin muafiyetini, fail-closed'ı ve ham SQL kapılarını
anlatıyor. Kural kaydedilirken tablo-harita tutarlılığı ve kolonun varlığı doğrulanıyor — yanlış
kural, çalıştırma anında sorguyu durdurmadan önce yakalanıyor.

**Kanıt:** 11 yeni test (5 üretici kaçış/enjeksiyon + 6 çözücü) → **285 backend + 55 önyüz testi
yeşil**; API DI ile sağlıklı açılıyor.

**Kimlik sağlayıcı + canlı doğrulama (18 Ağu 2026):** realm'e `servis-kodu` eşleyicisi ve `bayi`
demo kullanıcısı eklendi; **aynı kayıtlı rapor** admin ile 50 servis, bayi ile **yalnız 1632**
döndü; eksik iddia ve ham SQL raporu **403** verdi. Realm dosyası sıfırdan yeni bir realm'e içe
alınarak da sınandı (elle ayar şüphesi elendi). Yakalanan üç tuzak — `unmanagedAttributePolicy`
kapalıyken niteliklerin **sessizce silinmesi**, realm JSON'una yorum alanı koymanın içe alımı
tamamen düşürmesi, kısmi `PUT`'un kullanıcı adını silip girişi kilitlemesi — 09 numaralı dokümanın
§7.1–7.3'ünde.


### Ortam temizliği + E2E artık sızıntısı (19 Ağu 2026) ✅

Menü maddelerine geçmeden önce dev ortamı temizlendi (kullanıcı: *"açıkta bir işimiz kalmasın"*).
Silinen **71 artık kayıt**: 51 rapor · 23 pano · 7 abonelik. Kalanların tamamı kullanıcının kendi
verisi; silme ham SQL yerine **API üzerinden** ve doğru sırayla yapıldı (pano→abonelik→rapor), böylece
favori/pano referansları düzgün çözüldü. `smtp4dev` konteyneri ve imajı da kaldırıldı.

**Kök neden — asıl mesele buydu:** 71 kaydın 66'sı **E2E süitinin bıraktığı artıktı** ve süit her
koştuğunda yeniden birikiyordu (9/10/13/17 Ağustos turlarının hepsinin izi vardı). Tek seferlik
temizlik borcu kapatmaz; bugün silinen yarın geri gelirdi. Sonuç: 58 raporun 42'si, 25 panonun 23'ü
test verisiydi — **gerçek veri artıkların içinde kayboluyordu.**

**Çözüm:** `e2e/temizlik.teardown.ts` + `playwright.config.ts`'te `setup` projesine `teardown`
bağlandı → süit **nasıl biterse bitsin** (yeşil ya da kırmızı) ürettiklerini siler. Sözleşme:
testler her kaydı **`E2E ` önekiyle** adlandırır; önek dışındaki hiçbir kayda dokunulmaz, yani
kullanıcı verisi asla silinemez. Temizlik tarayıcı açmaz — kimlik setup'ın dosyaladığı token'dan
okunur, API doğrudan çağrılır (önyüz kırıkken de çalışır). Silme sırası referansa göre:
pano → abonelik → rapor → içe alım işi → harita → bağlantı.

**Kanıt:** koşum çıktısında `[temizlik] abonelik: 1/1 artık silindi · [temizlik] rapor: 1/1 artık
silindi`; koşum sonrası veritabanında **altı varlık türünde de `E2E ` önekli kayıt sayısı 0**.

**Testler arası veri bağımlılığı da kapatıldı (aynı gün, kullanıcı: *"şimdi kapatalım bir şey
kalmasın"*).** `crud.spec.ts`'teki pano testi, başka bir testin bıraktığı `E2E SQL Rapor`'a
bağımlıydı — kodda yorumu bile vardı (*"sıralı koşumda mevcut"*) ve tek başına koşunca düşüyordu.
Artık test **kendi bağımlılığını üretiyor**: yeni `e2e/apiYardimci.ts` içindeki `hamSqlRaporVarEt`
gerekli raporu API'den var ediyor (yoksa oluşturuyor), ad `E2E ` öneki taşıdığı için teardown
temizliyor. Kimlik/önek/API tabanı da bu modülde toplandı; teardown oradaki yardımcıları kullanıyor
(tekrar kalmadı).

**Kanıt:** daha önce düşen komut — `playwright test e2e/crud.spec.ts --grep "Panolar CRUD"` —
tek başına **geçiyor** (8.4 sn), teardown ürettiği pano ve raporu siliyor.

### Süit hiç sonuna kadar koşmuyormuş — kimlik 15. dakikada ölüyordu (19 Ağu 2026) ✅

Teardown'ı doğrulamak için tam süit koşuldu ve **28 test düştü / 10 geçti (37 dk)**. Tek tek
koşulduklarında hepsi geçtiği için sebep arandı; **ölçümle** üç ayrı kök neden çıktı:

**1. Kimlik koşumun ortasında ölüyordu (asıl sebep).** Access token ömrü **900 sn**, süit **37 dk**.
Setup'ta bir kez alınan token 16:28:34'te öldü; `nav.spec` testleri 16:29'dan itibaren düşmeye
başladı — oysa aynı dosya tek başına 10.8 sn'de geçiyor. Kesin kanıt: teardown 16:50'de **altı ucun
hepsinden 401** aldı ve hiçbir şeyi temizleyemedi. Yani süit bugüne kadar hiç tam koşmamıştı;
parça parça koşulduğu için fark edilmemişti. Düzeltme: `fixtures.ts` her testten önce refresh
token ile yeniliyor ve yenilenen kullanıcıyı dosyaya geri yazıyor (rotation'da zincir kopmasın,
SSO oturumu boşta sayılmasın); `apiYardimci.tazeJeton` aynısını teardown/API yardımcıları için
yapıyor. Yenileme başarısızsa eldeki token kullanılıyor — iyileştirme, yeni kırılma noktası değil.

**2. `ingest.spec` 5 gündür kırıktı.** 14 Ağu 2026'da (`d8de088`) çıplak cron kutusu **CronSecici**
ile değişti; test hâlâ eski `Günlük 03:00` düğmesini arıyordu. Yeni akışa uyarlandı ve **daha güçlü**
hâle getirildi: artık yalnız düğmeye basmakla kalmıyor, seçicinin ürettiği insan-dili özeti
("Her gün saat 03:00'de çalışır") doğrulanıyor.

**3. `m-testleri` yok olmuş bir fixture'a bağlıydı.** Beş test `KDSDemoHarita1`'i seçiyordu; o harita
ortada yok (mevcut: Harita2/Harita3/…), testler **60–90 sn görünmeyen bir seçeneği bekleyip** zaman
aşımıyla düşüyordu ve mesaj eksik fixture'ı söylemiyordu. Artık ortak `haritaSec` yardımcısı ön
koşulu açıkça doğruluyor: harita yoksa test saniyeler içinde ve **nedenini söyleyerek** düşüyor.
Yanında iki kırılganlık daha giderildi: (a) M4 `AktifMi` kolonunu seçiyordu — o ad haritada **4
tabloda** geçiyor ve `.first()` yanlış tabloyu yakalıyordu (benzersiz `BeyanTuru`'ya geçildi);
(b) join etiketleri tek yöne çakılı aranıyordu, oysa ağacın kökü değişince etiket ters yönde
yazılıyor — artık yöne duyarsız aranıyor (iddia "bağlantı kuruldu", "şöyle yazıldı" değil).

**Sonuç — süit ilk kez uçtan uca yeşil:**

| | Önce | Sonra |
|---|---|---|
| Sonuç | 28 düştü / 10 geçti | **38 geçti / 0 düştü** |
| Süre | 37 dk | **3.2 dk** |
| Koşum sonrası artık | (teardown 401 aldı, temizleyemedi) | **6 varlık türünde de 0** |

Süredeki 11 kat fark gerçek bir kazanç değil, hastalığın ölçüsü: düşen her test 60–90 sn zaman
aşımı + tekrar yakıyordu. Not: **hiçbir üründe hata yoktu** — üç kusurun üçü de ölçüm aracındaydı
(kimlik, bayat fixture, kırılgan iddia). Kanvas ekran görüntüsü join zincirinin doğru kurulduğunu
gösteriyordu; yanlış olan testti.

### S20 — Uyarılar (eşik bildirimi) *(sol menü sırası: madde 3)* ✅

Menüdeki ilk gerçek boşluktu. **Aboneliğin kardeşi ama işi zıt:** abonelik takvim geldiğinde HER
ZAMAN gönderir ("işte bu sabahki rapor"); uyarı **yalnız durum kötüyse** gönderir ("dikkat, eşik
aşıldı"). Bu yüzden aboneliğe alan eklenerek değil, ayrı varlık olarak modellendi — karşılaştırma
tanımı (kolon+özet+operatör+eşik) ve **durum hafızası** abonelikte yoktu.

**Uyarı = rapor + neye bakılacağı + eşik + sıklık + alıcılar.** Özet seçenekleri: satır sayısı ·
toplam · ortalama · en büyük · en küçük · ilk satır. `Satır sayısı > 0` en yaygın biçim — *"hatalı
kayıt VARSA haber ver"* demenin doğal yolu, kolon seçmeden çalışır.

**Kritik tasarım kararı — ne zaman SUSAR.** Bu özelliği çöpe çeviren klasik hata, eşik aşılı kaldığı
sürece her kontrolde posta atmaktır: saat başı kontrolde bir haftalık aşım **168 e-posta** demektir
ve uyarı ilk haftasında okunmaz hâle gelir. Bu yüzden uyarının **durumu** vardır ve bildirim yalnız
geçişte gider: sakin→tetiklendi bir posta, sonra sessizlik, tetiklendi→sakin "normale döndü" bilgisi
(bu ikincisi olmazsa "sustu mu, düzeldi mi?" belirsizliği kalır). "Her kontrolde tekrar bildir"
isteyen açıkça seçer; varsayılan kapalıdır ve ekran yanında ne demek olduğunu yazar.

**Canlı kanıt (19 Ağu 2026):** zamanlayıcı dakikada bir ölçtü; eşik değişip durum sıfırlanınca ilk
turda **bildirim gitti** (21:37:49), bir sonraki turda son bildirim tarihi **değişmedi**
(21:39:34'te hâlâ 21:37:49) → bir geçiş, bir posta.

> **Yanlış okunan kanıt — düzeltildi.** Aynı denemede "durum zaten tetiklenmişken üç turda sıfır
> e-posta" gözlendi ve bu *susma kuralının çalıştığı* sanıldı. Değildi: durumu "Şimdi Dene" tüketmiş,
> gerçek bildirim hiç doğmamıştı (aşağıdaki inceleme bulgusu). Gözlem doğruydu, yorumu yanlıştı —
> yeşil görünen bir belirti, altında yatan hatayı gizleyebilir.

**Sessiz arızaya karşı üç karar:** (a) izlenen kolon rapor çıktısında yoksa **hata verilir**,
sessizce 0 dönülmez — 0, "her şey yolunda" demek olurdu ve rapor değişince uyarı körleşirdi;
(b) değer ölçülemiyorsa (satır yok/sayısal değil) **alarm üretilmez** — "veri yok" ile "eşik aşıldı"
aynı şey değildir; (c) eşik/kolon/rapor değişince **durum sıfırlanır**, eski karar yeni kurala ait
değildir.

**"Şimdi Dene" e-posta ATMAZ** — ölçer, sonucu gösterir, durumu yazar. Kurulum sırasında deneme
yapan kişinin alıcılara posta yağdırması özelliği kullanılmaz hâle getirirdi.

**Müşteri rolüne açıldı** (kullanıcı kararı 19 Ağu 2026: *"hem zamanlanmışları hem de uyarıları
müşteriye de gösterelim"*) — **Zamanlanmış Raporlar da**. Rol kapısı kaldırılmadı, **daraltıldı**:
müşteri yalnız KENDİ kayıtlarını görür/yönetir ve yalnız erişebildiği raporları izler/abone olur.
Kapı handler'larda tek yerde durur; başkasının kaydına erişim **404** döner (403 değil — "var ama
senin değil" bilgisi bile varlığını sızdırır). Elle koşum uçları da kapıya bağlandı: koşum
handler'ı zamanlayıcıyla paylaşıldığı için kendi içinde kapı taşıyamaz, controller önce erişimi
doğrular ("kimse başkasının id'sini bilemez" bir güvenlik kapısı değildir).

**Kapsam dışı (bilerek):** anomali tespiti (istatistiksel sapma) — menü adında geçiyordu, ekran
metni ve menü açıklaması **gerçeğe çekildi**; webhook/Slack; pano uyarısı. RLS notu: uyarı arka
planda kullanıcı bağlamı olmadan koşar, satır kuralları uygulanmaz (aboneliklerdeki gerekçenin
aynısı).

### S20 sonrası bağımsız kod incelemesi — 6 kusur bulundu ve düzeltildi (19–20 Ağu 2026)

Dilim "yeşil" göründüğü hâlde (tüm testler + E2E geçiyordu) commit'ten önce üç boyutlu bağımsız
inceleme koşuldu (güvenlik · doğruluk · desen), her bulgu ayrıca **çürütülmeye çalışılarak**
doğrulandı. Sonuç: **testlerin göremediği 6 gerçek kusur.** Hepsi düzeltildi ve regresyon testine
bağlandı.

| # | Kusur | Neden testler yakalamadı |
|---|---|---|
| K1 | **"Şimdi Dene" durum geçişini YUTUYORDU** — deneme `Tetiklendi`'yi yazıyordu, zamanlayıcı sonraki turda "değişiklik yok" deyip susuyordu; **gerçek uyarı hiç gitmiyordu** | Testler karar mantığını (saf fonksiyon) ölçüyordu, handler'ın kalıcı yazımını değil |
| K2 | Satır sınırına takılan raporda **kesik veriyle** eşik kararı veriliyordu ("Satır sayısı > 1000" sınır 1000'ken asla tetiklenmez) | `SatirSiniriUygulandi` bayrağı hiç okunmuyordu |
| K3 | **Doğrulayıcı yoktu**: geçersiz cron sessizce kaydediliyor, uyarı "Aktif" görünüp hiç koşmuyordu | Kardeş dilimde (`SaveSubscriptionValidation`) vardı, yenisinde unutulmuştu |
| K4 | **Ham SQL raporlar RLS kapısını atlıyordu**: `RunReportHandler` reddediyor, abonelik/uyarı koşum yolu aynı raporu filtresiz koşuyordu | RLS testleri model yolunu kapsıyordu; ham SQL'in ikinci koşum yolu kör noktaydı |
| K5 | **Zamanlanmış koşumda rol-kapsamlı kurallar sessizce düşüyordu**: arka planda kullanıcı bağlamı boş → `Roller='musteri'` kuralı eşleşmiyor, fail-closed bile devreye girmiyor | Kimlik bağlamı olmayan koşum yolu hiç test edilmemişti |
| K6 | Önyüz **parametre değerlerini göndermiyordu** — parametreli rapora kurulan uyarı her koşumda hata verecekti | Backend yolu vardı, ekran onu hiç doldurmuyordu |

**K4+K5 nasıl kapatıldı — ve ilk denemenin neden yetmediği.** İlk düzeltme kapıyı **kaydetme**
anına koydu: "satır kuralına tabi kullanıcı zamanlanmış iş kuramaz". Düzeltmeler ayrıca bağımsız
doğrulamadan geçirildi ve bu kapının **yetersiz** olduğu ortaya çıktı — üç delik birden:

1. Kapı eklenmeden **önce** kurulmuş işler koşmaya devam ediyordu (geri dolum yok).
2. Kural **sonradan** eklenince mevcut işler etkilenmiyordu.
3. Kapı işi **KURAN'ın** kimliğine bakıyordu: admin kurup alıcıya kurala tabi kullanıcıyı yazınca
   sızıntı geri geliyordu — ve reddetme mesajı bu kaçış yolunu kullanıcıya bizzat öneriyordu.

Ayrıca kapı ters yönde de yanlıştı: `KuralaTabiMiAsync` haritaya bakmadığı için kiracıdaki **tek**
bir kural, ilgisiz haritalardaki raporlara bile özelliği kapatıyordu.

**Doğru kapı — kimliğe değil VERİYE bakar ve KOŞUM tarafındadır** (`ZamanlanmisIsKapisi`):
- Model raporunda kapı **haritaya daraltılır**; kuralsız haritalar etkilenmez.
- Ham SQL raporunda hangi tabloya dokunulduğu bilinemez → kiracıda tek aktif kural bile yeter
  (ekran yolundaki `RunReportHandler` de aynı yasağı koyuyor: tutarlı).
- Her koşumda çalışır → mevcut kayıtlar, sonradan eklenen kurallar ve "admin kurar, kurala tabi
  kullanıcı alıcıdır" durumu kapsanır. Kaydetme anındaki kontrol kaldı ama artık yalnız **erken
  uyarı**: "kaydettim, çalışmıyor" hâlini önler.

**O günkü sınır — ve ertesi gün kaldırıldı:** bu hâliyle bir haritada aktif satır kuralı varken o
veriye dayanan zamanlanmış iş hiç çalışmıyordu. Güvenliydi ama özelliği RLS kullanan kiracılarda
kullanılamaz kılıyordu. Kalıcı çözüm **S21**'de (aşağıda): iş, **sahibinin kimliğiyle** koşuyor.

### S21 — Zamanlanmış işler sahibin kimliğiyle koşar *(20 Ağu 2026)* ✅

S20'nin bıraktığı sınırı kaldıran dilim. **Sorun:** abonelik/uyarı arka planda, HTTP isteği olmadan
koşar; RLS ise kimliğe dayanır. Boş kimlikle rol-kapsamlı kurallar sessizce düşer → S20'de
fail-closed kapı konmuştu → kurallı veride zamanlanmış iş hiç çalışmıyordu.

**Çözüm — kimlik anlık görüntüsü.** İş kurulurken sahibin **rol + iddia** kümesi token'dan okunup
işle birlikte saklanır (`SahipKimlikJson`; migration `AddSahipKimligi`). Koşumda geri yüklenip
`RunQueryModelRequest.KosanKimlik` ile taşınır; satır kuralları **o kimliğe** uygulanır. Böylece
zamanlanmış gönderim rapor sahibinin yetkisiyle koşar — piyasadaki karşılığı da budur.

**Neden anlık görüntü, neden canlı sorgu değil:** arka plan koşumunda Keycloak'a gidip güncel rolleri
sormak, koşumu dış servise ve ağa bağımlı kılardı; kimlik sağlayıcı erişilemezse ya iş düşer ya da —
daha kötüsü — "filtresiz koş" baskısı doğar. Bedeli bilinçli: rol sonradan değişirse iş eski
kimlikle koşar, bu yüzden görüntü **her kayıtta tazelenir** (testi var) ve yetki daraltıldığında
yöneticinin işi güncellemesi/pasife alması gerektiği burada yazılıdır.

**Kapı daraltıldı** — artık yalnız iki dar hâlde reddediyor:
1. **Kimlik yok** (kimlik saklanmadan önce kurulmuş eski kayıt) → mesaj çözümü söylüyor: "işi açıp
   bir kez kaydedin, sahip kimliği tazelenir".
2. **Ham SQL + sahibi kurala tabi** → rapor modelden geçmediği için kural enjekte edilemez; kimlik
   bilinse bile uygulanacak yer yok. (Ekran yolundaki `RunReportHandler` aynı ayrımı yapıyor.)

Kuralsız haritalar, kurala tabi olmayan sahipler ve admin sahipli işler **etkilenmiyor**.

**Canlı A/B kanıtı — aynı rapor, iki sahip:**

| Abonelik sahibi | Dönen satır |
|---|---|
| **bayi** (kurala tabi, `servis_kodu=1632`) | **1** — yalnız kendi servisi |
| **admin** (RLS'ten muaf) | **50** — tamamı |

Kimlik taşınmasaydı ikisi de 50 satır döner, yani bayi göremediği veriyi e-postayla alırdı.

**Yan etki (bilinçli):** `CalistiranKullanici` Business'tan **Contracts**'a taşındı — kimlik artık
bir sözleşme parçası (istek nesnesi taşıyor). 13 kullanım güncellendi.

**Kanıt:** **backend 325/325 yeşil** (6'sı yeni: sahibin kimliğiyle koşum, kimliksiz eski kayıt
reddi, ham SQL iki yönü, kuralsız harita etkilenmezliği, görüntünün her kayıtta tazelenmesi).

**Yanında yakalanan E2E kırılganlığı:** yeni kapı devreye girince `abonelik.spec` ve `uyarilar.spec`
düştü — ama sebep üründe değildi: iki test de listedeki **ilk** "Şimdi Gönder"/"Şimdi Dene"
düğmesine basıyordu (`.first()`), yani kendi kurduklarını değil **rastgele bir kaydı** sınıyorlardı.
Liste son koşuma göre sıralı olduğu için hangi kayda bastıkları koşuma göre değişiyordu; eski
kayıtlar (sahip kimliği saklanmadan önce kurulmuş) kapıya takılınca ortaya çıktı. Düğmeler artık
testin **kendi kartından** seçiliyor. Kart seçicisinin kendisi de iki koşulla daraltıldı (adı geçen
VE düğmesi olan Paper): yalnız ada bakmak, bilgi şeridini (o da bir Paper ve mesajında ad geçiyor)
yakalıyordu. Son iddia da metin yerine **kartın** yokluğuna bakıyor — ad, silme sonrası bilgi
şeridinde hâlâ yazılı kalıyor.

**K1'in ayrıca öğrettiği:** kural artık "durum yalnız **bildirim borcu kapandığında** ilerler" —
bildirim gerekmiyorsa ya da gerçekten gönderildiyse. Alıcı listesi boşken, SMTP yapılandırılmamışken
ya da gönderim düştüğünde bayrak ilerlemez, sonraki koşum tekrar dener. Yoksa "geçişi tüketip
kimseye haber vermeme" hâli kalıcı sessizlik üretiyordu.

### S21 sonrası: ekran kullanım testi + tüm-proje kod incelemesi → 8 küçük bulgu kapatıldı (20 Ağu 2026) ✅

**Ekran testi** gerçek tarayıcıyla, kullanıcı gibi yapıldı (admin + bayi, 36 ekran görüntüsü, konsol/
ağ hataları izlendi): tüm ekranlar render oluyor; pano, silinmiş rapor/haritada zarif bozuluyor;
bayi menüsü doğru daralıyor; elle URL yazılan 10 yetkisiz ekranda veri sızmıyor; **S21 onarım
yönergesi ekrandan kanıtlandı** (eski uyarı 403 → aç → Kaydet → Şimdi Dene → ölçüm başarılı).

**Kod incelemesi** (~22K satır): 14 controller'ın tamamı yetkili + fallback policy; ham SQL AST
beyaz-liste bekçisi fail-closed; kimlikler haritadan doğrulanıyor; `IgnoreQueryFilters` yalnız
meşru 4 yerde; parola loglanmıyor; kritik/önemli açık bulgu **yok**.

**Kapatılan 8 küçük** (hepsi test/ekran doğrulamalı):
1. Rol rozeti Keycloak'ın teknik rollerini gösteriyordu ("default-roles-kds · offline_a…") →
   whoami yalnız ÜRÜN rollerini süzüyor; rozet artık "musteri" *(ekrandan doğrulandı)*.
2. `apiGet` hata gövdesini atıyordu → ProblemDetails okunuyor (apiSend ile aynı).
3. Gövdesiz 403 ham "API 403: /yol" görünüyordu → "Bu işlem için yetkiniz yok." *(ekrandan doğrulandı)*.
4. `IlkSatir` özeti "ilk SAYISAL değer"e kayıyordu → gerçekten ilk satıra bakıyor; ilk hücre NULL
   ise ölçülemedi (alarm yok).
5. bit/bool hücreler sessizce atlanıyordu → 1/0 sayılıyor ("hatalı kayıt var mı" bit kolonları çalışır).
6. NaN/taşan float `OverflowException` ile uyarıyı düşürüyordu → NaN=ölçülemedi; aralık dışı uca
   satüre (1e300'lük okuma her eşiğin üstündedir); toplam taşarsa okunur mesaj.
7. `SaveAlertHandler.BenimMi` sabit `true` dönüyordu → gerçek sahiplik.
8. Uyarı kaydetme hatası çekmecenin ARKASINDA basılıyordu → çekmece içinde; `Zamanlama`'ya 100
   karakter sınırı da doğrulamada (DB hatası yerine anlaşılır ret).

**Kanıt:** `Uyarilar` tablosu + `AddUyarilar` migration (uygulandı) · zamanlayıcı (Cronos +
BackgroundService, abonelik/içe alım kardeşi) · **backend süiti yeşil; 18'i yeni test** —
değerlendirme çekirdeği, sahiplik kapısı, **durum ilerlemesi (handler'ı gerçekten koşturan 4 test)**
ve **RLS kapısı (3 test)** · **E2E `uyarilar.spec`** (kur → kural cümlesi → Şimdi Dene ölçsün ve
posta atmadığını söylesin → sil) · TypeScript 0 hata.

## Sol menü — kalan maddeler (SIRALI çalışma listesi)

**Çalışma kuralı (kullanıcı, 18 Ağu 2026):** iş sırası **sol menüdür**; maddeler menüdeki sırayla
gidilir. **Yeni bir menü maddesine geçmeden önce** teslim edilmiş maddelerde eksik/ertelenmiş parça
kalmadığı doğrulanır. Bir maddenin devredilen borcu varsa **o madde kapatılmadan** kapatılır.

Menü sırası kaynağı: [`frontend/src/layout/menu.tsx`](../frontend/src/layout/menu.tsx) · görev/rol
tablosu: [05-menu-arastirma.md](05-menu-arastirma.md) §6. Yer tutucu (`BlankPage`) olan 7 madde:

| # | Menü maddesi | Grup | Kapsam / devredilen borç | Durum |
|---|---|---|---|---|
| 1 | ~~**Soru Sor**~~ (NLQ) | Çalışma Alanı | Doğal dille sorgu → JSON modeli — **S23'te yapıldı** (aşağıda) | ✅ |
| 2 | ~~**Metrikler**~~ | Çalışma Alanı | Merkezi ölçüt kataloğu — **S22'de yapıldı** (aşağıda) | ✅ |
| 3 | ~~**Uyarılar**~~ | Otomasyon | Eşik bildirimi — **S20'de yapıldı** (aşağıda) | ✅ |
| 4 | ~~**Roles & Yetkiler**~~ | Yönetim | Rol tanımı + **rol-bazlı rapor/klasör görünürlüğü** — **S27'de yapıldı** (aşağıda; devir kapandı) | ✅ |
| 5 | ~~**Kuruluş Ayarları**~~ | Yönetim | Kuruluş adı, plan, marka — **S35'te yapıldı** (aşağıda; limitler bilinçli olarak Sistem Ayarları'nda kaldı) | ✅ |
| 6 | ~~**Gömme & Marka**~~ | Yönetim | Embed token, izinli alan adları, beyaz-etiket — **S42'de yapıldı** (aşağıda; v2'nin ilk dilimi) | ✅ |
| 7 | ~~**Kullanım**~~ | Yönetim | Kullanım/performans izleme, az kullanılan içerik — **S41'de yapıldı** (aşağıda) | ✅ |

### S22 — Metrikler: merkezi ölçüt kataloğu *(20 Ağu 2026; sıra takası kullanıcı onaylı — NLQ'nun ön koşulu olduğu için Soru Sor'dan önce alındı)* ✅

**Sorun:** "Toplam Ciro"yu üç kişi üç raporda üç farklı formülle kurabilir — hangisi doğru,
belirsiz. **Metrik = ölçütün TEK, adlandırılmış, merkezi tanımı:** ad + formül + biçim (₺/#/%) +
açıklama + bağlı harita. Formül dili yeni değil: tasarımcının **Y1 ifade dili** aynen kullanılıyor
(`SUM({Odemeler.Tutar})`) — yeni motor yazılmadı.

**Kritik karar — kopya değil REFERANS.** Tasarımcının Kolonlar & Ölçüler adımına "Σ Metrik Ekle"
geldi; modele formülün kopyası değil metriğin **kimliği** girer (`MetrikAlanlar`), formül **her
koşumda** katalogdan çözülür (`MetrikCozucu` → `RunQueryModelHandler`, yani tasarımcı/rapor/pano/
abonelik/uyarı TÜM yolları tek noktadan kapsanır; üretici metrik kavramını hiç bilmez). Canlı
kanıt: model hiç değişmeden formül SUM→AVG yapılınca aynı sorgu 1.981.200 yerine 30.018 üretti —
tanım güncellenince onu kullanan her rapor bir sonraki koşumda yeni tanımı kullanır.

**Fail-closed:** silinmiş/pasif/başka-harita metriği sessizce atlanmaz — sorgu açık hatayla durur,
mesaj hangi metriğin neden kullanılamadığını söyler; silme onayı da "N rapor kullanıyor" der
(kullanım, ModelJson'da kimlik aramasıyla bulunur). Ad kiracıda **benzersiz** — "iki farklı Toplam
Ciro" tam da bu özelliğin çözdüğü sorun.

**Formül kaydetme anında üreticinin kendisiyle doğrulanır** (metriği tek alan yapan deneme modeli
haritada SQL'e çevrilir) — "geçerli görünüp çalışmayan metrik" kaydedilemez (K3 dersi); hatalıysa
üreticinin yol gösteren mesajı aynen kullanıcıya gider ("Kolon haritada yok: Talepler.OlmayanKolon").

**Ekran:** katalog kartları cümle gibi (ad + biçim çipi + formül + harita + açıklama + kullanım
sayısı + kim tanımladı) + **"Ölç"**: güncel değeri o an hesaplar (otomatik ölçüm bilerek yok —
pahalı). Ölç normal koşum hattından geçer: RLS/sınır/denetim aynen uygulanır. Yazma admin+
geliştirici; müşteri kataloğu görür ve ölçer (menü M/G/A).

**Yanında düzeltilen üretici sınırı:** "En az bir alan seçilmeli" kuralı hesaplanan kolonu/metriği
saymıyordu — "yalnız Toplam Ciro" (tek KPI) meşru bir sorgudur ve Ölç tam bunu yapar. Üretici +
validator hizalandı; alansız modelde FROM kökü formülün ilk `{Tablo.Kolon}` atfından deterministik
seçilir (2 yeni üretici testi).

**Kapsam dışı (bilerek):** ~~metriğe bağlı uyarı · sürümleme · import-export~~ → **✅ S30 (23 Ağu
2026)**; OSI/Ossie semantik uyumu v2 kuyruğunda.
**Stratejik amaç:** sıradaki madde Soru Sor (NLQ) bu kataloğu sözlük olarak kullanacak — Açıklama
alanı ekranda "Soru Sor (NLQ) bunu okuyacak" diye işaretli.

**Kanıt:** `Metrikler` tablosu + `AddMetrikler` migration (uygulandı) · **8 yeni backend testi**
(6 çözücü: referans çözümü, güncel-formül, fail-closed üçlüsü, takma ad; 2 üretici: alansız model)
→ süit yeşil · **E2E `metrikler.spec`** (kur → formül doğrulama → katalog → Ölç ₺ biçimli → sil) ·
canlı: Ölç **68.997.239.400 ₺** (641 ms) · kırılımla servis başına tutar · SUM→AVG tek-kaynak kanıtı.

### S23 — Soru Sor (NLQ): Türkçe soru → JSON sorgu modeli, tamamen YEREL modelle *(20 Ağu 2026)* ✅

**Karar (kullanıcı):** dış AI API'si YOK — SQLST ailesinde kanıtlanmış yerel altyapı aynen taşındı:
**Ollama (127.0.0.1:11434) + qwen2.5-coder:7b (Apache-2.0)**, `temperature 0.2 · num_ctx 8192`.
Soru ve şema adları makineden çıkmaz; internet gerekmez. KDS farkı: model **SQL değil, bizim JSON
sorgu modelimizi** üretir ve o model **normal hattan** koşar (`RunQueryModelRequest` → metrik
çözümü → üretici → SELECT bekçisi → RLS → satır sınırı → denetim). AI için ayrı koşum yolu yoktur —
en kötü senaryoda model uydurma kolon yazar, üretici "haritada yok" der, sorgu hiç koşmaz.

**Mimari (EpostaGonderici deseni):** `SoruIstemleri` (SAF istem kurucu — Business) →
`ISoruCevirici` sınırı → `OllamaSoruCevirici` (IO adaptörü — Api; adres/model her çağrıda Sistem
Ayarları'ndan: **Soru Sor** grubu) → `ModelKurtarici` (SAF: çit ayıkla → ara JSON → doğrulanmış
model). Zaman aşımı 300 sn (SQLST duruşu): 7B model CPU'da 60-120 sn düşünür, bu hata değildir —
ekran "yerel model düşünüyor" + saniye sayacı gösterir; Ollama yoksa ürün çökmez, 503 + yönerge
kartı (kur/başlat/pull komutu) döner, ürünün geri kalanı etkilenmez.

**Kurtarıcı GERÇEK modelle kalibre edildi** (3 fizibilite koşumu, prompt inceltmek yerine kodda
deterministik normalizasyon): ```json çiti `format:json`'a rağmen gelebiliyor → ayıklanır; model
metriği seçip AYNI hesabı alanla da kurabiliyor → metrik formülüyle çakışan toplamalı alan düşer;
"aylık talep sayısı" tek alana kova+toplama sıkıştırıyor (Y4 red) → kırılım+ölçü diye ikiye ayrılır;
ad eşleme toleranslı (dostça/ham) ama **listede olmayan ad uydurmadır → açık red** (S22 fail-closed
diliyle aynı). Gruplama modelden istenmez — üretici toplamasız alanları kendisi gruplar.

**Metrik kataloğu = NLQ sözlüğü (S22'nin stratejik amacı gerçekleşti):** istemde haritanın aktif
metrikleri ad+açıklamayla listelenir ("ÖNCE metrik kataloğuna bak" kuralı); model formülü hiç
görmez, referans kullanır — cevap tanımlı ölçütlere dayanır, halüsinasyon yüzeyi küçülür.

**Ekran (üç güven taahhüdü):** her cevapta **"Nasıl hesaplandı?"** (metrik/kırılım/filtre/sıralama/
limit + RLS notu, dostça adlarla) · **"Modele gideni gör"** = SQLST **Show Prompt** kuralı:
gösterilen metin modele gidenin TA KENDİSİ (yalnız tablo/kolon adı+tipi + metrik sözlüğü gider;
satır verisi/bağlantı/sunucu adı asla) · **"Üretilen sorguyu gör"** = JSON model + ham çıktı.
**"Rapor olarak kaydet"** AI'nın kurduğu modeli normal rapora çevirir (Özel paylaşım) — tasarımcıda
açılıp düzeltilebilir: AI kara kutu değil, sorgu kuran yardımcı. Roller: sorma herkese açık (RLS
sorana uygulanır); rapor kaydetme admin+geliştirici.

**Kanıt:** **13 yeni backend testi** (istem kurucu gizlilik/katalog; kurtarıcı: çit, çift-hesap,
kova ayrıştırma, op eşleme, uydurma red, belirsiz kolon, hata kaçışı, özet; handler: Show Prompt +
normal hat + pasif metrik katalog dışı) → süit yeşil · **E2E `sorusor.spec`** (kesiştirilmiş ağla
cevap+şeffaflık; Ollama-yok kartı) · **canlı doğrulama** gerçek Ollama'yla ekran turu (21 Ağu):
"servis başına toplam talep tutarı, ilk 3" → çeviri **98.3 sn** + sorgu **226 ms**, doğru ilk-3
tablosu; "Nasıl hesaplandı"da çift-hesap eleme notu CANLIDA tetiklendi (model metriği seçip SUM'ı
da kurmuştu — kurtarıcı düşürdü); Show Prompt gerçek şema+katalogla doğru; rapor kaydet/sil temiz;
konsol/ağ hatası sıfır. Yanlış haritayla sorulan soruda fail-closed açık hata, soğuk LocalDB'de
60 sn SQL zaman aşımında yol gösteren mesaj (M7) ekranda birebir görüldü.
**Kapsam dışı (bilerek):** ~~sohbet geçmişi · çok turlu düzeltme ("bunu aylık yap") · grafik
önerisi~~ → **✅ S30 (23 Ağu 2026)**.

### S24 — Tema: Lacivert & Teal açık/koyu çifti *(21 Ağu 2026)* ✅

**Süreç (kullanıcı ile mockup turu):** ui-ux-pro-max skill'iyle 10 tasarım yönü üretildi (önce
renk çeşitleri, sonra yapı çeşitleri: üst menü / bento / veri masası / dev-KPI…), ardından
login'li + 16 çalışan ekranlı **gezilebilir tam prototip** üzerinde karar verildi:
**tek kimlik Lacivert & Teal, açık + koyu eş** (artifact: KDS Canlı Mockup).

**Ürüne taşınanlar:**
- **Login (Keycloak kds teması):** sol marka / sağ form oranı **40/60** (önce 25/75); marka
  paneline "Veri konuşur, karar sizde" sloganı + açıklama + taahhüt üçlüsü (**%100 Yerel model ·
  0 Buluta giden veri · 7/24 Uyarı nöbeti**). Kaynak `infra/keycloak-theme`, çalışan kopya
  `infra/keycloak/themes` (git dışı) elle eşitlenir.
- **Önyüz koyu modu:** `theme.ts` tema fabrikası oldu (`temaYap('acik'|'koyu')`) — koyuda zemin
  `#0b1622`, panel `#12202e`, birincil eylem rengi teal; ray + PageHeader bandı gradyanları moda
  göre. `TemaSaglayici` tercihi **localStorage'da kalıcı** tutar; ay/güneş düğmesi sol rayın
  altında. `boxRadius`/yoğunluk standartları iki modda ortak.

**Kanıt:** tsc 0 hata · canlı tur: açık→koyu geçiş, koyu modda Ana/Metrikler/Soru Sor/Uyarılar,
yeniden yüklemede mod korunur, konsol hatası sıfır · Keycloak login canlıda yeni hâliyle ·
E2E nav+sorusor 8/8. **Bilinen sınır:** koyu modda tüm ekranlar tek tek gezilmedi; sabit renkli
köşelerde (grafik/çip tonları) rötuş çıkarsa kullanıcı test turunda kapatılacak.

### S25 — Mockup tasarımı ürüne eksiksiz taşındı *(21 Ağu 2026; kullanıcı: "mockuptaki tüm tasarımı taşı, oradaki her şey eksiksiz olsun")* ✅

- **Global bant:** sayfa başlığı `AppShell`'deki tek `UstBant`'a taşındı (rota→menü eşleşmesi
  en-uzun-önek ile); `PageHeader` yalnız aksiyon şeridi oldu — 19 sayfa props değişmeden geçti.
- **Ana Sayfa** mockup düzenine çekildi (selam başlığı, KPI kartları, hızlı eylemler).
- **Koyu tema rötuşu:** kullanıcının 10 ekran görüntülük bulgu turu → 162 sabit açık renk iki
  Node codemod geçişiyle `--kds-*` çift-değerli CSS token'larına bağlandı; Monaco `vs-dark`;
  bağlantı formunda tarayıcı autofill kapatıldı (Keycloak şifresi DB formuna dolmasın).
- **Kanıt:** tsc 0 · koyu tur ekran görüntüleri temiz · kullanıcı onayıyla commit.

### S26 — Login maskotları "Nöbetçi Botlar" + grafik açılış animasyonları *(21–22 Ağu 2026)* ✅

- Kullanıcının Instagram reel'inden esinlenen animasyonlu login: 3 mockup varyanttan **3. (Nöbetçi
  Botlar)** seçildi → Keycloak `kds` temasına işlendi (`maskot.js` + `kds.css`): yukarıdan düşüş,
  salınım, göz kırpma, imleç/odak takibi, kullanıcı adında bakış, **şifrede gözler yana kaçar**
  (arka dönme yok — kullanıcı düzeltmesi); `prefers-reduced-motion` desteklenir.
- **S26b:** ECharts grafiklerine açılış animasyonu (stagger) + Ana Sayfa çubuk yükselişi + KPI
  belirme; `chartPalette` regresyonu düzeltildi (ECharts tema yolu değil HAM renk ister).

### S27 — Roles & Yetkiler: rol yönetimi + rol-bazlı paylaşım *(22 Ağu 2026; kullanıcı: "çok kritik orası")* ✅

**Mimari karar:** roller **Keycloak realm rolleridir** (kimlik tek doğruluk kaynağı; ayrı rol
tablosu YOK). API, Keycloak Admin API'ye **`kds-yonetim`** confidential client'ı (client_credentials
servis hesabı; realm-management: view-realm/view-users/manage-users/manage-realm) ile çıkar.

- **Backend rol yönetimi:** `IRolYonetimi` + `KeycloakRolYonetimi` (token önbelleği, gizli rol
  süzgeci, kiracı süzgeçli kullanıcı arama `q=tenant_id:...`) + `api/roller` (admin-only):
  listele/oluştur/sil/üyeler/üye ekle-çıkar/kullanıcı ara/durum. Kural retleri **400**
  (sistem rolü silinemez · üyeli rol silinemez · ad çakışması), Keycloak arızası **503**;
  rol adı deseni `^[a-z0-9_-]{2,50}$`. Üye değişikliği **sonraki girişte** token'a yansır.
- **Rol-bazlı görünürlük (S10 devri):** `ReportSharing`'e **Roller** eklendi; rapor+pano
  `PaylasimRolleri`, klasör `ErisimRolleri` (migration `RolPaylasimi`). Liste süzgeci ve tekil
  erişim (`RaporGorunurluk.GorebilirAsync`) AYNI kural: admin | sahip | Ekip | rol kesişimi |
  (özel-değil + klasör rol erişimi). Kesişim bellekte (virgüllü liste SQL'e çevrilemez); Özel
  raporlar DB süzgecinde bile başkasına inmez. Paylaşım Roller değilse liste null'a temizlenir.
- **Frontend:** `RolesPage` (durum kartı; rol listesi sistem rozetli; sekmeler: Üyeler / Rapor-
  Klasör Erişimi (klasör çipleri buradan yönetilir) / RLS kuralları) + `PaylasimSecici`
  (Özel·Ekip·Roller + rol çipleri) Sorgu Tasarımcısı ve Pano düzenleyiciye girdi.
- **Kanıt:** `RolPaylasimTests` 17/17 + rapor/pano süiti 15/15 · canlı duman testi (rol oluştur →
  üye ekle → üyeli sil 400 → üye çıkar → sil; sistem rolü sil 400) · RolesPage açık/koyu ekran
  görüntüleri temiz, konsol hatası 0 · tsc 0. **Not:** kds-yonetim kurulumu `infra/KURULUM.md`'de.

### S28 — Kadi bot yoldaş *(22 Ağu 2026; mockup onaylı — artifact "Kadi")* ✅

Ürünün maskotu/asistan yüzü: PS5 Astro Bot esinli ama **kendi tasarımımız** (beyaz kubbe kafa,
camgöbeği gözlü koyu vizör, gülümseme; login'deki Nöbetçi Bot ailesinin uygulamaya giren üyesi).
Yerel AI taahhüdünün görünür yüzü — tıklanınca **Soru Sor**'a götürür.

- **Çekirdek:** `kadi/KadiYoldas.tsx` (SVG + durum makinesi: ucus/okey/dusun/uzgun/alarm/selam/kos;
  göz takibi, balon, hedefe uçuş, eve dönüş) + `kadi/kadiKanal.ts` (sayfalar CustomEvent ile komut
  yollar; `kadiGorevSahnesi` ortak koreografi) + `kadi/kadi.css` (`--kadi-*` token çifti: açık =
  parlak beyaz, koyu = gece sürümü — eklemler açık kontur). AppShell'e monte.
- **Ekran cümleleri:** sol menünün TAMAMI için giriş cümlesi (en-uzun-önek rota eşleşmesi);
  veri iddiası yok, yalnız yol gösterir. İlk girişte selam (oturumda bir kez, StrictMode korumalı).
- **Sahneler (v1):** Bağlantılar hızlı test — butona uçar, "test ediyorum…", başarıda okey işareti
  + gecikme, hatada üzgün; Soru Sor — model çalışırken anten döner, bitince okey/üzgün.
- **Login (Keycloak):** `kadi.js` + kds.css eki — login akışına ve Nöbetçi Botlara DOKUNMADAN
  Kadi sağ alta eklendi; selam verir, imleci izler, şifrede o da gözünü kaçırır, form gönderilince
  ortaya süzülüp "Seni içeri almasını ilettim…" der. Çalışan tema kopyası eşitlendi.
- **Clippy sigortası:** akışı asla kesmez, balon kendiliğinden söner, "–" ile küçültme
  (localStorage kalıcı), dar ekranda gizli, `prefers-reduced-motion` desteği, MUI modal altında.
- **Kanıt:** tsc 0 · canlı tur — Keycloak login'de Kadi + botlar birlikte, girişte selam,
  bağlantı testi GERÇEK sonuçla (18 ms) sahnelendi, tıklama Soru Sor'a götürdü, koyu temada gece
  sürümü; konsol hatası 0. **Sonraki adım:** kullanıcı ekran ekran gezip cümle/sahne ayarı yapacak
  ("taşırken çıkarır ekleriz").

### S28b — Kadi 3D'ye geçti: gerçek render beden *(23 Ağu 2026; kullanıcı 3D atölyeden 1. modeli seçti)* ✅

**Karar:** SVG beden yerine GERÇEK ZAMANLI 3D (three.js 0.152.2, ~150 KB gzip) — mockup
"Kadi 3D" atölyesindeki onaylı model: parlak beyaz küre kafa (clearcoat plastik + oda yansıması),
cam vizör, ışıklı mavi halka gözler, gülümseme, anten çifti, tombul gövde.

- **`kadi/Kadi3D.tsx`:** şeffaf küçük tuvalde sahne (PMREM ortam, yumuşak gölge, ACES tone map);
  kafa İMLECİ 3 boyutlu takip eder, göz kırpar, nefes alır. Durum pozları: selam=el sallar ·
  düşün=EL ÇENEYE + mor gözler · okey=KOL KALKAR + yeşil gözler + büyük gülüş · üzgün=baş öne,
  gözler kısık (kol jesti YOK — kullanıcı seçimi) · alarm=zıplar + kırmızı gözler (kol jesti YOK) ·
  ucus=öne yatar, bacaklar geride · kos=bacaklar koşar. Tema koyuysa sahne GECEYE döner (kısık
  ışık + arkadan teal jant ışığı + göz parlaması). Sekme gizliyken döngü durur;
  prefers-reduced-motion'da sabit durur; WebGL yoksa `KadiSvg` yedeğine düşer.
- **Login (Keycloak):** three.min.js temaya gömüldü; kadi.js aynı 3D modeli kurar — selamla
  karşılar, kafasıyla izler, ŞİFREDE BAŞINI ÇEVİRİR, girişte ortaya süzülüp uğurlar.
  Login akışına/Nöbetçi Botlara dokunulmadı; WebGL yoksa Kadi görünmez, login etkilenmez.
- **Ekran varsayılan pozları (v1 — kullanıcıyla ayarlanacak):** giriş cümlesine ekran başına
  kısa poz eklendi: Ana/Soru Sor=selam · Metrikler/Tasarımcı/Haritalar/Denetim/Kullanım=düşün ·
  SQL/Zamanlanmış/Roller/RLS=okey · İçe Alım=koş · Uyarılar=alarm.
- **Yükleme perdeleri = Kadi:** LoadingCurtain/RunningIndicator artık Kadi sahnesi — ekran kararır,
  Kadi ORTAYA süzülür, mesajı balonda söyler (yüklemede düşünür, sorgu koşarken koşar); bitince
  evine döner. İç içe yüklemeler sayaçla katlanır; Kadi montajlı değilken (kurulum/kimlik kapısı)
  eski spinner kalır. Mini Kadi konumu düzeltildi (küçük boyuta göre köşeye oturur).
- **Kanıt:** tsc 0 · canlı tur: login 3D + şifre baş çevirme + giriş uğurlaması, uygulamada dock
  + ekran pozları + bağlantı testi sahnesi (gerçek 13 ms) + koyu gece sürümü; konsol hatası 0.

### S28c — Kullanıcı bulgu turu: 5 düzeltme *(23 Ağu 2026)* ✅

1. **Koyu modda grid:** ResultTable başlık/çizgi sabit açık hex'leri tema token'ına çevrildi
   (başlık var(--kds-zemin2/dolgu), tüm çizgiler divider) — koyu zebra artık tutarlı.
2. **Açık modda rapor tür butonları:** görünüm değiştirici, banda göre beyaz stillerle içerik
   alanında görünmezdi — iki modda okunur token stiline geçti (seçili: tealdolgu).
3. **Yükleme perdeleri gerçekten Kadi:** kadiAktif bayrağı aboneli oldu (useSyncExternalStore) —
   sayfaların İLK render'ı Kadi'nin montaj effect'inden önce çizildiğinden perdeler eski
   spinner'a düşüyordu; ayrıca merkez sahnesi uçuş yerine IŞINLANMA (hızlı yüklemede görünmeden
   dönüyordu) + merkezde büyük boy. Yapay asgari süre YOK (kullanıcı kararı).
4. **Ray-bant renk tuhaflığı:** gradyanlar mockup'ın birebir değerlerine döndü — ray LACİVERTTE
   kalır, bant rayın bittiği lacivertten teal'e akar; köşe kaynaşır.
5. **Buton kilitlenirken Kadi sahnede:** abonelik Şimdi Gönder · uyarı Dene · metrik Ölç · içe
   alım Şimdi Çalıştır · bağlantı form testi · rapor dışa aktarma — hepsi kadiYukleniyor sahnesi
   açar (Kadi ortada '…yorum' der, bitince evine döner).

**Kanıt:** tsc 0 · canlı tur ekran görüntüleri: koyu grid, açık tür butonları, iki modda köşe,
Şimdi Gönder'de Kadi ortada, gecikmeli ilk yüklemede Kadi merkez perdesi.

### S28d — İkinci bulgu turu: kaydırıcı beyazlığı (kök neden) + Soru Sor sahnesi *(23 Ağu 2026)* ✅

- **KÖK NEDEN — şeffaf kaydırıcı katmanı:** koyu temada diyalog/grid satırlarının BEYAZ basması
  DOM renklerinden değil, yuvarlatılmış Paper içindeki overflow:auto kaydırıcının GPU bileşik
  katmanının şeffaf zeminde beyaz rasterize edilmesinden geliyordu (ölçümle kanıtlandı: DOM
  şeffaf→koyu, piksel beyaz; zemin verilince düzeldi). Düzeltme: MuiDialogContent-root globalde
  opak panel zemini (--kds-panel çifti) + 14 şeffaf kaydırıcıya background.paper.
- **Kaydırma çubukları:** tüm uygulamada ince, tokenlı çubuk (--kds-kaydirici çifti).
- **Soru Sor:** Kadi artık ORTADA düşünür (kullanıcı isteği); hata gelince üzüntü balon süresi
  kadar tutulur (eskiden 1.9 sn'de silinip 'hiç üzülmedi' görünüyordu).
- **NLQ boş katalog:** model, katalogsuz haritada kolon adını metrik diye uyduruyordu
  (ToplamTutar bulgusu) — bekçi doğru reddediyordu; istem sertleştirildi: katalog boşsa
  'metrikler alanını KULLANMA, toplama ile alanlara koy'. Canlıda doğrulandı: aynı soru artık
  metriksiz haritada alan toplamasıyla cevaplanıyor (35 sn çeviri, 3 satır sonuç).

### S28e — Kadi işlem-dönüşü standardı: her sonuç duyurulur *(23 Ağu 2026; kullanıcı standardı)* ✅

- **Merkezi duyurular (kadiKanal):** kadiBasarili (okey 2.4 sn, varsayılan: Tamamladım — işte
  burada.) ve kadiHata (üzgün 4.5 sn, varsayılan: Maalesef bir sorunla karşılaştım — ayrıntı hata
  mesajında.). Yükleme sahnesi sürerken yapılan duyuru BEKLETİLİR, sahne bitince oynar
  (catch, finally-bitişten önce koşar).
- **Kapsam (codemod, 23 dosya):** 5 hata setter ailesinin (setHata/FormHata/IslemHata/
  YuklemeHatasi/EditorHata) TÜM non-null çağrılarına kadiHata; setBasari/setBilgi/setKayitSonuc
  başarılarına kadiBasarili. Elle: rapor/sorgu koşum başarıları (Raporu tamamladım — sonuç
  burada.), bağlantı form testi sonucu, sessiz kaydet akışlarına Kaydettim.
- **İstisnalar:** Soru Sor hata akışı kendi zengin sahnesinde; uyarı Alert leri ve onay
  diyalogları bilinçli Kadi-DIŞI (pasif durumlar).
- **Süreç kuralı:** bundan sonra her ekran/geliştirmede "Kadi hareketi" değerlendirmesi yapılır
  (ayrıntı: Karar günlüğü 2026-08-23).
- **Kanıt:** tsc 0 · canlıda üçü de görüldü: koşum sonrası okey duyurusu, gerçek bir çalışma
  hatasında anında üzgün duyuru, form testinde diyaloğun üstünde merkez sahne.

### S29 — Kadi asistan paketi: 7 yetenek *(23 Ağu 2026; kullanıcı 7 öneriyi de onayladı)* ✅

Kadi maskottan GERÇEK asistana geçti; hepsi hareket altyapısı üstünde — Kadi artık ekranın her
noktasına uçabilir (isaret: hedefin sağ/sol/alt/üstüne · git: serbest nokta · balon üst bölgede
otomatik ALTA açılır; eylemli balonlar tıklanabilir düğme taşır).

1. **Uyarı nöbeti:** 60 sn'de bir uyarıları yoklar; Sakin→Tetiklendi GEÇİŞİNDE alarm pozu +
   "Eşik aşıldı: {ad}!" + "Uyarılara git" düğmesi. Açılışta eski tetiklenmişler bağırmaz;
   sessiz seviyede bile çalışır (tek istisna).
2. **Boş durum rehberi:** Bağlantılar/Veri Haritaları boşsa Kadi "Yeni …" düğmesinin yanına
   uçup yol gösterir (oturumda bir kez).
3. **Hata yardım köprüsü:** kadiHata artık eylem düğmesi taşıyabilir; SMTP/posta içeren gönderim
   hatasında "E-posta ayarlarına git" köprüsü.
4. **Balondan soru:** Kadi'ye tık → balonda soru kutusu; yazınca Soru Sor'a taşınıp OTOMATİK
   sorulur (navigate state + harita hazır olunca tek sefer).
5. **Fısıltı ipuçları:** 14 ekran için gerçek özellik ipuçları; normalde günde 1, konuşkanda
   ekran başına oturumda 1 (giriş cümlesinden ~6 sn sonra).
6. **İlk kullanım turu:** 5 adım (Bağlantılar → Haritalar → Metrikler → Soru Sor → Uyarılar);
   Kadi her adımda İLGİLİ DÜĞMENİN yanına uçar (data-kadi-hedef), Devam/Bitir balon düğmeleri;
   ilk selamdan sonra bir kez teklif edilir, "Şimdi değil" kalıcı.
7. **Kadi ayarları (Sistem Ayarları):** Kapalı · Sessiz · Normal · Konuşkan — bu tarayıcıya
   özel; Kapalı'da Kadi hiç görünmez ve perdeler klasik spinner'a döner.

**Düzeltilen kök nedenler:** StrictMode tur-teklif zamanlayıcısını öldürüyordu; perdeli
sayfalarda sayfanın yükleme sahnesi rota cümlesini/turu YUTUYORDU → cümle/tur artık sahne
bitene dek ertelenir (S28b'den beri var olan sessiz regresyon da kapandı). Ayrıca kök
tsconfig yalnız references içerdiğinden 'npx tsc --noEmit' HİÇBİR ŞEY derlemiyormuş —
doğrusu: npx tsc --noEmit -p tsconfig.app.json.

**Küçük borç kapanışı (aynı gün):** tur GERÇEK ürün akışına göre 6 adıma çıktı (Panolar eklendi:
bağla → haritala → ölçütle → sor → panola → nöbet); boş durum rehberi Panolar (yalnız yetkiliye)
ve Raporlar'a (hedefsiz — 'Tasarımcıya git' eylem düğmesiyle) yayıldı; öneri balonu 1.4 sn
gecikmeli oldu (ekran giriş cümlesi eziyordu — artık öneri üstüne yazar).

**Kanıt (canlı ekran görüntüleri):** tur teklifi + adım 1 + adım 5'te düğme yanında Kadi;
soru kutusu; Ayarlar bölümü; boş durumda öneri balonu; nöbette "Eşik aşıldı … Uyarılara git";
konuşkanda 💡 ipucu; kapalıda kadi-kutu=0. Gerçek tsc 0.

### S30 — Kuyruk kapanışı: NLQ üçlüsü + Metrik üçlüsü *(23 Ağu 2026 — "kuyrukta kalanları yapalım")* ✅

S22/S23 kapanışlarında bilerek kuyruğa bırakılan 6 madde tek dilimde kapandı.

**Soru Sor (NLQ) üçlüsü:**
1. **Grafik önerisi:** model şemaya eklenen `gorsellestirme` alanıyla görünüm önerir; öneri
   geçersiz/boşsa sunucu modelden deterministik seçer (kova→Çizgi · ölçü+kırılımsız→Sayı Kartı ·
   ölçü→Çubuk · değilse Tablo). Cevap ÖNERİLEN görünümle çizilir (görüntüleyici/panoyla AYNI motor —
   `reportVisual.reportChartOption`), sağ üstte mini tür değiştirici (AI önerisi imli), "Rapor
   olarak kaydet" seçili türle kaydeder. "Nasıl hesaplandı"ya "Önerilen görünüm: X" satırı eklenir.
2. **Çok turlu düzeltme:** cevabın altında "Düzeltme yaz…" alanı — soru sıfırdan sorulmaz,
   `OncekiModelJson` isteme "## Mevcut model … AYNEN koru / ## Düzeltme isteği" bölümü olarak girer.
   **Canlı kalibrasyon dersi (S23 deseninin devamı):** 7b model "ilk 2yi göster" düzeltmesinde
   limit'i uygulamak yerine SİLDİ (1000 satır döndü) — istem kuralı tek başına yetmedi; sunucuda
   deterministik **düzeltme koruması** eklendi: düzeltme cevabında limit/sıralama BOŞ geldiyse
   önceki modelden korunur ve bu "nasıl hesaplandı"ya açıkça yazılır (canlıda doğrulandı: 3 satır +
   iki koruma notu + Çubuk önerisiyle çizim). Bilinçli bedel: "limiti kaldır" isteği korumaya
   takılır — not satırı nedenini söyler.
3. **Sohbet geçmişi:** harita başına son 20 soru localStorage'da (`kds.soruGecmisi.{haritaId}`;
   yalnız o tarayıcı — sunucu kaydı bilerek yok). "Geçmiş" düğmesi → diyalog: soru + tarih + satır +
   önerilen görünüm + çeviri süresi; tık yeniden sorar; "Geçmişi temizle". Düzeltme turları
   geçmişe yazılmaz — "ilk 2yi göster" tek başına yeniden sorulabilir bir soru değil.

**Metrik üçlüsü:**
4. **Metriğe bağlı uyarı:** `Alert.RaporId` null olabilir + `MetrikId` eklendi — uyarı ya bir
   RAPORU ya bir METRİĞİ izler (doğrulama XOR zorlar; metrik yolunda kolon/özet kavramı yok —
   metrik zaten tek değerdir). Koşum `MeasureMetric` ile aynı kırılımsız modeli işin SAHİBİNİN
   kimliğiyle koşar (RLS deseni aynı); kapı metriğin haritasına bakar
   (`ZamanlanmisIsKapisi.DogrulaHaritaAsync`). Ekranda "Rapor izle / Metrik izle" geçişi + koşul
   metni "Metrik 'Toplam Ciro' > 500" dili. Canlı: metrik uyarısı kuruldu, Şimdi Dene gerçek ölçüm
   yaptı (68.997.239.400 · EŞİK AŞILDI · deneme olduğu için e-posta yok).
5. **Metrik sürümleme:** `MetrikSurumleri` tablosu — her güncellemede ÖNCEKİ hâl yazılır
   (satır = eski hâl; Aktif bayrağı bilerek dışarıda — o durumdur, tanım değil). Geçmiş diyaloğu
   sürümleri kim/ne zaman ile listeler; **"Bu sürüme dön" normal kaydetme YOLUNDAN geçer**: formül
   bugünkü haritada yeniden doğrulanır ve dönüşün kendisi de sürüm üretir — geçmiş hiç kaybolmaz.
6. **Metrik içe/dışa aktarma:** dışa = harita ADIYLA taşınabilir JSON (GUID kiracıya özel, ad hedef
   ortamda aynı kurulabilir — test→canlı taşıma); içe = önizleme + çakışma stratejisi (atla /
   güncelle→sürüm geçmişine yazarak), her öğe kendi başına değerlendirilir ve sonuç öğe öğe
   raporlanır (Eklendi/Güncellendi/Atlandı/Hata+neden). OSI/Ossie uyumu v2'de ayrıca değerlendirilir.

**Kadi değerlendirmesi (süreç kuralı gereği):** düzeltmede "Düzeltiyorum — isteğini mevcut sorguya
uyguluyorum" sahnesi + "Düzelttim!"; sürüme dönüşte "Eski tanıma dönüyorum…" + "Döndüm — tanım eski
hâline geldi."; dışa aktarmada "… dosyaya indirildi"; içe alımda sorunluysa kadiHata sayıyla.
Geçmiş diyaloğu için hareket bilinçli olarak yok (salt okuma).

**Kanıt:** migration `MetrikUyarisiVeSurumleme` (uygulandı) · **7 yeni backend testi**
(`MetrikUyarisiVeSurumTests`: XOR + metrik-yolunda-kolon-kuralı-işlemez + metrik uyarısı koşumu
MeasureMetric modeliyle + sürüm yazımı/Aktif-hariç + dönüş-de-sürüm-üretir + ad-ile-çözüm/strateji)
→ hedefli süit 33/33 yeşil · tsc (`-p tsconfig.app.json`) 0 · canlı Playwright turları (S30 ss
klasörü): sürüm geçmişi v1→v3, sürüme dönüş, dışa→içe (Atlandı stratejisi), metrik uyarısı formu +
listede "Metrik: …" + gerçek ölçüm, NLQ cevabı Çubuk önerisiyle + düzeltme korumaları + geçmiş
diyaloğu.

### S30b — Kadi çakışma kaçınması: içeriğin üstünde durmaz *(23 Ağu 2026; kullanıcı kararı)* ✅

**İstek:** *"içerikle çakıştığı an yukarı doğru ya da herhangi içeriğin olmadığı alana kaymasını
sağlayalım"* — S30 turunda bulunan sınırın (Kadi'nin cevap kartındaki "Düzelt" düğmesini örtmesi)
kapanışı.

**Davranış:** Kadi evdeyken 1.3 sn'de bir (ve scroll'da) altını yoklar; gövdesi tıklanabilir bir
öğeye değiyorsa ev sütununda **yukarı** 56px'lik adımlarla, gerekirse bir tık **sola** ilk uygun
yere süzülür (uçuş pozuyla). Alan boşalınca evine döner. Yalnız EV duruşunda çalışır: yükleme
sahnesi, tur adımları, `isaret`/`git` konumları ve soru kutusu asla bozulmaz; **modal açıkken de
durur** (Kadi zaten z-index 1250 ile perdenin altında — kaçmak onu perde kapanınca alakasız bir
yerde bırakırdı).

**İki tercih katmanı:** kritik olan gövdenin **düğme** örtmemesi; ikincil olarak tamamen boş
(yazı/grafik de olmayan) bir yer en yakın düğmesiz yerden ≤3 adım uzaktaysa o seçilir — yoksa Kadi
uzak köşede asılı kalmaz, yakında durur (yazı örtmek, düğme örtmekten iyidir).

**Üç kök neden canlı debug'la bulundu (hepsi yanlış "boş" raporluyordu):**
1. **Nokta örneklemesi kör bant bırakıyordu** — 29px'lik düğme bandı örnek satırlarının arasına
   düşüyordu. Örneklemeyi sıklaştırmak yerine **geometrik kesişime** geçildi (engel dikdörtgenleri).
2. **`elementsFromPoint` pasif düğmeleri hiç döndürmüyor** — MUI disabled düğme `pointer-events:
   none` alır; boş girdideki "Düzelt" hit-test'te görünmezdi. Geometri bundan bağımsızdır.
3. **Ölçü gövdeden alınıyordu** — alarm pozunun zıplaması (±18px) hesabı her tikte kaydırıp
   salınım üretiyordu; ölçü artık kutunun layout kutusundan alınıyor (çocuk transform'u etkilemez).

**Denendi ve GERİ ALINDI:** balon payının (≈160×92) kaçınma alanına eklenmesi — canlı turda 8
ekranda gereksiz kaçış üretti, Kadi her ekranda başka yere konan huzursuz bir şeye dönüştü. Balon
zaten geçici ve `pointer-events:none` (tıklamayı engellemez); gerekçe kodda yazılı.

**Kanıt (canlı, 1440×800):** 18 ekranlık tur — hepsinde Kadi'nin gövdesiyle kesişen tıklanabilir
öğe **sıfır**; yalnız gerçekten gereken 3 ekranda kaçış (SQL Editörü, Bağlantılar, Sistem Ayarları),
kalan 15'inde evinde. Soru Sor senaryosu: Düzelt satırı gelince 632→520 kaçtı ve düğme
**force'suz normal tıklamayla** çalıştı (asıl kanıt); enjekte dev düğme testi 632→352→**632**
(engel kalkınca eve dönüş); diyalog açıkken konum sabit. Sayfa hatası sıfır, tsc 0.

### S30c — NLQ düzeltme yavaşlığı: kök neden ölçümle bulundu *(23 Ağu 2026; kullanıcı: "bu büyük problem")* ✅

**Şikâyet:** ilk soru ~10-50 sn sürerken, hemen ardından gelen **düzeltme** 60-210 sn sürüyordu.

**Ölçüm (Ollama'nın kendi metrikleri — tahmin değil):**

| | istem | **üretim** | süre | limit doğru mu |
|---|---|---|---|---|
| Normal soru | 928 tok | **145 tok** | 24 sn | — |
| Düzeltme (eski) | 1503 tok | **542 tok** | 129-194 sn | ❌ |
| Düzeltme (yeni) | 1133 tok | **79 tok** | 26.6 sn (tekrar 14.2) | ✅ |

**Kök neden — tek ve şaşırtıcı:** düzeltme isteminde modele "mevcut model" diye **iç `QueryModel`**
gösteriliyordu (GUID'li `tabloId`'ler + onlarca `null` alan). Model bu yabancı biçimi **taklit
ediyor**: çıktı 145 → 542 token (3.7 kat) ve CPU'da üretim ~5.8 tok/s olduğu için süre doğrudan
3.7 katına çıkıyordu. Aynı taklit **kalite kaybının da sebebiydi** — çıktı kurtarıcının beklediği
şemada olmadığı için `limit` gibi alanlar kayboluyordu (S30'da "limit korundu" güvencesi bu
semptomu maskeliyordu; artık gerçek neden kapandı).

**Çözüm:** düzeltme isteminde artık modelin **KENDİ ara biçimi** gösteriliyor
(`ModelKurtarici.AraJsonaCevir` — kurtarmanın tersi: dostça tablo/kolon adları, GUID ve null yok).
Model kendi dilinde konuşturulunca hem 6.9 kat az token üretiyor hem doğru alanları güncelliyor.
Ara biçim VEYA'lı/iç içe filtreyi temsil edemez → o durumda istem "filtreler alanını BOŞ bırak"
der ve handler eski filtreyi **korur** (kullanıcının filtresini sessizce düşürmek, yanlış rakam
üretmenin en sinsi yoludur).

**İki ek kazanç (aynı ölçüm turundan):** `keep_alive: 30m` — Ollama varsayılanı 5 dk olduğu için
sorular arasında model bellekten düşüyor ve sonraki soru yükleme bekliyordu (soğuk çeviri 108 sn,
sıcak aynı soru 24 sn). `num_predict: 700` — kaçak üretim sigortası.

**Denendi, faydası yok:** `num_thread` taraması (2/4/6/8 vs varsayılan) — üretim hızı 4.3-6.0 tok/s
arasında, varsayılan (5.8) zaten en iyilerden; fark gürültü. **Donanım tavanı:** i7-1355U (15W, 2
P-core + 8 E-core, GPU yok sayılır — `size_vram: 0`) üzerinde 7B Q4 için ~5.8 tok/s. Bundan sonrası
ancak daha küçük modelle (3B ≈ 2 kat) alınır ve kalite bedeli vardır — kullanıcı kararına açık.

**Kanıt:** 3 yeni backend testi (ara biçime çevirme GUID/null sızdırmaz · handler düzeltmede iç
model DEĞİL ara biçim koyar · gösterilemeyen VEYA filtresi korunur) → NLQ süiti 16/16 yeşil;
canlı uçtan uca: "ilk 2yi göster" düzeltmesi **2 satır** döndürdü, "Limit: ilk 2 satır" ve sıralama
korundu, çeviri **212.8 sn → 41.1 sn**.

### S31 — Soru Sor tezgâh düzeni: konuşma + sonuç tuvali *(23-24 Ağu 2026; kullanıcı mockup "A yönü"nü onayladı)* ✅

**Neden:** ekran tek soru–tek cevaptı — yeni soru sorulunca önceki cevap kayboluyor, düzeltme küçük
bir alana sıkışmış, 15–50 saniyelik bekleme boyunca ekran ne olduğunu anlatmıyordu.

**Süreç:** üç yerleşim yönü mockup'landı (A konuşma+tuval · B tek sütun akış · C defter) + piyasa
taraması; kullanıcı **A**'yı seçti. Mockup: `claude.ai/code/artifact/6dea834f-b852-47be-bf5d-27c66b726d63`.

**Araştırmanın belirleyici bulgusu — "Keyhole Effect"** (arxiv 2602.00947): sohbet arayüzleri veri
analizinde beş mekanizmayla çöküyor; ikisi doğrudan bizi vuruyordu: (1) **içeriğin sürekli yer
değiştirmesi** — önceki grafikler yukarı kayınca kullanıcı çalışma alanının zihinsel haritasını
kuramıyor; (2) **gizli durum değişkenleri** — aktif filtre/limit mesajlara dağılıyor, çalışma
belleği ~4 parça, sonuç "unutulmuş filtre altında yanlış çıkarım". Ayrıca dar sohbet sütunu şikâyeti
ölçülebilir (ChatGPT/Claude için genişletme eklentisi ekosistemi doğmuş).

**Düzen:** solda konuşma (360px, lg altında alta iner), sağda **sonuç tuvali**. Kural:
**grafik ve tablo asla sohbet sütununda çizilmez**; sohbette yalnız tek cümlelik özet + ölçümler
durur. Tuval KALICI: hangi cevap seçiliyse o gösterilir, eski cevaba tıklayınca tuval ona döner.

**Yeni parçalar:**
- **Durum rayı** (`DurumRayi` + backend `SorguDurumu`): harita · metrik · kırılım · hesap · sıralama ·
  limit çipleri tuvalin üstünde sürekli. Kaldırılabilir çipe tıklamak = "bu kısıtı kaldır" düzeltmesi;
  silme modele YAPTIRILIR (modeli elle budamak kurtarıcının doğrulama hattını atlardı). Metrik ve
  hesap çipleri kilitli — onlar sorgunun kendisi.
- **Bekleme kartı**: 4 aşama + belirlenimli çubuk + **"genelde 20–40 sn sürer"** (sahte yüzde değil,
  belirsizlik bandı) + **Durdur**. Dürüstlük kuralı (NN/g uyarısı: adım anlatıları modelin gerçek
  hesabına sadık değil): yalnız GERÇEKTEN bilinen şey tiklenir — "soru gönderildi" (kesin) ve
  "model yazıyor" (o an bekliyoruz); sonraki adımlar SOLUK, iddia değil sıradaki iş.
- **İskelet tuval**: beklerken cevabın şeklini taklit eden gri iskelet (boş beyaz değil).
- **Atıf satırı** (daima açık): tablo · metrik + **tanımın son değişme tarihi** · satır sayısı · RLS.
  Gerekçe: metrik tanımı kayınca cevaplar sessizce bayatlıyor ve hiçbir üründe uyarı çıkmıyor.
- **Kanıt şeridi**: "Nasıl hesaplandı / Üretilen sorgu / Modele giden istem" üç ayrı düğme yerine tek
  katlanır alanda üç sekme; "Büyük göster" ile tam ekran.
- **Takip çipleri** (en fazla 3): cevabın DURUMUNA bakıp üretilir (*İlk 6'ya çıkar · Ters sırala ·
  Aylık kır*) — düzeltme bugüne kadar gizliydi, artık yazı yazmadan tek tık.
- **Tek cümlelik özet**: "3 satır buldum; en yüksek 2.639.332,8 — 724." Tabloyu okumadan cevap.
- **Zengin boş durum**: haritanın adıyla bağlanmış 4 örnek + üç güven kartı (tanımlı metrik · veri
  makineden çıkmaz · kara kutu değil). "Yönlendirmesiz boş kutu bir özellik değil, yarım kalmış iştir."
- **Kadi**: bu ekranda merkez yükleme sahnesi AÇILMAZ (ekranın kendi aşama kartı var; iki ilerleme
  anlatısı fazlaydı) — Kadi köşesinde tepki verir, sohbette avatar olarak görünür, sonucu duyurur.

**Canlı turda bulunan ve düzeltilen kusur:** durum rayı çipinde yalnız küçük **×** ikonu çalışıyordu;
gövdeye tıklayınca hiçbir şey olmuyordu ("tıkladım, bir şey olmadı"). Çipin tamamı tıklanabilir yapıldı.

**Kanıt:** `SorguDurumu` sözleşmesi + `DurumCikar` (kurtarıcıda, `Ozetle` ile aynı bilgi ama
yapılandırılmış) · tsc 0 · backend 0 hata, NLQ süiti 16/16 · **E2E `sorusor.spec` yeni düzene göre
yeniden yazıldı, 5/5 yeşil** (sohbet özeti + tuval + durum rayı + kanıt sekmeleri · takip çipiyle
düzeltme ve eski cevaba dönüş · Ollama-yok kartı) · mock'lu düzen turu (bekleme kartı, iskelet, çip
tıklaması 3. cevabı üretti, dar ekran, koyu tema — sayfa hatası sıfır) · **gerçek Ollama turu**:
`durum` alanı doldu (`kirilim: ServisId`, `hesap: toplam — ToplamTutar`, `siralama ↓`, `limit 3`),
özet cümlesi ve atıf satırı canlıda doğru basıldı (çeviri 39.8 sn · sorgu 5.360 ms).

### S31b — Kadi sahne kusurları: kullanıcı bulgu turu *(24 Ağu 2026)* ✅

**Bulgu 1 — "İçe alımı çalıştırdım, Kadi ortaya geliyor ama 'çalışıyorum' yazması lazım."**
Kök neden: **perde yalnız `LoadingCurtain`/`RunningIndicator` bileşenlerindeydi.** Sayfalar
`kadiYukleniyor`'u DOĞRUDAN çağırdığında (İçe Alım "Şimdi Çalıştır", uyarı denemesi, metrik ölçümü)
Kadi perdesiz kalıyor, balonu beyaz kartların arasında kayboluyordu — tanıda balon metninin aslında
DOĞRU olduğu, ama görsel olarak seçilemediği görüldü. Perde artık **sahnenin kendi işi**
(`kadi-perde`, KadiYoldas çizer): `kadiYukleniyor` her nereden çağrılırsa çağrılsın ekran kararır.
Karartma poza bağlı — `dusun` (sayfa yükleniyor) yoğun, `kos` (işlem sürüyor) hafif, içerik görünür
kalsın. `Loading.tsx`'teki ikinci perde kaldırıldı (çift karartma olurdu).

**Bulgu 2 — "SQL editörü açılırken ekran hafif kararıyor ama Kadi gidiyor."**
Kök neden ölçümle bulundu (100 ms aralıklı izleme): perde açıkken **`merkez: false`** — Kadi merkeze
hiç gelmiyordu. Sayfa geçişinde sıra şuydu: Suspense fallback Kadi'yi merkeze çağırıyor → hemen
ardından KadiYoldas'ın **rota değişimi effect'i** koşup `eveDon()` ile merkezi iptal ediyor. Artık
rota effect'i, yükleme sahnesi sürerken eve dönmüyor.

**Aynı turda çıkan iki yan kusur:**
- **Sahneler arası titreme:** Suspense fallback bitip sayfanın kendi perdesi açılırken Kadi eve
  dönüp hemen geri geliyordu (ölçümde ~100 ms'lik sıçrama). Bitişe kısa bir devir penceresi (140 ms)
  eklendi — yapay uzatma değil, iki sahneyi birleştirme; o pencerede yeni sahne açılırsa Kadi
  merkezde kalır.
- **Poz kaybı:** ekran giriş pozunun 2.2 sn'lik temizleyicisi, o arada başlayan işlem sahnesinin
  pozunu siliyordu (İçe Alım'da Kadi "çalışıyorum" derken koşma pozu düşüp nötr duruyordu).
  Temizleyici artık sahneye dokunmuyor.

**Kanıt:** üç kusur da 100 ms aralıklı canlı izlemeyle ölçüldü ve düzeltme sonrası yeniden ölçüldü —
İçe Alım: `merkez:true · perde:true · durum:kos · "Aktarımı çalıştırıyorum…"`; SQL Editörü:
`merkez:true · perde:true · "Editör yükleniyor…"` ve iki sahne arasında kesintisiz devir.
**E2E 17/17 yeşil** (sorusor + nav + screens), tsc 0. İçe alım turu isteği KESİŞTİRİLEREK yapıldı —
gerçek aktarım koşturulmadı, kullanıcının verisine dokunulmadı.

### S31c — Kadi sahneleri UYGULAMA GENELİNDE organize edildi *(24 Ağu 2026; kullanıcı kuralı)* ✅

**Kullanıcı kuralı:** *"Her bekleme anında Kadi ortada olsun, ne yaptığını söylesin"* + *"tıklama
yapmaması gereken durumlarda arka tarafı loading'deki gibi karartmayı unutma"*.

**Önce tarandı:** `pages/**` + `components/**` (21 sayfa, 14 bileşen) — her async işlem, yükleme
state'i ve ilk-veri effect'i envanterlendi. Sonuç: **28 sahne vardı, 40+ bekleme anı sahnesizdi.**

**Kapatılanlar:** kaydetme (metrik · uyarı · bağlantı · abonelik · içe alım işi · harita · satır
kuralı · sistem ayarları · rapor [tasarımcı + SQL] · pano yerleşimi · pano · rol), silme (metrik ·
uyarı · bağlantı · abonelik · içe alım işi · harita · satır kuralı · pano · klasör), **boş açılan
diyaloglar** (uyarı/abonelik/iş/pano/harita düzenleme, metrik sürüm geçmişi) — bu sonuncusu en can
yakıcıydı: çekmece BOŞ açılıp veri sonradan düşüyordu.

**Soru Sor'da geri alınan karar:** S31'de "ekranın kendi aşama kartı var, iki anlatı fazla" diye
merkez sahnesi kapatılmıştı; kullanıcı bulgusuyla **geri alındı**. Kadi ortaya geliyor, perde
HAFİF (aşama kartı ve "Durdur" okunur kalsın).

**"Renk katalım" (kullanıcı isteği):** cevap gelince Kadi hemen köşesine kaçmıyor — ortada kalıp
*"Sorgu çalıştı, 3 satır geldi. Beraber inceleyelim mi?"* diyor, "Tuvale bak" düğmesi sahneyi
kapatıyor; basılmazsa 6.5 sn sonra kendi dönüyor. (Bu davet önce `finally`'de anında yutuluyordu;
sahne kapatma catch/timeout yollarına taşındı + sayfa terk edilirse unmount koruması kapatıyor.)

**Perde artık iki kipli:**
- **yoğun** (sayfa yükleme, kaydetme, silme): karartma + `pointer-events:auto` → **arka plan
  tıklanamaz**, yarım işin üstüne ikinci iş başlatılamaz; imleç `progress`.
- **hafif** (uzun işlem, kullanıcı izliyor/iptal edebilir): tıklama geçer.
Sahne sürerken Kadi'ye tıklamak da soru kutusunu açmıyor.

**Açılış gecikmesi (180 ms):** iş bundan önce biterse sahne HİÇ açılmaz. Kaydetme istekleri ~100 ms
sürüyor; sahneyi anında açmak Kadi'yi ortaya fırlatıp geri çeken bir "flaş" üretiyordu (canlı
ölçümde görüldü). Devralınan sahnede gecikme yok — Kadi zaten ortada.

**Yan bulgu — ürün YALAN söylüyordu:** `kadiHata()` beş yerde `.catch(...)` zincirinin DIŞINDA
çağrılıyordu (S28e codemod kalıntısı): AdminPage, HomePage, DashboardsPage'de hata olmasa bile her
yüklemede Kadi üzülüp "bir sorunla karşılaştım" diyordu. AuditPage/RawSqlPage/QueryDesignerPage'de
ise `AbortError` filtresinin dışındaydı — sayfa terk edilince ya da koşum iptal edilince de
üzülüyordu. Sekizi de düzeltildi.

**Kanıt:** tsc 0 · **E2E 19/19 yeşil** (sorusor + screens + crud + metrikler + uyarılar — sonuncular
kaydet/sil akışlarını gerçekten koşturuyor) · canlı ölçüm: metrik kaydetme
`merkez:true · perde:yogun · tıklamaKilidi:true`, hızlı GET'lerde sahne açılmıyor (flaş yok),
İçe Alım `merkez:true · perde:true · durum:kos`. Yazma uçları KESİŞTİRİLEREK denendi — kullanıcının
verisi değişmedi.

### S31d — Kadi tam denetimi: 47 madde canlı ölçüldü, 3 kusur kapandı *(24 Ağu 2026)* ✅

**Yöntem:** kullanıcı isteğiyle Kadi, BÜTÜN kurallara karşı ekran ekran sınandı — 20 ekranın giriş
cümlesi/pozu, bekleme sahneleri, perde kipleri, çakışma kaçınması, seviyeler, mini mod, tur, nöbet,
boş durum, balondan soru, duyurular, login, dar ekran. Ölçüm canlı DOM'dan (balon görünürlüğü, poz
özniteliği, perde sınıfı, localStorage) alındı; yazma uçları kesiştirildi — kullanıcı verisine
dokunulmadı.

**Kusur 1 — uyarı nöbeti geçişi SESSİZCE tüketiyordu (en ciddisi).** Nöbet, yeni durumu önce
localStorage'a yazıp sonra "sayfa yükleniyor mu / tur var mı" diye bakıp susuyordu; geçiş bir sayfa
yüklemesine denk gelirse **eşik aşıldığı hâlde kullanıcı hiçbir zaman haber almıyordu**. Artık durum
YALNIZ haber verilebildiyse ilerliyor — bu, sunucudaki `RunAlertHandler`'ın "bildirim borcu
kapanmadıysa Tetiklendi bayrağını ilerletme" kuralının ekran tarafındaki birebir eşi. Canlı kanıt:
sahne sırasında `localStorage={"test-1":"Sakin"}` korundu, sonraki turda *"Eşik aşıldı: Test Eşiği!"*
+ "Uyarılara git" düğmesiyle bağırdı (poz `alarm`).

**Kusur 2 — üç ekranda başarı duyurusu yoktu.** Metrikler, Uyarılar ve Veri Haritaları kaydetmede
Kadi "tamamladım" demiyordu (S28e standardı orada uygulanmamış). Eklendi: *"Metriği kaydettim —
katalogda."*, *"Uyarıyı kurdum — nöbetteyim."*, *"Haritayı kaydettim."*

**Kusur 3 — giriş cümlesi YUTULUYORDU (regresyon, kaynağı S31b).** Denetimde cümlenin DOM'a yazılıp
balonun hiç açılmadığı görüldü. Kök neden: S31b'de eklenen 140 ms'lik sahne devir penceresi. Sayaç
sıfırlanınca `kadiYukleniyorAktif()` false dönüyor, cümle söyleniyor, ardından pencerenin
`kadiSus()` çağrısı balonu kapatıyordu. Düzeltme: **sahne, devir penceresi kapanmadan bitmiş
sayılmaz** (`kadiYukleniyorAktif` artık `bitisZamani`'ni de sorar). Cümle erteleme yoklaması da
300 → 120 ms'ye indirildi (sahne bitiminden ~250 ms sonra konuşuyor).

**Sonuç:** ilk turda 27/36 → düzeltmeler sonrası **35/36**. Kalan tek madde (mini mod düğmesi)
ölçüm artefaktı: hover'sız `force` tıklamayla düşüyor, gerçek hover ile çalıştığı ayrıca kanıtlandı.
Pozlar da düzeldi — cümle yutulmayınca poz onunla geliyor (Metrikler `dusun`, İçe Alım `kos`,
Uyarılar `alarm`, Roller `okey`). **E2E 15/15 yeşil**, tsc 0, JS hatası sıfır.

**Denetimde teyit edilen çalışanlar:** 20/20 ekran giriş cümlesi · login Kadi (nöbetçi botlar
bozulmadan) · bekleme sahnesi + yoğun perdenin arka planı kilitlemesi · çakışma kaçınması (3 ekranda
Kadi'nin altında tıklanabilir öğe sıfır) · boş durum rehberi · balondan soru → Soru Sor'a taşıma ·
hata duyurusu (`uzgun` + "Maalesef bir sorunla karşılaştım") · üç seviye (kapalıda hiç görünmüyor,
sessizde konuşmuyor) · tur teklifi · dar ekranda gizlenme.

### S32 — NLQ hız kararı: model küçültmek yerine ÇIKTIYI küçültmek *(24 Ağu 2026)* ✅

**Soru:** qwen2.5-coder 7B → 3B'ye inmeli miyiz (≈2 kat hız, kalite riski)?

**Cevap: hayır — asıl kaldıraç orada değil.** Ölçüm, üretim hızının CPU'da ~5.8 tok/s ile SABİT
olduğunu, sürenin doğrudan modelin **kaç token yazdığına** bağlandığını gösterdi (S30c'de düzeltme
isteminde aynı kaldıraçla 542→79 token / 7 kat kazanç alınmıştı). Aynı şey normal sorularda da
vardı: model `"metrikler":[]`, `"filtreler":[]`, `"kova":null`, `"limit":null` gibi **kullanmadığı
alanları dolduruyordu** — kurtarıcı bunları zaten iyimser null karşılıyor, yani bilgi kaybı olmadan
atılabilirdi.

**Yapılanlar (üçü de `SoruIstemleri`):**
1. **KISA YAZ kuralı** — "kullanmadığın alanı hiç yazma; boş liste ve null yazma".
2. **ZAMAN KIRILIMI kuralı örnekle** — "2026'da aylık talep sayısı" sorusuna model kova KURMADAN
   1000 ham satır döndürüyordu. S23 dersi tekrarlandı: bu modelde örneksiz kural görmezden geliniyor;
   kurala birebir JSON örneği eklendi. Kova artık kuruluyor (37 satırlık aylık kırılım).
3. **Şema kataloğa göre kuruluyor** — katalog boşken `"metrikler"` satırı ve metrik kuralları
   şemadan TAMAMEN çıkarılıyor. Gerekçe: "KISA YAZ" eklendikten sonra model `ToplamTutar`ı metrik
   sanıp bekçiye takıldı; **iki ayrı yasak cümlesi de durduramadı**. Görmediği alanı yazamaz —
   yasak yerine alanı gizlemek tek işe yarayan yol oldu. (Katalog doluyken alan ve kurallar geri gelir.)

**Kesin kazanç (token sayısı — gürültüsüz ölçüm):** üretim **145 → 67 token (%54 azalma)**; model
çıktısı artık `{"alanlar":[…],"limit":3}` kadar sade. Uçtan uca çeviri ortalaması 55.7 sn → 27-43 sn
bandına indi (tur içi dalgalanma makine yüküne bağlı; token sayısı asıl göstergedir).
Doğruluk 4/5 → **4/5 korundu**, kalan tek başarısızlık model kusuru değil: kova sorgusu 2M satırlık
tabloda 60 sn'lik SQL zaman aşımına takılıyor (ayrı bir performans işi).

**3B karşılaştırması yapılamadı:** `ollama pull qwen2.5-coder:3b` kurumsal ağda
`x509: certificate signed by unknown authority` ile düştü. Sertifika doğrulamasını kapatmak ağ
politikasını delmek olacağı için denenmedi; **kullanıcı ağ sorununu çözünce ölçüm tekrarlanacak.**
Ölçüm sırasında ayar 3B'de kalıp NLQ'yu 503'e düşürmüştü — 7B'ye geri alındı ve "Yerel model hazır"
rozetiyle doğrulandı.

**Kanıt:** 16/16 NLQ testi (biri yeni: şemada `metrikler` alanının katalog boşken YOK, doluyken VAR
olduğunu kilitliyor) · backend 0 hata · 5 senaryoluk uçtan uca ölçüm 3 kez tekrarlandı.

### Devredilen borç → Roles & Yetkiler (madde 4)

**Kullanıcı kararı (18 Ağu 2026):** *"'Ekip' kavramını ekip değil, **şu role sahip kişiler** olarak
yorumlamak gerekiyor. Ben yeni rol açtığımda bu madde rol ve yetkiler alanının altında olsun; orası
tamamlanmadan kapatmayalım."*

S10'un (Rapor kaydetme, ✅ 2026-08-06) kapanışında *"paylaşım Özel/Ekip — **kişi-bazlı sonraki
dilim**"* diye ertelenen iş buraya taşındı. Bugünkü durum: `enum ReportSharing { Ozel, Ekip }` —
rapor ya yalnız sahibinin ya **tüm kiracının**; arası yok. Hedef, paylaşımın **rol** üzerinden
tanımlanması: kullanıcı yeni bir rol açtığında rapor/klasör o role açılabilsin. Böylece "Ekip"
belirsiz bir küme olmaktan çıkıp **rol kümesi** olarak somutlanır.

→ **Roles & Yetkiler maddesi bu iş yapılmadan kapatılmaz.**

**KAPANDI (S27, 22 Ağu 2026):** paylaşım artık rol üzerinden tanımlanıyor (`ReportSharing.Roller`
+ rapor/pano `PaylasimRolleri` + klasör `ErisimRolleri`) — ayrıntı yukarıdaki S27 bölümünde.

### Kapatılan borç → Zamanlanmış Raporlar (S15)

S15 satırındaki *"pano aboneliği + gerçek SMTP canlı doğrulaması sonraki dilim"* notunun iki
parçasından biri **kullanıcı kararıyla iptal edildi** (18 Ağu 2026): *"Zaten sadece rapora abone
olsun, panoya olmasın — gerekirse ayrı ayrı mail atılır."* Yani **pano aboneliği kapsam dışıdır**,
borç değildir.

**SMTP canlı doğrulaması ✅ (18 Ağu 2026).** Docker'da gerçek bir SMTP sunucusu (`rnwood/smtp4dev`,
2525/SMTP + 5099/web) kaldırıldı; Sistem Ayarları → E-posta oradan gösterildi ve abonelik
"Şimdi Gönder" ile koşuldu. **Kanıt:** posta kutusuna `Rapor: SMTP canli dogrulama` düştü —
gönderen `rapor@kds.local`, **iki alıcı**, **1 ek** (`SMTP canli dogrulama.pdf`); Xlsx biçimiyle de
tekrarlandı. Böylece rapor→export→SMTP→teslim zinciri gerçek protokole karşı kanıtlandı (daha önce
yalnız günlüğe yazan gönderici sınanmıştı). Kod değişikliği gerekmedi: SMTP bilgisi her gönderimde
Sistem Ayarları'ndan okunuyor, parola `Sir` tipinde şifreli.

**Açık kalan: dış sunucuya gönderim bu makineden denenemedi + bir ÜRÜN sınırı bulundu.** Gmail ile
gerçek gönderim denendi ve başarısız oldu; iki bağımsız sebep ölçülerek ayrıştırıldı:

1. **Ağ:** bu makineden `25`, `587`, `2525` portları kapalı (Gmail'de ve Office365'te de) — yalnız
   **465 açık**. Klasik spam-koruma engellemesi; KDS ile ilgisi yok.
2. **Ürün sınırı:** 465 **örtük TLS** ister. `EpostaGonderici` .NET'in `SmtpClient`'ını kullanıyor
   ve o yalnız **STARTTLS** konuşur. Ölçüm: 465'e TCP bağlanıyor, istemci düz metin karşılama
   bekliyor, sunucu hemen TLS istediği için bağlantı kapanıyor →
   `IOException: Unable to read data from the transport connection`.

**Sonuç:** yalnız 465'e izin veren bir müşteri ağında KDS e-posta gönderemiyordu.

**✅ ÇÖZÜLDÜ — MailKit'e geçildi** (kullanıcı kararı 18 Ağu 2026: *"MailKit'e geçelim o zaman"*).
`EpostaGonderici` artık **MailKit 4.17** (MIT) kullanıyor; `System.Net.Mail.SmtpClient` ve onun
`SYSLIB0014` obsolete bastırması kaldırıldı. Şifreleme kipi **porttan** türetiliyor — kullanıcıya
ayrı bir "TLS kipi" ayarı sorulmuyor, çünkü yanlış eşleşme tam da bu sessiz başarısızlığın
kaynağıydı: SSL kapalı → şifresiz · **465 → örtük TLS (SslOnConnect)** · diğer portlar → STARTTLS.
4 birim test bu seçimi kilitliyor (`EpostaTlsKipiTests`).

**Kanıt:** (a) yerel SMTP sunucusuna PDF ekli teslim — MailKit'le de çalışıyor, gerileme yok;
(b) **Gmail 465'te TLS el sıkışması artık TAMAMLANIYOR** — bağlantı Gmail'in kimlik doğrulama
aşamasına ulaşıp `535 5.7.8 Username and Password not accepted` döndü. Yani taşıma katmanı sınırı
kalktı; kalan tek şey geçerli bir **Gmail uygulama parolası** (2 adımlı doğrulama gerektirir).
Hata mesajı da artık teşhis edilebilir — eskiden sebebi söylemeyen "Failure sending mail." geliyordu.

**✅ Gerçek teslim kanıtlandı (19 Ağu 2026):** Gmail uygulama parolasıyla abonelik gönderimi
kullanıcının kutusuna PDF ekiyle ulaştı. Böylece zincirin tamamı (rapor → export → SMTP → gerçek
posta kutusu) canlıda doğrulanmış oldu.

**Aynı turda düzeltilen iki mesaj kusuru — ikisi de "ürün gerçeği söylemiyordu" sınıfından:**

1. **Başarı mesajı varsayıma dayanıyordu.** Ekran sabit olarak *"Dev'de SMTP yok → günlüğe yazıldı"*
   diyordu; SMTP kurulup posta gerçekten gidince **yalan söylemeye başladı**. Artık gerçekten
   teslim edilip edilmediğini sunucu bildiriyor (`IEpostaGonderici` → `bool`,
   `SubscriptionRunResult.EpostaGonderildi`) ve ekran iki ayrı metin gösteriyor; gönderilmediğinde
   suçlamak yerine **nereye gidileceğini** söylüyor (Sistem Ayarları → E-posta).
2. **Hata mesajı sebebi gizliyordu.** SMTP hataları bilinmeyen istisna sayılıp ekrana *"An error
   occurred while processing your request."* diye çıkıyordu; oysa sunucu *"Application-specific
   password required"* diyordu. SMTP hataları artık göndericide `EpostaGonderimHatasi`'na sarılıp
   **ne yapılacağını + sunucunun ham yanıtını** taşıyor (kimlik reddi → uygulama parolası yolu;
   bağlantı reddi → "birçok ağ 587'yi kapatır, 465'i deneyin"; protokol kopması → port/TLS
   eşleşmesi). Çeviri saf metotta, 3 test kilitliyor.

**Doğrulama sırasında bulunan ve düzeltilen kusur:** ilk gönderim denemesi **500** verdi ama
aboneliğin `SonDurum` alanı **boş kaldı** — yani hata hiçbir yere yazılmadı. Kök neden: iş
handler'larının `catch` bloğu, hata durumunu **isteğin kendi** `CancellationToken`'ıyla
kaydediyordu. İş iptal/zaman aşımıyla düştüğünde o token zaten iptal olduğu için kayıt da iptal
oluyor, iş "hiç koşmamış" görünüyordu — **en çok kaydedilmesi gereken başarısızlıklar tam da
kaydedilmeyenlerdi**. `RunSubscriptionHandler` ve `RunIngestJobHandler`'da durum yazımı
`CancellationToken.None`'a çevrildi (build 0 uyarı/0 hata, ilgili 6 test yeşil).

**Gizemin çözümü:** düzeltmeden sonra aynı senaryo tekrar edince hata açıkça göründü — rapor
**soğuk önbellekte 60 sn'lik sorgu sınırını aşıyordu** (`400 · "Sorgu zaman aşımına uğradı"`).
İlk denemedeki 500 buydu; iptal edilen token yüzünden hiçbir yere yazılamadığı için gizemli
kalmıştı. Yani düzeltme, kendisini bulan hatayı da görünür kıldı.

### S33 — Kalan borçlar kapatıldı: depo AUTO_CLOSE, pano toplu sahne, kurulum perdesi *(24 Ağu 2026; kullanıcı kuralı: "borçlar kapanmadan yeni menüye geçmeyeceğiz")* ✅

Kullanıcı talimatı: **3B model karşılaştırması hariç** (ağ/sertifika engeli sürüyor) kalan üç borç
kapatıldı.

**(a) Rapor sorgusu zaman aşımı — kök neden sanıldığı yerde DEĞİLDİ.**
İlk şüphe indekssizlikti: içe alım tabloyu HEAP bırakıyordu ve 2M satırlık `talep.Talepler`
üzerinde aylık kırılım soğuk ölçümde **13.2 sn** sürüyordu. İndeks eklenince 0.33 sn'ye indi —
ama bu **adil olmayan bir kıyastı**: ilk ölçüm soğuk diskti, ikincisi sıcak önbellekti. Nitekim
tarih indeksi HİÇ olmayan `KdsDemo`'da aynı sorgu 97 ms sürüyordu; yani belirleyici olan indeks
değil, verinin bellekte olup olmamasıydı.

Gerçek kök neden ölçümle bulundu: `SELECT` CPU'da yalnız **62 ms** harcarken duvar saatinde
**9442 ms** geçiyordu — aradaki fark saf disk beklemesi. Sebep: **LocalDB'de `CREATE DATABASE`
ile doğan depo `AUTO_CLOSE=ON` gelir.** Raporlama deposu kısa ömürlü bağlantılarla çalışır; son
bağlantı kapanınca veritabanı da kapanıyor ve önbelleği atılıyordu, böylece **her rapor 2M satırı
yeniden diskten** okuyordu. `SET AUTO_CLOSE OFF` sonrası ayrı bağlantılarla üç ardışık koşum
**140 / 125 / 121 ms** (aynı SQL, aynı veritabanı — bu kıyas adil).

- `SqlIngestRunner.DepoyuYaratAsync`: `CREATE DATABASE` ardından `ALTER DATABASE … SET AUTO_CLOSE
  OFF WITH NO_WAIT`. Yalnız **KDS'in kendi yarattığı** depoya uygulanır; M22b'de kullanıcının
  seçtiği var olan veritabanının ayarına dokunulmaz (bizim tasarrufumuz değil).
- Yükleme **bittikten sonra** indeks kurulur (`IndeksDdlUret`): tarih kolonları + `*Id` biçimli
  sayısal/GUID anahtarlar, tablo başına en fazla 12. Sıra önemli — indeks bulk copy'den önce
  kurulsa yükleme yavaşlardı. İndeksin katkısı ölçüldü ve **mütevazı** (sıcak önbellekte
  154 ms → 125 ms); asıl kazanç AUTO_CLOSE'dur. İndeks kurulamazsa içe alım DÜŞMEZ (veri sağlam,
  yalnız sorgu yavaş kalır).
- **API üzerinden canlı doğrulama:** aynı soru (`2026'da aylık talep sayısı`) — sorgu süresi
  **87.8 sn → 0.88 sn**, 60 sn'lik sınırın çok altında. (Aynı isteğin kalan 37.5 sn'si model
  çevirisidir — o ayrı borç, aşağıya bakınız.)

**(b) Pano kartları: kart başına sahne yerine tek ilerleme sayacı.** Kartlar bağımsız koşmaya
devam eder (ızgara bloklanmaz) ama Kadi tarafında `panoSahnesi` tek sahne açar ve kartlar
bitirdikçe cümle ilerler: *"Panoyu çalıştırıyorum… 3/7"*. Açılış bir tick ertelenir — kartlar aynı
render'da mount olduğu için ilk katılımda toplam henüz 1'dir; tick sonunda gerçek toplamla açılır.
Hepsi tick içinde biterse (önbellekten dönen kartlar) sahne hiç açılmaz. **6 birim testi** bu
davranışı kilitler; `pano-filtre` E2E'si 3/3 yeşil.

**(c) Kurulum sihirbazı perdesi.** Kurulum ekranında Kadi montajlı değildir (uygulama kabuğu henüz
yok), bu yüzden `kadiYukleniyor` sessizce boşa düşüyordu. Uzun süren "kur" adımı artık
`LoadingCurtain` ile perde indiriyor (Kadi yoksa kendi klasik perdesini çizer) — form bu sırada
tıklanamaz. Kısa testlerde (bağlantı/kimlik) perde yok, buton içi spinner yeterli.

**Kanıt:** backend 375 test yeşil, frontend `panoSahnesi` 6/6, `pano-filtre` E2E 3/3, tip denetimi
ve `dotnet build` 0 uyarı/0 hata.

### S33b — 3B karşılaştırması yapıldı (3B REDDEDİLDİ) + Ollama↔SQL bellek yarışı bulundu *(24 Ağu 2026)* ✅/⚠

Ağ engeli kalktı, `qwen2.5-coder:3b` indirildi (1.9 GB) ve iki model aynı test setiyle (4 yetenek
sorusu + 1 çok turlu düzeltme) karşılaştırıldı.

| Model | Doğruluk | Ort. toplam |
|---|---|---|
| qwen2.5-coder:7b | **3/5** | 181.6 sn |
| qwen2.5-coder:3b | **0/4** | 33.7 sn |

**Karar: 3B reddedildi.** 5 kat hızlı ama üretilen modellerin hiçbiri çalışmadı ve hatalar
içeriksel: `'ToplamTutar'` yine **metrik sanıldı** (S32'de 7B için kapatılan delik 3B'de geri
açıldı), var olmayan `'İçerir'` filtresi kuruldu, "Model bilinmeyen alan" ve aritmetik taşma.
Bunlar ortam/gürültü değil kalite hatalarıdır; S32'nin "model küçültmek yerine ÇIKTIYI küçültmek"
kararı ölçümle doğrulanmış oldu.

**Asıl bulgu — raporları yavaşlatan ikinci kök neden: Ollama ile SQL Server aynı makinede belleği
paylaşamıyor.** Karşılaştırma turundaki `503 Yerel modele ulaşılamadı` ve `400 Sorgu zaman aşımına
uğradı` hataları buradan geliyordu. A/B ölçümü (aynı SQL, aynı veritabanı, AUTO_CLOSE zaten
kapalı):

| Koşul | CPU | Geçen süre |
|---|---|---|
| llama-server bellekte (4.6 GB; boş RAM 1.5 GB) | 141 ms | **24 230 ms** |
| model boşaltıldı — 1. koşum (önbellek yeniden doluyor) | 141 ms | 5 327 ms |
| model boşaltıldı — 2. koşum | 125 ms | **139 ms** |

CPU her üç koşulda da ~130 ms: iş aynı, fark tamamen disk beklemesi. Yani **7B modeli belleğe
girince SQL Server'ın buffer pool'u eziliyor ve her rapor 2M satırı yeniden diskten okuyor
(≈174 kat).** Bu, S32'de konan `keep_alive="30m"` kararının yan etkisidir: model 30 dakika
bellekte kalır → o 30 dakika boyunca TÜM raporlar/panolar yavaşlar. Ödünleşim gerçek — keep_alive
kısaltılırsa bu kez her soru 108 sn'lik soğuk model yüklemesi öder.

Modelin kendi hızı normaldir ve suçlu değildir: saf `api/generate` ölçümü **5.8–6.5 tok/s**
(S32'deki değerle aynı), istem işleme 0.3–1.3 sn. Uzun süreler iki yerden geliyordu — Ollama'nın
istek kuyruğu (aynı istek kuyruk doluyken 76.6 sn, boşken **1.3 sn**) ve yukarıdaki bellek yarışı.

**Bellek yarışı — çözüldü (aynı gün, S34'ün ilk adımı).** Kullanıcının kısıtı nettir: ikinci makine
yok, Ollama bu makinede kalacak. Kök neden ölçüldü: LocalDB'de `min server memory = 16 MB`, yani
SQL Server bellek baskısı altında önbelleğini **tamamen** bırakabiliyor. 2 GB garanti (`min server
memory 2048`, `max 4096`) verilince model bellekteyken bile sorgu **85 / 86 / 99 ms** (öncesi
24 230 ms — **270 kat**, aynı SQL, aynı veritabanı, ardışık ayrı bağlantılar). Ollama'yı makineden
çıkarmaya gerek kalmadı: o adımın vaat ettiği kazancın tamamını bu ayar veriyor.

⚠ Bu bir **ortam ayarıdır, ürün kodu değil** — yeni kurulumlarda tekrarlanmalı; kurulum
dokümanına yazılacak (açık iş).

### S34 — Soru Sor hız turu: ısıtma (önek önbelleği) *(24 Ağu 2026; kullanıcı: "bunları yapalım, sonuçlara göre karar verelim")* ✅

**Ölçümle başlandı — darboğaz sanılan yerde değildi.** Gerçek KDS istemi doğrudan Ollama'ya
gönderilip aşamalar ayrıştırıldı:

| Aşama | İş | Hız | Soğuk | Önbellekli |
|---|---|---|---|---|
| İstem işleme | 928 token | 19 tok/s | **49,7 sn** | **0,2 sn** |
| Üretim | 125–143 token | 5,25 tok/s | 27,3 sn | 23,7 sn |
| **Toplam** | | | **77,1 sn** | **24,1 sn** |

Yani sürenin **%64'ü istem işlemede**. S32'de çıktıyı küçültmek doğruydu ama yalnız %36'lık dilime
dokunuyordu. Ve Ollama aynı öneki ikinci kez işlemiyor — 250 kat fark.

**Yapılan: ısıtma.** `SoruIstemleri` ikiye ayrıldı — `OnekKur` (şema + kurallar + metrik kataloğu +
tablolar; harita başına DEĞİŞMEZ) ve `Kur` (önek + soru). Yeni uç `POST /api/soru/isit` bu öneki
`num_predict=1` ile modele işletir; ön yüz Soru Sor ekranı açılıp harita seçilince ateşle-unut
çağırır. Kullanıcı sorusunu yazarken önek önbelleğe girer.

**Canlı ölçüm (API üzerinden, uçtan uca):** ısıtma sonrası ilk soru **çeviri 25,2 sn / sorgu 152 ms**
— ısıtmasız aynı yol 77 sn çeviri istiyordu. Bellek ayarıyla birlikte toplam **100–164 sn → ~26 sn**.

Kritik tasarım kuralı ve onu koruyan test: **önek ile istemin başlangıcı bayt bayt aynı olmalı** —
bir karakterlik fark önbelleği ıskalatır ve ürün yine doğru çalışacağı için kimse fark etmez, sadece
yavaşlar. `isitma_oneki_gercek_istemin_BASLANGICIYLA_birebir_ayni` bu sessiz kaybı yakalar; düzeltme
turu için de ayrı test var (düzeltme istemi öneke ekleme yapar ama başlangıcı korur).

**Yapılmayan iki iş — ölçüm sonucu vazgeçildi (gerekçeleriyle):**

- **Katı JSON şeması (`format` → JSON Schema).** Ollama 0.32.15 destekliyor ve denendi: hız aynı
  (5,12 vs 5,25 tok/s), sarmalayıcı kazancı marjinal — ama model şemada gördüğü `kova` alanını
  doldurup **`"kova": "Yil"` uydurdu**, oysa soru kırılım istemiyordu. Bu, S32'de kapatılan
  "gördüğü alanı yazar" tuzağının aynısı ve burada YANLIŞ SONUÇ üretir. Mevcut `format="json"`
  zaten kod bloğu sarmalamasını kesiyor. **Karar: yükseltme yapılmadı.**
- **Şemayı soruya göre daraltmak (K1).** Isıtma ile **çelişiyor**: şema soruya göre değişirse her
  sorunun öneki farklı olur ve önbellek her seferinde ıskalar (soru başına ~16 sn), oysa sabit önek
  + ısıtma ile önek maliyeti 0,2 sn. Sabit şema kazanıyor. Daraltma yalnız önek `num_ctx`'e
  sığmayacak kadar büyük haritalarda gündeme gelir; o durumda ısıtma da harita başına yapılır.

**Kanıt:** backend **378 test** yeşil (+3 ısıtma testi), tip denetimi ve `dotnet build` 0 uyarı/0 hata,
uçtan uca canlı ölçüm yukarıda.

**Sıradaki karar (kullanıcı):** buradan sonra tipik soru ~25 sn ve tamamı model ÜRETİMİ. Daha da
hızlanmak için iki yol var ve ikisi farklı şeyi çözer — **GPU** (hız: 5,25 → 45+ tok/s, çeviri
~3 sn; aylık kira ya da tek seferlik donanım) ve **fine-tuning** (hem hız hem doğruluk; eğitim
verisi Soru Sor kullanımından birikir, tek seferlik ~3–6 $).

**Karar (24 Ağu 2026, temiz ölçümden sonra): ŞİMDİLİK İKİSİ DE YOK.** Altyapı düzeldikten sonra
aynı test seti **5/5 doğruluk / ortalama 31,0 sn** verdi (öncesi 3/5 · 181,6 sn) — yani model
kalitesi sorun değil ve fine-tuning'in doğruluk gerekçesi düştü. GPU ise yalnız hız satın alır;
KDS SaaS'a geçerken sunucu kararının parçası olarak yeniden değerlendirilecek. Kullanıcı kararı:
dış LLM API'si (ücretsiz katmanlar dahil) **kullanılmayacak** — model kendimizin kalır.

### S35 — Kuruluş Ayarları (sol menü madde 5) *(24 Ağu 2026)* ✅

Yer tutucu ekran gerçek ekrana dönüştü: **Yönetim → Kuruluş Ayarları** (yalnız Admin).

**Kapsam kararı — limitler bilinçli olarak DIŞARIDA.** Menü tanımı "tenant adı, plan, limit,
marka" diyordu ama sorgu limitleri (zaman aşımı / varsayılan satır / sert tavan) zaten Sistem
Ayarları'nda tanımlı ve **kiracı bazında ezilebiliyor**. İkinci bir düzenleme yeri açmak, birinin
diğerini sessizce geçersiz kılmasıyla biterdi. Bunun yerine ekran o değerleri **yürürlükteki
hâlleriyle ve kaynak rozetiyle** (bu kuruluşa özel / kurulum geneli / varsayılan) özetler ve
Sistem Ayarları'na bağlar. Kaynak rozetinin dili iki ekranda aynı tutuldu.

**İçerik:** *Kimlik* (kuruluş adı, plan/lisans, kopyalanabilir kiracı kimliği + durum çipi —
Keycloak `tenant_id` eşleşmesi uyarısıyla), *Marka* (logo yükleme + vurgu rengi), *Yürürlükteki
limitler* (salt görüntü).

- **Plan/lisans şimdilik BİLGİ alanıdır, davranış değiştirmez.** Fiyatlandırma modeli (hibrit:
  creator seat + tenant paketi) hâlâ açık bir iş kararı; karar verilmeden kota/özellik kapısı
  kurmak, uydurma bir plan sınıflandırmasını şemaya çakmak olurdu. Ekran bunu kullanıcıya da
  söylüyor ("Şimdilik bilgi amaçlıdır; kota uygulamaz").
- **Marka gerçekten UYGULANIR** (yoksa yarım özellik olurdu): logo uygulama kabuğunda ürün adının
  yerine geçer, vurgu rengi `--kds-marka` değişkenine yazılır. Bunun için ayrı bir dar uç
  (`GET /api/kurulus/marka`) eklendi — **tüm roller** çağırabilir, çünkü kuruluş ayarlarını yalnız
  Admin görse de logoyu herkes görmeli; uç yönetim bilgisi (plan/limit/kiracı kimliği) taşımaz.
- Beyaz-etiket/embed kapsamı buraya alınmadı — o "Gömme & Marka" maddesinde (v2) kalıyor.
- Logo `data:` URI olarak satır içinde saklanır (kolon 400 KB, doğrulama 300.000 karakter ≈ 220 KB
  görsel): ayrı depolama katmanı ve statik dosya sunucusu gerektirmez. Görsel olmayan data URI
  reddedilir.
- Boş bırakılan marka alanı "temizle" sayılır — "logoyu kaldır" için ayrı komut yok.

**Kadi:** kaydetme sahnesi (`kadiYukleniyor`) + başarı/hata duyurusu; rota cümlesi zaten vardı.

**Kanıt:** `Tenant`'a 3 nullable kolon + `KurulusAyarlari` migration (canlı uygulandı), **393 test**
yeşil (+14 kuruluş testi), `screens` E2E 11/11 (yeni ekran listeye eklendi), tip denetimi ve
`dotnet build` 0 uyarı/0 hata. **Canlı doğrulama:** ekran açıldı → ad+plan değişti → kaydedildi →
yenilemede kalıcı → geri alındı; logo yüklendi → kabukta çizildi (`--kds-marka: #B4531A`) → geri
alındı. Konsol hatası yok, kullanıcının kuruluş kaydı test öncesi hâline döndürüldü.

**Tam UI süiti (24 Ağu 2026): 43/44.** Tek kırmızı `abonelik.spec` ve sebebi ÜRÜN DEĞİL ORTAM →
aşağıdaki nota bakınız.

### ⚠ Bilinen ortam kırmızısı — `abonelik.spec` (SMTP kimlik doğrulama) *(24 Ağu 2026)*

**Belirti:** `abonelik: rapor oluştur → abone ol → Şimdi Gönder → başarılı` testi `400` alıyor:

```
535 5.7.8 Username and Password not accepted (Gmail) — BadCredentials
```

**Sebep:** Sistem Ayarları'nda gerçek bir **Gmail SMTP** yapılandırması girili ama parola kabul
edilmiyor. Gmail/Office365 normal hesap parolasını reddeder; hesap ayarlarından üretilen bir
**uygulama parolası** gerekir. Ürünün hata mesajı bunu zaten adım adım söylüyor (S15'te bilerek
böyle yazılmıştı) — yani ürün doğru davranıyor, test de doğru düşüyor: e-posta gerçekten gidemiyor.

**Neden daha önce yeşildi:** SMTP boşken devreye `GunlukEpostaGonderici` giriyordu (dev davranışı,
S15). Gerçek sunucu tanımlanınca boru hattı gerçekten göndermeye çalışıyor.

**Çözüm kullanıcıda:** Google hesabında uygulama parolası üret → Sistem Ayarları → E-posta → SMTP
parolası. Sonrasında bu test yeşile döner ve **S15'in "SMTP canlı doğrulaması" borcu da kapanmış
olur** — yani bu kırmızı, o borcun canlı kanıt anıdır.

**Claude bu parolaya DOKUNMAZ:** sır tipli ayar okunamaz ve üzerine yazılırsa eski değer geri
gelmez; hata senaryosu gerekiyorsa sır olmayan alandan (kullanıcı adı/port) üretilir.

### S36 — Soru Sor kullanıcı bulguları: Kadi'nin yeri + "Üretilen sorgu" JSON basıyordu *(24 Ağu 2026)* ✅

Kullanıcı iki kusur bildirdi; ikisi de canlı ölçümle doğrulanarak kapatıldı.

**(1) Kadi beklerken ekranın ortasındaydı, sonuç gelince orada duruyordu.** İstek: *"Kadi sağdaki
yani sonucun geldiği ekranın ortasında olsun; sonuç gelince sağ alta kayıp mesajını versin."*

- `kadiMerkez`/`kadiYukleniyor` artık **alan** (CSS seçici) alıyor: Kadi ekranın değil, verilen
  alanın ortasına gelir. Alan bulunamazsa ekran ortasına düşer — sahne asla kaybolmaz.
- Soru Sor tuvali `data-kadi-alan="tuval"` ile işaretlendi (hem dolu hem boş tuvali kapsar).
- Sahne bitiricisi **son söz** alabiliyor: `kadiBitir({ mesaj })`. Cümle, temizlikten (`kadiSus`)
  SONRA kurulur — yani Kadi önce evine süzülür, duyuruyu ORADA yapar. Eskiden merkezde duyurup
  sonra kayıyordu; kullanıcının okuduğu balon yerinden oynuyor ve tuvali gövdesiyle kapatıyordu.
- **Canlı ölçüm:** beklerken kadi(1092,299) ↔ tuval merkezi(1092,331) — yatayda sapma **0 px**,
  alanın içinde; sonuç gelince kadi(1488,837) / ekran 1560×940 → sağ alt köşe, `kadi-merkez`
  sınıfı kalkmış, balon: *"Sorgu çalıştı, 37 satır geldi. Sonuç tuvalde…"*.

**(2) "Üretilen sorgu" sekmesi SQL değil JSON gösteriyordu.** Kullanıcı: *"üretilen sql diyor, bu
json dosyası — burada sql sorgusu olması gerekiyor."* Haklı: kanıt şeridinin asıl sorusu **"bana
ne çalıştırıldı"**dır.

- Kanıt şeridi dört sekme oldu: *Nasıl hesaplandı · **Çalışan SQL** · Sorgu modeli · Modele giden
  istem*. SQL, tasarımcının da kullandığı `POST /api/sorgu/sql` ucundan **tembel** çekilir (şerit
  açılıp o sekmeye gelinmeden istek atılmaz) ve kopyalanabilir.
- Model JSON'u kaybolmadı, kendi sekmesine taşındı — "Rapor olarak kaydet" onu taşıdığı için
  şeffaflık değeri sürüyor.
- **Canlı doğrulama:** sekme gerçek SQL basıyor (`SELECT DATEADD(month, DATEDIFF(month, 0,
  [t].[GirisTarihi]), 0) … FROM [talep].[Talepler]`), içinde JSON izi (`"alanlar"`/`"tabloId"`) yok.

**Yan iyileştirme — ısıtma seyrekleştirildi.** S34'te eklenen ısıtma her ekran açılışında istek
atıyordu; Ollama istekleri tek tek işlediği için ekranlar arası gidiş geliş gerçek soruyu kuyruğa
itebiliyordu. Artık harita başına **15 dakikada bir** (model bellekte kalma süresinden kısa);
başarısızlıkta damga düşer, sonraki açılış yeniden dener.

**Kanıt:** frontend 61 birim testi yeşil, `sorusor` E2E **5/5** (SQL sekmesi testi eklendi: SELECT
içerir, `satirLimiti` içermez), tip denetimi temiz. NLQ hattında regresyon yok — aynı soru API
üzerinden 200 / 29,5 sn / 10 satır.

### S37 — Bağımlılık güvenlik borcu kapatıldı *(25 Ağu 2026)* ✅

Roadmap'teki borç kaydı **bayatmış**: react-router advisory'si (GHSA-qwww-vcr4-c8h2) artık
görünmüyor — açık kalan tek şey **nanoid `<3.3.18`** (high, GHSA-2v37-7h3g-55p8: özel üreteçler
`size = 0` iken sonsuz döngüye girebiliyor). Doğrudan bağımlılığımız değil, geçişli:
`vite@8.2.0 → postcss@8.5.25 → nanoid@3.3.17`.

`npm audit fix` yetti — **`package.json` DEĞİŞMEDİ**, yalnız kilit dosyasında 3 satır (nanoid
3.3.17 → 3.3.18, yama sürümü). 2026-08-10'daki dompurify borcunda `--force` monaco'yu düşüreceği
için `overrides` gerekmişti; burada öyle bir kırıcı etki yok.

**Kanıt:** `npm audit` → **0 açık**; tip denetimi temiz; 61 birim testi yeşil; `npm run build`
hatasız (ana chunk 336 KB — 2026-08-11'deki 321 KB notuyla tutarlı, aradaki fark S35/S36'da eklenen
ekranlar); `screens` E2E **11/11**.

*Not (ölçüm dürüstlüğü):* ilk `screens` koşumunda Raporlar ekranı kırmızı düştü ama sebep bağımlılık
değil **yüktü** — o koşum üretim derlemesinin hemen ardından çalıştı ve 4,8 dk sürdü (normali ~55 sn).
İzole koşumda 17,9 sn'de geçti, sistem sakinken tam süit 11/11 yeşil.

### S38 — İlk girişte Kadi'nin yazma kutusu açık gelir *(25 Ağu 2026; kullanıcı isteği)* ✅

Kadi ilk girişte *"Bana tıklarsan sorunu yazabilirsin"* diyordu — davet, kendisi için **bir tıklama
daha** istiyordu. Kullanıcı: *"Kadi de bana tıkla sorunu yazıver diyor, ilk girişte yaz olsun."*

- Oturumun ilk selamında soru kutusu **kendiliğinden açılır** ve odak girdidedir: kullanıcı hiçbir
  şeye tıklamadan yazmaya başlayabilir.
- Tanıtım cümlesi kutunun **başlığına** taşındı (*"Ben Kadi. Ne aramıştın? Sorunu buraya yaz, Soru
  Sor'da çevireyim:"*) — ayrı bir selam balonu açılsaydı iki balon üst üste binerdi.
- **Tur teklifi ertelenir:** kutu hâlâ açıkken teklif gösterilmez, kullanıcı yazarken üstüne ikinci
  balon binmesin. `TUR_ANAHTAR` yazılmadığı için hak yanmaz, sonraki girişte teklif edilir.
- Kutu kapanınca (Vazgeç / Esc / gönderim) tanıtım başlığı da düşer; sonraki açılışlarda kısa
  metin (*"Sorunu yaz…"*) kullanılır.

**Canlı doğrulama (temiz oturum):** kutu tıklamadan açık ✓, odak girdide ✓, başlık tanıtıyor ✓,
8,5 sn sonra görünür balon sayısı **1** (tur teklifi binmedi) ✓, Enter → `/soru-sor` ve kutu kapandı ✓,
aynı oturumda ikinci ziyarette kutu kendiliğinden açılmadı ✓. Tip denetimi temiz.

**Ek düzeltme — merkezleme GÖVDEYE göre yapılır (aynı gün, kullanıcı bulgusu).** S36'da "Kadi
tuvalin ortasında" diye kapatılan iş **eksikti**: Kadi kutusunda balon üstte, gövde altta durur ve
konumlandırma sabit piksel ofsetiyle yapılıyordu — üstelik sahne kurulurken balon HENÜZ açılmamış
oluyordu (önce `kadiMerkez`, sonra `kadiSoyle`). Sonuç: balon ortaya oturuyor, Kadi'nin kendisi
aşağı kayıyordu. Kullanıcı ekran görüntüsüyle bildirdi: *"Kadi rapor ekranının ortasında değil,
oraya sok — AI dönüşünün yerinde dursun."*

*Bu kusuru benim S36 ölçümüm de kaçırdı:* doğrulama `.kadi-kutu` merkezini ölçüyordu ve kutu
gerçekten ortadaydı; gözle görülen kayma **gövdenin kutu içindeki yerinden** geliyordu. Ölçüm artık
`.kadi-gov` merkezini alıyor.

- Ofset gerçek DOM'dan ölçülür (`.kadi-gov` merkezinin kutuya göre yeri), sabit sayı yok.
- Merkez sahnesindeyken balon açılıp kapandıkça / metni değiştikçe **yeniden ortalanır** — kutu
  yüksekliği değişince hiza bozulmasın.
- **Ölçüm:** gövde(1092,331) ↔ tuval merkezi(1092,331) → sapma **X:0 px, Y:0 px**; gövde tamamen
  alanın içinde (gövde 245–417, tuval 135–528). Sonuç gelince yine sağ alt köşe (1488,837).

### S39 — Çok tablolu NLQ: ilişkiler, sessiz yanlış koruması, Id↔Ad onarımı *(25 Ağu 2026; kullanıcı bulgusu)* ✅

Kullanıcı gerçek bir rapor istedi ve NLQ **iki kez** çeviremedi: *"Ankara Etimesgut'taki servislerde
yapılan AUDİ marka, 2015 üstü model araçların talep parçalarındaki toplam tutar."* Hatalar:
`'Markald' değeri MarkaId kolonunun tipine (int) çevrilemedi` ve `'Tutar' kolonu birden çok tabloda
var`.

**Kök neden:** istem modele **yalnız tablo ve kolon adlarını** gösteriyordu; haritadaki **23 ilişki
hiç gitmiyordu**. Model tabloların nasıl bağlandığını bilmediği için marka ADINI doğrudan `MarkaId`
(int) kolonuna yazıyordu. Bu soru 4 tabloyu zincirlemeyi gerektiriyor
(`TalepParcalari → Talepler → Araclar → Markalar`).

**Önemli mimari tespit:** üretici join'leri **zaten kendisi kuruyor** (haritadaki ilişkilerden BFS
ile yol buluyor). Yani modele join yazdırmaya gerek yok — işi doğru **kolonu** seçmek. İlişki listesi
isteme bu yüzden "birleştirmeyi SİSTEM yapar, sen yazma" başlığıyla eklendi.

**Yapılanlar:**
1. **İlişkiler isteme eklendi** + "ADLA süzme" kuralı, yaşanan hata örneklenerek (yanlış:
   `MarkaId = "AUDİ"` / doğru: `Markalar.Ad = "AUDİ"`) ve "aynı adlı kolon birden çok tabloda
   olabilir, tabloyu MUTLAKA yaz" uyarısı.
2. **`İçerir` artık Türkçe harf duyarsız** (`COLLATE Turkish_CI_AI`). Veride "Audi" yazılıyken
   kullanıcı "AUDİ" yazınca varsayılan collation eşleştirmiyor ve sorgu **sessizce 0 satır**
   döndürüyordu. LIKE zaten indeks seek kullanmadığı için ek maliyet yok; eşitlik (`=`) bilerek
   dokunulmadı (orada collation seek'i bozardı).
3. **Sessiz yanlış uyarısı:** model bir `...Id` kolonuna doğrudan SAYI yazdıysa ve o Id'nin işaret
   ettiği tabloda ad kolonu varsa, "Nasıl hesaplandı"ya ⚠ notu düşer. Sorgu engellenmez (kullanıcı
   bilerek Id ile süzüyor olabilir) ama rakam artık "doğrulanmamış" olarak işaretlenir.
4. **Tarih olmayan kolondaki kova düşürülür** (hata yerine not). Model `ModelYili` (smallint) ve
   hatta `Ad` (nvarchar) kolonlarına tarih kovası uygulayıp sorguyu 400'e düşürüyordu.
5. **Id kolonuna yazılan METİN, ad kolonuna taşınır:** `MarkaId = "AUDİ"` → `Markalar.Ad İçerir
   "AUDİ"`. Koşullar dar tutuldu (tek değer, sayı değil, sayısal `...Id`, ilişki var, hedefte metin
   ad kolonu var) — yanlış tabloya taşınan filtre "çalışan ama yanlış" sonuç üretirdi.

**⚠ İstem kuralı TERS TEPTİ — ders tekrar doğrulandı.** "Yıl sayı kolonundaysa kova KOYMA, ör.
ModelYili" diye yazınca model iki ardışık soruda tam da `ModelYili` ve `Ad` kolonlarına kova
uyguladı. S32'de `metrikler` alanında yaşanan tuzağın aynısı: **yasak cümlesi, yasakladığı şeyi
öğretiyor.** Kural istemden çıkarıldı, garanti koda taşındı (madde 4).

**Durum: KISMEN ÇÖZÜLDÜ (dürüst sonuç).** Karmaşık soru artık **çalışıyor** (Etimesgut + ModelYili ≥
2015 filtreleri doğru, join'ler otomatik, 4 satır gerçek veri) ve kova/Id onarımları canlıda
görüldü. Ama **7B model bu ölçekte (18 tablo, 4–5 koşul) kararsız**: aynı soru üç ardışık koşumda
üç farklı yerde takıldı (`MarkaId`'ye metin → `MarkaId = 1` tahmini → `'Tutar'` belirsizliği).
Altyapı doğru; sınır artık modelin kendisi.

**İkinci tur — kalan eksikler (aynı gün):**

6. **Belirsiz kolon adı bağlamdan çözülür.** "Tutar" iki tabloda birden geçiyor ve model bir alanda
   tabloyu yazmayı unutuyordu → "birden çok tabloda var" hatası. Artık modelin AYNI cevapta açıkça
   yazdığı tablolar bağlam sayılır; aday tek kalıyorsa o seçilir. Bağlamda iki aday da varsa hata
   sürer — seçim yine tahmin olurdu ve yanlış tablodan gelen rakam sessiz yanlıştır.
7. **Metin eşitliği "içerir"e çevrilir (yalnız NLQ yolunda).** Model isteme rağmen `Esit` kullanmaya
   devam ediyordu; veride "Audi" yazılıyken kullanıcı "AUDİ" deyince sorgu **200 dönüp toplamı null**
   veriyordu. Tasarımcıda elle kurulan filtreye dokunulmaz (orada tam eşitlik bilinçli seçimdir).

**⚠ Collation seçimi ölçümle düzeltildi.** İlk denemede `Turkish_CI_AI` kullanıldı ve **işe
yaramadı**: Türkçe alfabede İ/i ve I/ı AYRI harf çiftleridir, yani Türkçe collation tam da
istemediğimiz ayrımı KORUR (`'Audi' LIKE '%AUDİ%'` → eşleşmez; hatta `'%AUDI%'` de eşleşmez, çünkü
'I'nın küçüğü 'ı'dır). Doğrusu **`SQL_Latin1_General_CP1_CI_AI`**: noktayı aksan sayar, ayrıca
ş≈s, ğ≈g, ü≈u, ö≈o, ç≈c — Türkçe metin aramasında beklenen davranış budur.

### S40 — Join yolu artık görünür: "hangi tablodan geçtim" raporlanıyor *(26 Ağu 2026)* ✅

S39'da bulunan sessiz yanlış borcu kapatıldı — **yol değiştirilmedi, görünür kılındı** (hangi yolun
doğru olduğu sorunun anlamına bağlıdır; ürünün karar vermesi yeni bir sessiz yanlış üretirdi).

**Bilgi doğru katmandan geliyor.** S39'da kurtarıcıdan uyarmak denenmiş ve gürültülü çıkmıştı;
sebebi yapısaldı: kurtarıcı üreticinin SEÇTİĞİ yolu bilmez, yalnız olasılıkları görür. Artık
üretici kendi seçtiğini söylüyor: `JoinPlani`'nın döndürdüğü yerleşim sırasında olup kullanıcının
seçtiği tablolar arasında OLMAYANLAR = ara tablolar.

- `GeneratedQuery.AraTablolar` → `QueryRunResult.AraTablolar` → `AskQuestionResult.NasilHesaplandi`.
  Zincir boyunca opsiyonel alan, geriye uyumlu.
- NLQ notu: *"Bağlantı için X tablosu üzerinden geçildi — sonuç beklediğiniz değilse soruda hangi
  tablodan gidileceğini belirtin."*
- **Gürültü kontrolü:** yalnız kullanıcının SEÇMEDİĞİ tablolar listelenir. Seçilen tablolar zaten
  doğrudan bağlıysa liste boş kalır ve hiçbir şey söylenmez.
- Birleşim (UNION) dalında raporlanmaz: orada tek bir join planı yok, her alt sorgunun kendi planı
  var; boş bırakmak yanlış birleştirilmiş bir liste sunmaktan doğrudur.

**Kanıt:** 406 test yeşil (+2: ara tablo raporlanır / doğrudan bağlı tablolarda liste boş kalır),
`dotnet build` 0 uyarı/0 hata. **Canlı doğrulama — S39'daki asıl vaka artık kendini söylüyor:**

> *Bağlantı için **Servisler** tablosu üzerinden geçildi — sonuç beklediğiniz değilse soruda hangi
> tablodan gidileceğini belirtin.*

Yani kullanıcı, sonucun neden boş geldiğini (Markalar'a aracın markasından değil servisin bayi
markasından gidildiğini) ekrandan görüyor. İkinci soruda not gürültüsüz kaldı: *"Talepler, Iller
tablosu üzerinden geçildi"* — sorgunun gerçekten geçtiği iki ara tablo.

Aynı koşumda S39'un diğer onarımları da görüldü: *"'AUDİ' bir sayı değil; 'MarkaId' yerine
Markalar · MarkaAdi üzerinden süzüldü"* ve *"⚠ 'MarkaId' için 1 değeri MODELİN TAHMİNİDİR"*.

### S48 — Deneme yayını: KDS internete açıldı (bu makine + tünel, 0 ₺) *(1-6 Eki 2026; kullanıcı kararı — docs/10-yayin-yontemleri.md)* ✅

**Karar:** kullanıcı KDS'yi bağımsız adresli deneme yayınına almak istedi; yöntem karşılaştırması
ve "diğer yollar sonra konuşulacak" kaydı [docs/10-yayin-yontemleri.md](10-yayin-yontemleri.md)'de.
Seçim: bu makine + tünel (şirket makinesi şerhi konuşuldu; tünel yalnız tek yerel portu yayınlar).

**Mimari (infra/yayin/):** internet → tünel → **Caddy :8085** (tek origin) → `/` ön yüz üretim
derlemesi · `/api` → Kds.Api · `/kimlik` → Keycloak (`--http-relative-path`). Ön yüz + kimlik
yolları tek kullanıcılık **basic-auth** arkasında (parola üretilir, repoya girmez) — geliştirme
kullanıcıları internete ÇIPLAK AÇILMAZ; `/api` bilerek dışında (Bearer ile çakışırdı; API'yi
kendi JWT kapısı korur). `yayinla.ps1` zinciri tek komutla kurar/durdurur.

**Tek-origin değişiklikleri:** ön yüz kimlik adresi üretimde `origin/kimlik`'ten türer
(identityConfig — adres derlemeye gömülmez); API'de **`Kimlik:IssuerDeseni`** (yeni, opsiyonel):
adresi oynak tünelde issuer regex'le doğrulanır — imza anahtarı yine tek Keycloak'ın jwks'inden,
audience sabit; boşsa davranış eskisiyle birebir. `Kimlik:MetadataAdresi` (S13) burada ilk kez
gerçek işinde kullanıldı.

**Ağ gerçekleri (bu kurumsal ağda ölçüldü):**
- Cloudflare Tunnel ÇALIŞMAZ: edge portu 7844 hem QUIC/UDP hem http2/TCP kapalı.
- SSH 22 dışarı açık → **localhost.run** seçildi (hesapsız). Ücretsiz tünel kendiliğinden kopup
  ADRES DEĞİŞTİRDİĞİ için **nobetci.ps1**: tüneli yeniden kurar, `GUNCEL-ADRES.txt`'yi ve
  Keycloak redirect'ini tazeler (art arda 3×502 + 30 sn hoşgörü — aceleci yenileme sağlıklı
  tüneli düşürüyordu).
- **ngrok ön-testi GEÇTİ** (443/TLS'ten bağlanıp sunucudan kimlik hatası aldı) — kullanıcı
  ücretsiz hesap/token verince sabit adresli kalıcı iyileştirme hazır.

**Yol boyunca yakalananlar:** kcadm'in stderr bilgi satırı PS 5.1'de betiği kesiyordu (cmd /c ile
yutuldu) · cmd içinden geçen JSON tırnakları `\"` kaçışı istiyor — update SESSİZCE kaydolmuyordu ·
Keycloak HOST jokerini desteklemez (`https://*.lhr.life/*` hiç eşleşmez) → tam adres yazılır ·
lhr `X-Forwarded-Proto` koymuyor → Caddy'de https sabitlendi · dönüşte 1.9MB paket yeniden indiği
için ilk açılış yavaş (ücretsiz tünel gerçeği).

**Kanıt (6 Eki 2026, DIŞARIDAN):** basic-auth kapısı → Keycloak girişi (tünel adresinde /kimlik)
→ callback → uygulama tam açıldı (ekran görüntülü; rol bazlı menü çizildi = token/issuer zinciri
çalıştı). `tsc` + build temiz. Kalan rahatsızlık: ücretsiz tünelin yavaşlığı/oynaklığı — çözümü
ngrok sabit adresi (kullanıcı token'ı bekleniyor) ya da docs/10'daki VPS yolu.

**ngrok eki (9 Eki 2026):** kurum bilgisayarı değişen lhr adresine giremeyince kullanıcı ücretsiz
ngrok hesabı açtı → `yayinla.ps1`'e **ngrok VARSAYILAN tünel** oldu (`-Tunel ngrok|lhr|cloudflared`;
token/alan `.ngrok-token`/`.ngrok-domain` dosyalarında, repo dışı). Adres artık **SABİT**
(`*.ngrok-free.dev`) — kopsa da aynı adrese bağlanır; nöbetçiye gerek yok. İki iş daha gerekti:
(1) redirect tazeleme yalnız lhr nöbetçisindeydi, ngrok'ta Keycloak "Geçersiz parametre:
redirect_uri" verdi → `yayinla.ps1`'e tünel sonrası ORTAK redirect güncelleme adımı (yazıldığı
kcadm'den geri okunarak DOĞRULANIR — sessiz-kaçış dersi); (2) ngrok ücretsiz planın "Visit Site"
ara sayfası fetch'leri kırmasın diye `client.ts` tüm isteklere `ngrok-skip-browser-warning`
başlığı ekler (tarayıcı ilk ziyarette sayfayı BİR KEZ görür, tıklanıp geçilir). Kanıt: dışarıdan
Playwright tam akışı — basic-auth → /kimlik girişi → menü (20 öğe) + "API canlı" çipi + SPA derin
yol, 0 sayfa/API hatası.

### S47 — Metrik formül yardımı + abonelik düzenleme kusuru *(1 Eki 2026; kullanıcı inceleme turu)* ✅

**1) Formül girmek kolaylaştı (kullanıcı önerisi: "örnek versek ya da kolaylaştırsak").**
Metrik çekmecesindeki çıplak formül kutusu üç yardımla donatıldı; elle yazım yolu aynen açık:
- **Şablon çipleri:** Toplam · Ortalama · Sayım · Oran % · Koşullu toplam. Tıklanınca şablon
  yerleşir, ilk `…` boşluğu SEÇİLİ kalır.
- **Alan listesi (tıkla-ekle):** harita seçilince aranabilir tablo/kolon listesi (gizli kolonlar
  görünmez); kolona tıklayınca `{Tablo.Kolon}` o boşluğu doldurur ve SIRADAKİ boşluk seçilir —
  "çip → alan → alan" diye formül KLAVYESİZ kurulur. İmleç/yer-tutucu kuralları saf modülde
  (`metrikFormul.ts`, 7 vitest; canlı kanıt: Oran % + iki tıklama →
  `SUM({Talepler.GirisKm}) * 100.0 / SUM({Talepler.RandevuId})`).
- **Fonksiyon ipucu ("?")**: gruplu liste (Toplama/Sayı/Metin/Tarih/Mantık), örneğe tıklanınca
  formüle eklenir. İçerik backend'de ifade BEKÇİSİNİN yanından gelir (`IfadeYardimi` +
  `GET /api/metrikler/fonksiyonlar`) — ön yüzde kopya liste olsaydı bekçiyle sessizce ayrışır,
  "önerip reddetme" yaşatırdı. İki test kilitler: ipucundaki her ad izinli kümede + her örnek
  denetimden geçer (ikincisi daha yazarken işe yaradı: NULLIF örneğindeki çıplak `x`/`y`yi yakaladı).
- **Kadi değerlendirmesi (kural):** hareket EKLENMEDİ — yardım arayüzün kendisi öğretici;
  balon gürültü olurdu.

**2) Abonelik "Düzenle" boş açılıyordu (kullanıcı bulgusu, ekran görüntülü).** `openEdit` çekmeceyi
hemen açıp detayı SONRA çekiyordu; çekme düşerse (o anda API'nin derleme için kısa durdurulması —
"Failed to fetch") form boş kalıyor ve **Kaydet açıktı: boş formla kayıt, mevcut aboneliği
alıcısız EZERDİ** (asıl tehlike buydu). Düzeltme: form özetten ÖN-dolu açılır (ad/rapor/biçim/
zamanlama/aktif), detay yalnız alıcıları + parametre değerlerini tamamlar; detay inene kadar
Kaydet KİLİTLİ; düşerse açıklayıcı hata + "Tekrar dene". Canlı kanıt: düzenle ön-dolu açıldı,
kaydet çalıştı.

**3) Aynı ekrandaki ikinci bulgu — "abonelik çalışmadı" — kusur değil, S19 güvenlik kapısı:**
kimlik saklanmadan önce kurulmuş abonelik, RLS'li veride sahip kimliği olmadan koşmaz (fail-closed;
sessizce geniş veri göndermek yerine). Banner'ın dediği çözüm (aboneliği açıp bir kez kaydet) tam
da (2) kusuru yüzünden yapılamıyordu; şimdi yapılabiliyor ve doğrulamada `KDSZamanlayiciTest`
kaydedilip kimliği tazelendi. Yol üstünde banner'daki şablon ek hatası da düzeltildi
("aboneliğiyı" → ek kelimeye göre değiştiğinden cümle EKSİZ kuruldu: "İlgili … kaydını açıp").

**Kanıt:** backend 458/458 (2 yeni kilit testi; ilk koşudaki tek kırmızı API ayaktayken ortam
yarışı — ardışık iki koşu temiz), vitest 68/68 (7 yeni), `tsc` temiz; metrik çekmecesi + fonksiyon
ipucu + ön-dolu abonelik düzenleme ekran görüntülü canlı doğrulandı.

### S46 — OSI (Apache Ossie) uyumu: semantik katman satıcıdan bağımsız biçimde dışarı/içeri *(2 Eyl 2026; araştırma kuyruğundan — rakip analizi §5 "kapalı ada" riski)* ✅

**Bağlam:** 2025-2026'da doğan **OSI — Open Semantic Interchange** (Snowflake/dbt/Cube öncülüğü)
Ocak 2026'da v0.1 spec'ini Apache 2.0 ile yayımladı ve ASF kuluçkasına **Apache Ossie** olarak
girdi: metrik/semantik tanımın satıcıdan bağımsız YAML değişim biçimi ("USB-C"). Rakip analizi
kararı: "metrik katmanımıza import/export kapısı — kapalı ada riskinden kaçın". Spec, uygulamadan
önce kaynağından yeniden doğrulandı (bayat-bilgi kuralı): üst yapı `version` +
`semantic_model[]` (datasets/relationships/metrics), metrik ifadeleri LEHÇELİ
(`expression.dialects[]`, ANSI_SQL dahil), satıcı verisi için resmi `custom_extensions
[{vendor_name, data}]` mekanizması.

**Ne kuruldu (S30 transfer altyapısının üstüne standart biçim köprüsü):**
- **Dışa aktarım:** her Veri Haritası bir `semantic_model` öğesi — tablolar `datasets`
  (dostça ad + `source: şema.hamAd`; GİZLİ kolon dosyaya YAZILMAZ — son kullanıcıya kapalı olan
  değişim dosyasına da girmez), ilişkiler `relationships`, metrikler `metrics`. Metrik ifadesi
  **ANSI_SQL lehçesiyle** (Y1 yer tutucuları açılmış hâli — `SUM({T.K})` → `SUM(T.K)`), orijinal
  Y1 + gösterim biçimi **LST_KDS `custom_extensions`** içinde: KDS→KDS round-trip KAYIPSIZ, başka
  ürünler ANSI'yi okur. YAML ELLE üretilir (yalnız yazım; kaçış tek fonksiyonda), İÇE ALIMDA
  **YamlDotNet** (MIT — yeni paket, yalnız Business) ayrıştırır.
- **İçe alım:** modeller harita ADIYLA eşlenir (S30 deseni — GUID kiracıya özel, ad taşınır);
  LST_KDS uzantısı varsa Y1 aynen, yoksa ANSI ifadesi Y1'e çevrilir (`T.K` → `{T.K}`;
  tek-tırnaklı literaller iki yönde de DOKUNULMAZ; üç parçalı `şema.tablo.kolon` çevrilmez —
  sessiz yanlış sarım yerine kayıt anında üreticinin açık hatası). Kayıt normal SaveMetric
  yolundan: formül bugünkü haritada doğrulanır, güncelleme sürüm geçmişine yazılır, sonuç
  S30'un öğe-öğe sözleşmesiyle döner (ekran aynı diyaloğu kullanır).
- **Yol boyunca yakalanan sessiz-yanlış:** ilk sürüm `primary_key`'e haritanın "Anahtar" rolündeki
  kolonları yazıyordu — o rol PK+FK karışımıdır ("join'de kullanılır"); yanlış PK beyanı alıcı
  üründe join/teklik çıkarımını sessizce bozardı → alan HİÇ yazılmıyor (elde olmayan bilgi beyan
  edilmez; ilişkiler zaten `relationships`'te açık).
- **Ekran:** Metrikler'de "Dışa Aktar" yanına **OSI** düğmesi (`kds-semantik.osi.yaml` iner);
  İçe Al artık .yaml da kabul eder — biçim UZANTIDAN değil İÇERİKTEN tanınır (`semantic_model`
  kökü), yanlış adlandırılmış dosya da çalışır.

**Kanıt:** 6 yeni test (dışa aktarım spec yapısı — üretilen YAML gerçek ayrıştırıcıyla okunur ·
KDS→KDS round-trip kayıpsız · uzantısız dosyada ANSI→Y1 · harita-yok/bozuk-YAML/OSI-olmayan dosya
hataları · çakışma stratejisi · ifade dönüşümünün literal/üç-parçalı-ad korunumu), tam süit
**456/456**, `tsc` temiz. **Canlı:** gerçek katalog `GET /api/metrikler/disa-osi` → 23 KB OSI
YAML (2 harita, ilişkiler, 1 metrik ANSI+uzantı) → aynı dosya `ice-osi` ile geri alındı
(`Guncellendi`); Metrikler ekranında OSI düğmesi ekran görüntülü.

### S45 — KDS Ajanı: müşteri ağından dışarı doğru, push tabanlı içe alım *(1 Eyl 2026; araştırma kuyruğundan — 00-analiz §3'ün çizdiği yol)* ✅

**Sorun (satış/kurulum engeli, 00-analiz risk tablosu):** müşteri MSSQL'i şirket ağındadır; SaaS
oradan ona ulaşamaz. v1 "erişilebilir DB" önkoşuluyla yaşıyordu. **Çözüm analizde çizilmişti:**
ajan gelirse içe alım işi müşteri tarafında koşar ve veriyi İTER (Power BI gateway modeli) —
"mimari bugünden buna göre çizilir (senkron işi tek modülde toplanır)". S45 tam bunu yaptı.

**Ne kuruldu:**
- **`Kds.Ajan`** — müşteri makinesine kopyalanan küçük konsol süreci (yalnız Contracts+Execution'a
  referans; Kds.Data/Business bilerek yok). `ajan.json`: KDS adresi + anahtar + bağlantı dizeleri.
  **Kaynak parolaları müşteri ağından ÇIKMAZ** — KDS'deki bağlantı kaydında sunucu/DB bilgi
  amaçlıdır, parola boş; gerçek dize yalnız ajan.json'da.
- **Anahtar: Gömme (S42) deseninin eşi, AYRI amaç dizesiyle** (`Kds.Ajan.Anahtar.v1` — gömme
  anahtarı ajan anahtarı olarak kullanılamaz, tersi de). Anahtarın içinde yalnız kayıt kimliği;
  yetki DB satırında → iptal ANINDA etkili. Üretimde BİR KEZ gösterilir. Tek kimlik kapısı
  `AjanKapisi` (kopyalansaydı bir uçta iptal denetimi unutulurdu); ret ayrımsız 403.
- **Protokol (5 uç, hepsi dışarı doğru):** `isler` (yoklama = kalp atışı; vadesi gelen cron +
  elle tetik) → tablo başına `tablo-basla` → `satirlar`* → `tablo-bitir` → `is-bitir`. Sunucu
  istekler arası bellek durumu TUTMAZ — her istek bağlamını taşır; API yeniden başlasa yarım iş
  "kayıp oturum" değil, bir sonraki vadede baştan koşulan iş olur.
- **S44 artımlı ile birebir bileşim:** su seviyesini sunucu söyler, ajan yalnız o dilimi okur,
  MERGE sunucuda. Değer kodlaması `AjanVeri`'de TEK yerde (binary=base64, decimal=invariant METİN
  — JSON double'ına sığmaz, sessiz yuvarlama olurdu; tarih=ISO). DDL/MERGE üretimi de S44'le ortak
  çekirdeğe çıkarıldı (`DepoYazimi`/`SuSeviyesi`) — iki yazım yolu ayrışamaz.
- **Çifte koşum koruması:** dağıtılan iş "Kosuyor" işaretlenir, SonSenkron ilerletilir, elle tetik
  düşürülür → ikinci yoklama aynı işi vermez. Ajan iş ortasında ölürse durum bir sonraki cron
  vadesinde kendini onarır. Su seviyesi TABLO BAŞINA, verisi gerçekten depoya inince yazılır.
- **Sunucu tarafı yönlendirme:** ajanlı kaynağın işlerini `IceAlimZamanlayici` ATLAR;
  "Şimdi Çalıştır" sunucuda koşmaz, tetiği damgalar ve bunu AÇIKÇA söyler (`AjanaDevredildi` —
  sessiz 0 satır "çalıştı ama boş" diye okunurdu). Kartta "Ajanda koşuyor…" çipi.
- **Ekranlar:** Yönetim → **Ajanlar** (üret/anahtar bir kez/iptal/çevrimiçi çipi — son görülme
  yoklamadan) · Bağlantılar'da İçe Alım moduna "Ajan" seçici (iptalli ajan reddedilir; kolonu
  değişen kayıt doğrulaması `ConnectionAjani`).

**Kanıt:** 7 yeni test yeşil (anahtar yaşam döngüsü · iş dağıtımı yalnız kendi bağlantısına +
çifte dağıtım yok · yabancı ajan reddi · tablo başına seviye · Şimdi Çalıştır devri ·
LocalDB'de tam+artımlı push round-trip ve hedef kayınca TAM'a düşme · değer kodlaması round-trip),
tam süit **450/450**, `tsc` temiz, screens E2E 15/15 (Ajanlar dahil), ingest E2E 3/3.
**CANLI (iki gerçek süreç):** API + `Kds.Ajan` konsolu — ajan üret → ajanlı bağlantı+iş →
"Şimdi Çalıştır" `ajanaDevredildi:true` → ajan yokladı, KdsDemo'dan okudu, depoya İTTİ (6 satır,
1. koşu tam) → ikinci tetik **artımlı koştu** (`artimli 1/1`) → iş kartı Basarili, ajan
"çevrimiçi". Test artıkları silindi, ajan iptal edildi, gerçek anahtar dosyası temizlendi.
*(E2E kararsızlık notu: ingest.spec bir turda 60 sn'lik takılmayla düştü; taze API ile 3/3 —
saatlerdir koşan dev API süreci yavaşlamıştı, kod regresyonu değil; ilk deneme uçtan uca
çalışmıştı zaten.)*

### S44 — Artımlı yükleme: içe alım her koşuda baştan çekmiyor *(27 Ağu 2026; araştırma kuyruğundan)* ✅

İçe alım S12'den beri her koşuda DROP+CREATE+tam yükleme yapıyordu — veri büyüdükçe ilk sıkışacak
yer. Artık tablo başına isteğe bağlı **artımlı kolon** seçilir; koşu yalnız yeni/değişen satırları
taşır.

**Yöntem: su seviyesi (watermark) + staging + MERGE.** CDC bilinçli SEÇİLMEDİ: kaynakta CDC açmak
sysadmin ister, ürün kaynağa salt-okunur kullanıcıyla bağlanır (readonly-user.sql deseni) —
rowversion/Id/tarih kolonunu sorgulamaksa salt-okumayla çalışır.

- **Akış:** kaynaktan `MAX(kolon)` KOPYADAN ÖNCE okunur (hedef kaçarsa pencere hiç kapanmaz) →
  `eski ≤ kolon ≤ yeni` dilimi staging tablosuna bulk-copy → kaynak PK'sı üzerinden hedefe MERGE.
  Alt sınır bilerek `>=`: eşit değerli satırlar (aynı tarihe düşen geç kayıtlar) tekrar kopyalanır
  ama MERGE PK ile tekilleştirir; `>` olsaydı o satırlar SESSİZCE kaybolurdu.
- **Kolon tipleri:** rowversion (güncellemeleri DE yakalar — her yazımda artar) · artan Id · tarih.
  Seviye metin olarak işin satırında saklanır (`ArtimliDurumJson`; zamanlama durumuyla aynı ilke —
  ayrı depo yok), kolon tipine göre kayıpsız çözülür (hex / invariant sayı / ISO tarih).
- **Tam yüklemeye kendiliğinden dönüş** (sessiz değil — sonuçta `ArtimliTabloSayisi` olarak
  görünür): ilk koşu · hedef tablo yok · kolon adları kaymış (şema kayması) · kaynakta PK yok ·
  saklı seviye çözülemiyor. Yalnız TİP değişimi ada dokunmaz; o durum koşu hatası olarak görünür —
  sessizce yanlış tipe dökmekten iyidir.
- **Silinen satırlar artımlıda taşınmaz** (belgelenen sınır) → karttaki **"Tam Yükle"** düğmesi
  (onaylı) seviyeleri sıfırlar ve baştan çeker.
- **Sessiz yanlış koruması (kayıtta):** artımlı KOLONU değişen tablonun su seviyesi kaydetme
  anında düşürülür — eski kolonun değeriyle yeni kolonu karşılaştırmak satır atlatırdı; kolonu
  aynı kalan tablo seviyesini korur. Seviye yalnız BAŞARILI koşuda ilerletilir: yarıda kalan
  koşunun seviyesini yazmak, kopyalanamayan dilimi bir daha hiç istememek olurdu.
- **Ekran:** İçe Alım editöründe tablo başına "Artımlı kolon" seçici (yalnız NULL olmayan
  rowversion/Id/tarih kolonları; PK'sız tabloda açıklamayla kapalı), kartta "Artımlı: N/M" rozeti,
  koşu sonrası Kadi "N/M tablo artımlı" der. Doğrulama: artımlı kolon seçili kolonların dışındaysa
  kayıt reddedilir (koşucu sessizce tam yüklemeye düşerdi; kayıt anında açık söylenir).
- **Zamanlayıcı değişmedi** — aynı handler'ı çağırıyor; cron'lu işler kendiliğinden artımlı koşar.

**Ölçülen kazanç (canlı, gerçek KdsDemo → depo):** `talep.TalepParcalari` (2.000.000 satır) —
tam yükleme **118 sn**, değişiklik yokken artımlı koşu **1.3 sn** (~90×). Zamanlanmış gecelik
senkronların maliyeti artık veri boyutuyla değil DEĞİŞİMLE ölçeklenir.

**Kanıt:** 4 GERÇEK LocalDB entegrasyon testi (`ArtimliYuklemeTests` — SqlBulkCopy/MERGE SQLite'ta
yok): rowversion ile ekleme+güncelleme taşınır ve değişiklik yokken satır sayısı sabit kalır ·
Id ile yeni satır gelir, silinen KALIR ve Tam Yükle temizler · şema kayması tam yüklemeye düşer ve
yeni kolon hedefe gelir · PK'sız tablo hep tam. +3 handler testi (kayıt round-trip · kolon
değişiminde seviye düşer/aynı kalırsa korunur · doğrulama reddi). Tam süit **446/446** (1 atlanan
öteden beri). Canlı akış API'den doğrulandı: 1. koşu tam (`artimli=0`) → 2. koşu artımlı
(`artimli=1`) → `tamYukleme=true` sonrası yine tam; editörde artımlı kolon seçicisi ekran
görüntülü. Bulunan ürün kusuru: SaveIngestJobHandler özet dönüşü artımlı sayısını doldurmuyordu
(test yakaladı).

### S43 — Ana sayfayı kullanıcı tasarlar + üst bant tema ayrımı + giriş perdesinde Kadi *(27 Ağu 2026)* ✅

Kullanıcının üç bulgusu tek dilimde kapandı.

**1) Ana sayfa hâlâ "temp" duruyordu.** Eski Ana Sayfa'nın KPI kartları, aylık çubuk grafiği,
servis tablosu ve nöbet listesi **temsilî sabitlerdi** (dosyanın kendi notu: *"Sayılar temsilîdir;
gerçek içerik pano/rapor dilimlerinde bağlanacak"*). Hepsi kaldırıldı.

**Seçilen yol (kullanıcı onaylı):** ayrı bir ana-sayfa tasarım motoru YAPILMADI. Kullanıcı panosunu
Panolar'da tasarlar, birini **"ana sayfam"** işaretler, ana sayfa onu çizer. İkinci bir sürükle-bırak
düzenleyici, panonunkiyle sürekli ayrışacak ikinci bir bakım yükü demekti; reddedildi.

- **Saklama:** `User.AnaSayfaPanoId` (nullable kolon, migration `AnaSayfaPanosu`). Favoriler ayrı
  tabloda çünkü **çoklu**; ana sayfa **tekil** → tekilliği kolonun kendisi garanti eder, ayrı tablo
  + toggle handler'ı gereksiz hareketli parça olurdu. Panoya **FK YOK**: pano silinirse kayıt
  bozulmaz, okuma anında görünürlük denetlenip sessizce karşılama görünümüne düşülür.
- **Güvenlik:** görünmeyen pano ana sayfa YAPILAMAZ — aksi hâlde kullanıcı başkasının özel panosunu
  ana sayfasına yazıp varlığını öğrenirdi. Seçim yapıldıktan SONRA paylaşım daralırsa okuma null
  döner ama **kayıt silinmez** (paylaşım geri açılınca tercih yerinde durur).
- **Rol:** uç OKUMA tarafında, favori ucundaki gibi rol kısıtsız — pano *oluşturamayan* `musteri`
  de gördüğü bir panoyu kendi açılışı yapabilir.
- **Yol boyunca kapatılan iki şey:** (a) pano görünürlük kuralı liste ve tekil handler'da
  **kopyalıydı**; "ana sayfam" üçüncü kopyayı doğuracaktı → `PanoGorunurluk`'ta tek yere alındı
  (aynı tuzak için rapor tarafında zaten yazılı uyarı var). (b) Pano gövdesi `DashboardViewPage`
  içine gömülüydü → `usePanoVerisi` (durum) + `PanoGovdesi` (çizim) ikilisine ayrıldı; görüntüleyici
  ve ana sayfa artık aynı ızgarayı, aynı filtre davranışını paylaşıyor.
- **Bulunan kusur:** uç seçim yokken gövdesiz **204** döndürüyordu, ön yüzdeki JSON ayrıştırıcı boş
  gövdede patlıyordu → sarmalayıcı kayıt (`HomeDashboardInfo`) her hâlde gövde garantiler.
- **Yan bulgu (düzeltildi):** pano görüntüleyicideki geri oku `color:'#fff'` idi; şerit S25'te
  renkli banttan nötr zemine taşındığında bu kalmış ve **açık temada görünmez** olmuştu.

**2) Üst bant açık/koyu temada değişmiyordu.** Ölçüm bulguyu doğruladı: açık `#0f2a43→#12958a`,
koyu `#0b1c2c→#0d7a70` — ikisi de koyu lacivert→teal, fark ölçülebiliyor ama gözle seçilmiyordu.
İki aday gerçek ekrana uygulanıp dört kare (varyant × tema) kullanıcıya sunuldu; **canlı teal**
seçildi (`#106e86→#2ec4b6`): kimlik iki temada da tanınıyor. Diğer aday (açık zemin + koyu metin)
tema ayrımını daha net veriyordu ama koyu şerit kimliğini açık temada tümden kaybediyordu.
Bandın **zemini ve üstündeki her öğenin rengi** artık tek `BantStili` takımından geliyor — metin/
ikon/avatar ayrı ayrı `#fff` sabitlenseydi açık zeminli bir varyant metni görünmez kılardı.
Not: açık temada bant artık rayın bittiği lacivertten değil doğrudan teal'den başlıyor, yani
`rayGradyani` yorumundaki köşe kaynaşması açık temada geçerli değil — orada bilinçli bir sınır var.

**3) Giriş ekranındaki "yükleniyor"da Kadi yok.** `LoadingCurtain`'in Kadi kanalı **henüz yokken**
çalışan dalı (oturum doğrulama ve açılış perdeleri AppShell'den ÖNCE çizilir) spinner yerine
Kadi'nin 3B gövdesini çiziyor — hareket/balon yok ama kullanıcı ürünün ilk anında da aynı yüzü
görüyor.

**Kanıt:** ana sayfa handler testleri 5 yeni (seçim round-trip · kullanıcıya özellik · görünmeyen
pano reddi · sonradan görünmez olma · silinen pano çökertmiyor) ile `DashboardTests` 9/9 yeşil,
E2E 14/14 (screens + pano-filtre), `dotnet build` 0 uyarı/0 hata, `tsc` temiz. Ana sayfa akışı
canlı doğrulandı: karşılama → pano seç → ana sayfada ızgara (3 kart) → yenilemede kalıcı →
Panolar'da ev ikonu dolu → kaldır → karşılamaya dönüş.

### S42 — Gömme & Marka (sol menü madde 6): v2'nin ilk dilimi *(26 Ağu 2026)* ✅

Sol menünün **son yer tutucusu** da gerçek ekrana dönüştü: dış siteye rapor gömmek için imzalı
anahtarlar. Referans desen rakip analizinden (Luzmo embed-token, GoodData signed embed).

**Güvenlik modeli (kullanıcı onaylı — A seçeneği):** gömülü koşum SABİT bir servis hesabıyla değil,
anahtarın taşıdığı **kimlikle** yapılır ve RLS aynen uygulanır. Sabit hesap seçilseydi tek bir
anahtar sızıntısı tüm kiracının verisini açardı.

**Reddetme zinciri** (`RunEmbeddedHandler`) — her adım bir saldırı yolunu kapatır: imza → DB kaydı
(yetki anahtarın İÇİNDE değil, bu yüzden **iptal anında etkili**) → iptal/süre → **Origin** →
kiracı bağlama → kimlik + RLS. Hata mesajları bilerek ayrımsız: hangi aşamada düştüğünü söylemek
numaralandırma saldırısına yardım eder.

**Yol boyunca kapatılan iki gerçek açık:**
1. **Rolsüz anahtar RLS'i atlıyordu.** `SatirKuraliCozucu.RoleUygulanir`: rolsüz kural herkese
   uygulanır ama **rollü kural yalnız o role sahip kimliğe**. Rol taşımayan anahtar, "Bayi kendi
   verisini görsün" gibi kuralları es geçip geniş veri görürdü → rol artık **zorunlu**.
2. **Admin rolü RLS'ten muaf** (`Muaf`). Admin rollü bir anahtar bütün kuralları devre dışı bırakıp
   dış siteye kısıtsız veri açardı → **yasak** (büyük/küçük harfle de kaçılamaz; ekranda da
   seçilebilir görünmüyor).

**Diğer bilinçli kararlar:**
- **Ham SQL raporu gömülemez:** o raporlarda RLS uygulanamıyor (`RunReportHandler` aynı sebeple
  kurala tabi kullanıcıyı reddediyor). Dışarıya açılan yüzeyde RLS'siz içerik olmamalı. Rapor
  sonradan ham SQL'e çevrilirse koşum anında da durur.
- **Alt alan adı joker'i yok:** `*.firma.com` olsaydı ele geçirilmiş tek bir alt alan yeterdi.
- **Anahtar yalnız üretimde bir kez döner, saklanmaz** (parola deseni): saklanan sır, sızabilecek
  ikinci kopyadır.
- **İptal siler değil işaretler:** sızıntı sonrası "hangi anahtar ne zaman kapatıldı" denetlenebilsin.
- **Gömülü uç için AYRI CORS politikası:** ana politika önyüz adresine kilitli, gömme müşterinin
  portalından gelir. `AllowAnyOrigin` güvenliği gevşetmez — CORS tarayıcı kuralıdır, asıl kapı
  handler'daki Origin denetimidir ve çerez yoktur (kimlik anahtarla taşınır).
- **Gömülü sayfa `ProtectedArea`/`AppShell` DIŞINDA:** oturumu olmayan ziyaretçiyi korumalı alan
  Keycloak'a atardı; menü/tema da müşterinin sayfasında yeri olmayan öğelerdir.
- **v1 kapsamı yalnız RAPOR:** pano gömmek her kartı ayrı koşturmak ve farklı bir sonuç biçimi
  demek — yarım çalışan bir pano gömmesi sunmaktansa açıkça reddediliyor.

**Kanıt:** backend **431 test** yeşil (+17 gömme güvenlik testi: rolsüz/admin/izinsiz alan reddi,
ham SQL reddi, geçersiz/iptal/süresi dolmuş/izinsiz kaynak zinciri, kiracılar arası sızıntı,
kimlikle koşum, son kullanım damgası, iptalin silmemesi), `screens` E2E **13/13**, tip denetimi ve
`dotnet build` 0 uyarı/0 hata.

**Canlı güvenlik doğrulaması** (istekler tarayıcı DIŞINDAN atıldı — `Origin` tarayıcıda korumalı
başlıktır, sayfa içinden set edilemez; ilk denemede tam bu yüzden izinli kaynak da reddedilmişti):

| Deneme | Sonuç |
|---|---|
| İzinsiz kaynak (`saldirgan.com`) | **403** |
| Benzer ad (`portal.firma.com.evil.co`) | **403** — alt dize eşleşmesi yok |
| Kaynaksız doğrudan çağrı | **403** |
| İzinli kaynak (`portal.firma.com`) | **200 — çalıştı** |
| İptal sonrası aynı anahtar | **403** — anında etkili |

**⚠ Dağıtım notu:** gömülü sayfa müşterinin `iframe`'inde açılır; ön yüzü sunan katman
`Content-Security-Policy: frame-ancestors` başlığını izinli alanlara göre vermelidir (KDS'in kendi
ekranları için `'none'` — clickjacking koruması). Kurulum dokümanına yazıldı.

### S41 — Kullanım (sol menü madde 7): v1 menüsü tamamlandı *(26 Ağu 2026)* ✅

Son yer tutucu ekran gerçek ekrana dönüştü: **Yönetim → Kullanım** (yalnız Admin). "Neyi
kullanıyoruz, ne yavaş?" sorusunu **S5'te zaten yazılan denetim kayıtlarından** cevaplar — yeni bir
ölçüm altyapısı kurulmadı.

**İçerik:** dönem seçici (7/30/90 gün) · özet kartları (koşum, ortalama/en uzun süre, yavaş koşum,
başarısız oranı, sorgu çalıştıran kullanıcı) · *En çok çalışan raporlar* · *Bu dönemde hiç
çalışmayanlar* · *En yavaş koşumlar* (SQL önizlemesi + satır + kullanıcı + tarih, tam dökümü Denetim
ekranına bağlar).

- **"Yavaş" eşiği burada TANIMLANMAZ**, Sistem Ayarları'ndan okunur ve ekranda yalnız gösterilir —
  aynı eşik sonuç ekranındaki "Sorun ne?" düğmesini de açıyor. (S35'te limitlerde verilen kararın
  aynısı: aynı sayıyı iki yerde tutmak, birinin diğerini sessizce yalanlaması demektir.)
- **Ölülük MUTLAK değil, seçilen döneme GÖREdir** — ekran bunu kullanıcıya da söylüyor. Sıfır koşum
  gruplamada hiç görünmez, o yüzden "hiç çalışmayanlar" AYRICA sorulur.
- **Rapor kullanımı yalnız rapor üzerinden gelen koşumları sayar**; tasarımcı/SQL Editörü denemeleri
  (RaporId null) bir raporun kullanımı değildir.
- ⚠ **Kapsam sınırı (dürüstçe):** denetim kaydı SORGU KOŞUMUdur; "pano açıldı" diye bir olay yok.
  Pano kullanımı ancak kartlarındaki raporlar üzerinden **dolaylı** okunur. Doğrudan pano ölçümü
  ayrı bir iş — menü açıklaması da artık "hangi rapor" diyor, pano vaat etmiyor.

**Canlı bulgu ve düzeltme (aynı gün):** ilk sürümde liste **"(silinmiş rapor)" satırlarıyla doldu**
(E2E artıkları) ve asıl raporları gizledi. Silinmiş raporun kullanım sayısı kullanıcıya hiçbir şey
kazandırmaz — açılamaz, düzeltilemez. Sorgu artık `Raporlar` ile JOIN'leniyor; **özet sayaçları yine
tüm koşumları sayıyor**, yani kaybolan rakam yok. Ekran doğrulaması: `rapor=20 → rapor=5`.

**Test altyapısı notu:** bu testler SQLite değil **LocalDB** ile koşuyor. Sorgu dönemi
`OlusturmaTarihi >= baslangic` ile süzüyor ve EF Core'un SQLite sağlayıcısı `DateTimeOffset`
karşılaştırmasını çeviremiyor ("could not be translated"). Üretim hedefi SQL Server olduğu için
gerçek sağlayıcıda koşmak hem tek geçerli yol hem daha dürüst doğrulama; her test kendi geçici
veritabanını yaratıp siliyor. (Özet sayaçları da bu yüzden tek `GroupBy(_ => 1)` yerine ayrı
toplama sorgularına bölündü.)

**Kanıt:** backend **8 kullanım testi** yeşil (özet + eşiğin ayardan okunduğu + rapor koşumu ayrımı +
silinmiş rapor elenmesi + ölü rapor + döneme göre ölülük + gün kırpma + boş kiracı), `screens` E2E
**12/12**, tip denetimi ve `dotnet build` 0 uyarı/0 hata. **Canlı:** 757 koşum / %16 hata / 57 yavaş
koşum gerçek veriyle çizildi, dönem değişimi `HTTP 200`, konsol hatası yok.

**Kararsız test de kapatıldı (aynı gün).** `KurulumHizmetiTests` günlerdir tam süitte kimi koşumda
düşüp izole koşumda geçiyordu. Kök neden testlerin kendisinde değildi — her biri benzersiz DB adı
ve geçici dizin kullanıyor. Çakışan şey **süreç genelindeki `VeriDizini` ortam değişkeniydi**: onu
değiştiren iki sınıf (`KurulumHizmetiTests`, `AnahtarHalkasiTests`) xUnit tarafından paralel
koşuluyor ve birbirinin ayarını eziyordu. İkisi ortak bir `[Collection]`'a alındı → ardışık koşuyorlar.
Süitin geri kalanı paralel kalmaya devam ediyor. **Kanıt: üç ardışık tam süit koşumu, üçü de
406/406 yeşil** (öncesinde aynı süit kimi koşumda kırmızıydı).

### ~~⚠ Açık borç — JOIN YOLU BELİRSİZLİĞİ~~ → **S40'ta kapatıldı** *(sessiz yanlış, S39'da bulundu)*

Üretici, sorgudaki tabloları haritadaki ilişkilerden BFS ile birleştirir; bir tabloya **birden çok
eşit uzunlukta yol** varsa ilk bulunan seçilir. Ölçülen vaka:

```sql
-- "AUDİ marka araçların ... toplam tutarı"
FROM Talepler JOIN Servisler ON … JOIN Markalar ON Markalar.Id = Servisler.AnaBayiMarkaId
```

Yani **aracın markası değil, servisin bayi markası** süzüldü; sorgu hatasız çalıştı ve yanlış şeyi
ölçtü (sonuç null). Doğru yol `Araclar.MarkaId` üzerinden geçmeliydi.

**Kurtarıcıdan uyarmak denendi ve GERİ ALINDI:** kullanılan tabloların komşularına bakıp "birden çok
yol var" demek, tek soruda 3 uyarı üretti ve çoğu alakasızdı (ör. `TalepParcalari` için uyarıyor ama
`Talepler` zaten sorguda). Sebep yapısal — kurtarıcı üreticinin SEÇTİĞİ yolu bilmez, yalnız
olasılıkları görür; gürültü gerçek uyarıyı gizliyordu.

**Doğru çözüm üreticidedir:** alternatif yol olup olmadığını bilen tek yer orası. Seçtiği yolu
sonuçla birlikte raporlamalı (`QueryResult`'a taşınacak bilgi), ekran da "şu yoldan bağlandım"
diyebilmeli. Ayrı bir iş olarak duruyor.

**Kanıt:** backend **404 test** yeşil (+10 yeni: ilişki istemi, ilişkisiz haritada bölüm
yazılmaması, Id tahmini uyarısı, adla süzmede uyarmama, kova düşürme + tarih kovasını koruma,
Id→Ad taşıma + sayısal Id'ye dokunmama, bağlamdan kolon çözme + belirsiz bağlamda hata, metin
eşitliğinin içerire dönmesi + sayısalda dönmemesi), `dotnet build` 0 uyarı/0 hata.
`QueryGeneratorTests`'teki LIKE beklentisi COLLATE'e göre güncellendi.

*Kararsız test notu:* `KurulumHizmetiTests.sifirdan_kurulum_…` tam süitte iki koşumda düştü, izole
ve sonraki koşumlarda geçti — gerçek LocalDB'ye veritabanı yaratıp silen entegrasyon testi,
zamanlama yarışı. NLQ değişiklikleriyle ilgisi yok. → **S40'ta kök nedeni bulunup kapatıldı**
(aşağıya bakınız).


## Kullanıcı test turu — 2026-08-11 (32 bulgu; kaynak: kullanıcı testi, ekran görüntüleri `Desktop\KDS-Test`)

Çalışma düzeni (kullanıcı, 2026-08-13): madde madde düzeltilir, her madde ayrı commit'lenir,
durum burada işlenir. ⬜ bekliyor · 🔵 üzerinde çalışılıyor · ✅ düzeltildi (commit'iyle).

| # | Bulgu | Durum |
|---|---|---|
| M1 | Veri haritası **Sil** butonuna liste ekranında tepki yok; onay diyaloğu ancak editöre girince beliriyor — kök: `{confirmDialog}` yalnız editör görünümünde render ediliyordu; liste görünümüne de eklendi (tek sayfadaki iki-görünüm tuzağı; diğer sayfalar tarandı, temiz) | ✅ |
| M2 | Kanvasa yeni eklenen düğüm çok uzağa düşüyor (JoinEkranGoruntusu_2) — kök: konum orijine çapalı sabit slottu; artık (a) yeni düğüm mevcut kümeye YASLANIR (masonry: en erken biten sütunun altına, gerçek düğüm boyu + oluk), (b) oluk ORANTILI (kullanıcı ayarı): yatay boşluk = düğüm genişliğinin %80'i (160px), dikey boşluk = üstteki düğüm boyunun %80'i — iki eksen görsel olarak aynı oranda açık, (c) ekleme sonrası kadraj animasyonla tüm düğümleri kapsar (fitView) — yeni tablo hep görünür | ✅ |
| M3 | Tasarımcı alan aramasında Türkçe i/ı: "Ilceler" tablosu "ilceler" yazınca bulunamıyor — kök: `toLocaleLowerCase('tr')` I→ı çeviriyordu ("Ilceler"→"ılceler" ≠ "ilceler"); `araKatla` katlaması (i/ı/İ/I tek harfe) ile iki varyant da eşleşiyor | ✅ |
| M4 | Otomatik join bağlayamadı: araclar↔markalar, kisiadresleri↔adresler, adres↔ilceler — kök: ilişkiler haritada VARDI; join ağacını türeten kör BFS (hem önyüz `buildTree` hem backend `JoinPlani`) haritanın tümünde ilişki-dizisi sırasıyla koşuyor, kanvastaki tabloyu kanvasta olmayan ara tablo üzerinden keşfedebiliyordu. İki taraf birden KULLANILAN-öncelikli deterministik kurala geçti (M13/M15 ile birlikte) | ✅ |
| M5 | Çok-tablolu join koşulu yok: `LEFT JOIN stok.ServisStoklari ss ON ss.ServisId = t.ServisId AND ss.ParcaId = tp.ParcaId` kurulamıyor — `JoinCondition`'a koşul-başına `EbeveynTabloId` eklendi (null = ana ebeveyn; geriye uyumlu); Join Yolları koşul satırına tablo seçici geldi; join SIRASI bağımlılık-farkında topolojik sıralamaya geçti (koşulun başvurduğu tablo çocuktan önce join'lenir; döngü açık hatayla reddedilir); koşulun başvurduğu tablo alan seçilmese de otomatik join'lenir | ✅ |
| M6 | "Bağlı Tablo Özeti (alt sorgu)" ne işe yarıyor — içeride örnekle açıklansın (SELECT mi WHERE mi) — cevap: **SELECT'e alt sorgu eklemenizi sağlar** (sonuca özet kolon; WHERE değildir, satır filtrelemez/çoğaltmaz); diyaloğa açıklama kutusu + Talepler/TalepParcalari örneği + seçim tamamlanınca canlı özet cümlesi, bölüm başlığına aynı ifade eklendi | ✅ |
| M7 | Üretilen SQL **kaydetmeden çalışmadı**, rapor kaydedilince çalıştı; ayrıca alias'larda boşluk ("Odemeler Tutar") — kök KANITLANDI: SQL geçerliydi, elle koşumda 177.8 sn sürdü = 60 sn **zaman aşımı**; "kaydedince çalıştı" = ikinci koşumda ısınmış önbellek (kaydetmenin etkisi yok, iki yol aynı kapı). Düzeltme: (a) zaman aşımına özel eyleme-dönük Türkçe hata, (b) otomatik alias'lar boşluksuz (`Odemeler_Tutar`; elle verilen ada dokunulmaz), (c) hız için `infra/kdsdemo-indeksler.sql` (20 FK/join indeksi — demo indekssiz kurulmuştu) + **plan teşhisi**: asıl katil TOP+ORDER BY'ın row-goal planı (tüm ödemeleri sıralayıp tek tek zincirden geçiriyor); `DISABLE_OPTIMIZER_ROWGOAL` ipucuyla soğukta **182 sn → 7.5 sn** kanıtlandı — ipucunun üreticiye varsayılan eklenmesi M33 konuşmasına bırakıldı (bekçi beyaz listesi + altın-örnek testler etkilenir) | ✅ |
| M8 | Referans sorgu: bizde 1000+ satır, ham SQL'de 99 — kökler M4 (Ilceler'e yanlış yol → Etimesgut filtresi yanlış kümede) + M5 (ServisStoklari'nda ParcaId koşulu kurulamıyor → satır patlaması); ikisi kapanınca **sayısal kanıt**: ham SQL (ON-filtreli) = 99, tasarımcı biçimi (WHERE-filtreli + bileşik LEFT + doğru yollar) = **99 birebir** (11.7 sn). Yeni kod gerekmedi — kullanıcı sorgu turunda ekrandan yeniden kurup doğrulayacak | ✅ |
| M9 | Login hata ekranında bozuk görsel (kırmızı ünlem deseni — LoginHata.png) — kök: tema CSS'indeki `background: #fff` KISALTMASI `background-repeat`'i sıfırlıyordu; hatalı girişte PatternFly'ın ünlem ikonu tüm input'a döşeniyordu. `background-color`'a çevrildi + `[aria-invalid]` emniyet kuralı (ikon tek, sağda). start-dev'de tema önbelleği kapalı → anında canlı | ✅ |
| M10 | Dışa aktar yalnız KAYITLI raporda olsun (editör sekmelerinde olmasın); dosya adı + içerik başlığı rapor adı olsun — SQL editöründeki Dışa Aktar menüsü + `/api/sorgu/ham/disa-aktar` ucu + `ExportQueryRequest` söküldü (tasarımcıda zaten yoktu); rapor görüntüleyicidekinde dosya adı, PDF başlığı ve Excel sayfa adı ZATEN rapor adından geliyordu (doğrulandı) | ✅ |
| M11 | SQL editöründe de parametre tanımlanabilsin (metindeki parametre yerine çalıştırmada değer konarak) — SQL'e `@ad` yazınca editör altında parametre paneli açılır (tip: Metin/Sayı/Tarih + değer); değerler metin değiştirme DEĞİL gerçek `SqlParameter` olarak bağlanır (enjeksiyon-güvenli). Algılama string literal/köşeli ad/yorumları atlar, `@@sistem` hariç (`sqlParams.ts` + 7 vitest); backend ham yolunda JsonElement→CLR çözümü eklendi (`JsonDegerCozucu` — model yoluyla paylaşık). Not: `@`'lı ham SQL raporu kaydedilirse rapor koşumu parametre değeri alamaz — abonelik/rapor parametre entegrasyonu M24-25'te | ✅ |
| M12 | Rapor olarak kaydedilen sorgu tasarımcıda taslak olarak kalmaya devam ediyor — kaydedilen gelmesin, yarım kalan gelsin — kayıt başarısında oturum taslağı silinir + kaydın kendi tetiklediği tek persist yazımı bastırılır; sonraki gelişte tasarımcı TEMİZ açılır, yarım işler (ve kayıttan sonra yapılan yeni düzenlemeler) yine geri gelir | ✅ |
| M13 | JoinEkranGoruntusu: kisiadresleri↔adresler bağlanamadı (net FK örneği varken) — M4 ile aynı kök; kanvastaki tablo artık MÜMKÜNSE kanvastaki komşusuna bağlanır, ara tabloya sapmaz | ✅ |
| M14 | "otomatik: …" join metninde taraflar yer değiştirsin — kaynak tablo solda (yalnız görsel) — etiket üretimi satırın tablosunu HEP sola alacak şekilde çevrildi (ilişki haritada ters yönlü kayıtlıysa taraflar döner: "Iller.Id = Adresler.IlId"); Join Yolları satırı + kanvas kenar etiketi aynı kaynaktan beslendiği için ikisi de düzeldi; join mantığına dokunulmadı (vitest ile kilitli) | ✅ |
| M15 | JoinEkranGoruntusu_2: adres ile bağlaması gerekirken yanlış tabloyla bağladı (Iller'e `Kisiler.DogumYeriIlId` kestirmesi) — birden çok kanvas-adayında artık EN DERİN yerleşik ebeveyn kazanır (snowflake sezgisi: Iller → Adresler.IlId); otomatik join M4/M13 ile birlikte yeniden yazıldı | ✅ |
| M16 | Otomatik join'de yalnız türü değiştirebilmeliyim (INNER→LEFT) — koşulları yeniden seçmeye zorlamasın (JoinEkranGoruntusu_3) — tür seçici artık otomatik satırda da aktif: tür seçilince otomatik ebeveyn olduğu gibi sabitlenir, koşullar boş kalır (haritadaki ilişki kullanılır) → kolon yeniden seçilmez; "otomatiğe dön" linki durur | ✅ |
| M17 | Tablo alansız eklenebilsin (join'de olsun, SELECT'te olmasın) — Id'yi kaldırınca join de kalkıyor (HataEkranGoruntusu) — alan panelinde tablo başlığına **+** butonu: tablo alansız eklenir (kanvasta düğüm olur, JOIN'e katılır, SELECT'e kolon eklemez); `QueryModel.EkTablolar` ile modele/rapora taşınır, taslak/rapor yüklemede geri gelir; kanvastaki X ile çıkarılır | ✅ |
| M18 | Join sırası değiştirilebilsin (JoinEkranGoruntusu_4 — ör. kisiadresleri 2. sıraya) — Join Yolları satırlarına ▲▼ eklendi; sıra yalnız görünüm değil, otomatik ebeveyn seçiminde işleme ÖNCELİĞİ (`QueryModel.TabloSirasi` ile backend'e gider — kanvas ile SQL determinizmi korunur); taslak/raporla saklanıp geri gelir; backend testi sıranın seçimi değiştirdiğini kilitler | ✅ |
| M19 | Aggregate sorgu (COUNT DISTINCT + SUM + GROUP BY, il bazında): (a) SQL üretilemedi (b) 81 il yerine 1000+ satır — teşhis: test 11.08'de, M4 düzeltmesinden ÖNCEKİ join motoruyla yapılmıştı; "SQL üretilemedi"nin üstteki (görüntüde kesik) hatası o dönemin "join yolu yok" hatası. Bugünkü üretici referans şekli DOĞRU üretiyor (yeni birim test: COUNT DISTINCT + SUM + GROUP BY + LEFT ✓) ve **sayısal kanıt: ham=81, tasarımcı biçimi=81 grup**. Not: alanlar "Varsayılan" bırakılırsa düz liste gelir (bilinçli — toplama kullanıcı seçer); 1000+ satır o koşumdandı. Yeni kod gerekmedi | ✅ |
| M20 | Placeholder/text kayması (KaymaEkranGoruntusu — "harita alanı" vb. tüm textbox'lar) — kök: kontroller kompakt (input py 6.5) ama MUI'nin YÜZEN ETİKET konumu varsayılan yüksekliğe göre kalıyordu; tema düzeyinde `MuiInputLabel` override (translate 7px + font 13) — tüm ekranlardaki etiketli boş alanlar dikey ortalandı | ✅ |
| M21 | Harita listesi sıralaması: son eklenen başa gelmeli (şu an ada göre — SiralamaEkranGoruntusu) — `ListDataMapsHandler` son eklenen/güncellenen başa sıralar (bellekte — SQLite DateTimeOffset kısıtı, raporlardaki desen) | ✅ |
| M22 | İçe alım işi: (a) cron kutusu anlaşılmıyor — **✅** yeni `CronSecici` bileşeni (Elle/Saatlik/Günlük/Haftalık/Özel + saat-dakika-gün seçicileri + Türkçe özet: "Her gün saat 03:00'de çalışır"); (b) depo hep otomatik `KdsDepo_…` — **✅** İçe Alım bağlantısına **"Depo hedefi"** seçimi eklendi (`Connection.DepoBaglantiId` + migration): hedef, CANLI modlu mevcut bir bağlantıdır; içe alım çekilen tabloları o bağlantının veritabanına yazar (`RunHedefeAsync`, DB yaratılmaz), raporlar da oradan okur (RunQueryHandler yönlendirmesi) — yaz/oku simetrik. Boş = eski davranış (KdsDepo_tenant otomatik). Doğrulama: hedef mevcut + Canlı + kendisi değil; hedef İŞ değil BAĞLANTI düzeyinde (aynı kaynağın raporlarının nereden okuyacağı belirsizleşmesin). SQLite yönlendirme testi eklendi | ✅ |
| M23 | Aynı cron anlaşılırlık sorunu zamanlanmış raporlarda da var — aynı `CronSecici` bileşeni abonelik çekmecesine de kondu | ✅ |
| M24 | Zamanlanmış raporlar şu an çalışmıyor (parametreli rapor: zorunlu parametre verilemediği için gönderim düşüyor) — abonelik artık **parametre değerlerini saklar** (`ParametrelerJson` + migration) ve gönderim o değerlerle koşar; çekmecede rapor seçilince parametre paneli açılır (ParametrePanel ortak) | ✅ |
| M25 | Abonelik oluştururken rapor parametreleri girilebilsin (ZamanlayiciEkranGoruntusu); ayrıca tarih aralığı parametresinde "bitiş = bugün" gibi dinamik varsayılan tanımlanabilsin — abonelik çekmecesinde parametre paneli (M24) + TarihAralik bitişine **"Bugün" işareti** (değer `$bugun`; üretici çalıştırma GÜNÜNE çözer — görüntüleyici/pano panellerinde de aynı işaret kullanılabilir) | ✅ |
| M26 | Denetim ekranına rapor adı kolonu (kayıtlı raporsa dolu, değilse boş) — `AuditRecord.RaporId` (+migration); RaporId rapor/abonelik koşumlarından çalıştırma zincirine taşınır (RunReport/RunSubscription → RunQueryModel → RunQuery → audit); ekranda "Rapor" kolonu (serbest koşumda —). Eski kayıtlarda boş kalır | ✅ |
| M27 | Tüm grid'lerde sayfalama ok yerine sayfa numaralarıyla olsun — sayfalamalı iki grid'de (sonuç grid'i `ResultTable` — tasarımcı/SQL editörü/rapor ortak — ve Denetim) ok butonları MUI `Pagination` (sayfa numaraları + ilk/son kısayolu) ile değiştirildi; Admin/Bağlantılar/pano kartı kısa tam liste olduğundan sayfalama yok, kapsam dışı | ✅ |
| M28 | Tüm grid'lere kolon sort özelliği — `ResultTable` başlığa tık: artan ▲ → azalan ▼ → kapalı; sıralama TAM sonuç kümesinde (yalnız görünen sayfa değil), NULL sona, sayı/bool/metin tr karşılaştırma. Denetim grid'i sunucu sayfalı ve zaten en-yeni-önce sıralı; sayfa-içi yanıltıcı sıralama eklenmedi (ihtiyaç olursa sunucu-taraflı sort ayrı iş) | ✅ |
| M29 | Sorgu tasarımcısında SELECT'e CASE WHEN yazılabilsin — kullanıcının örneği `SUM(CASE WHEN…)` zaten **Koşullu Ölçü** ile mümkündü; eksik olan satır-düzeyi (özet'siz) CASE idi. Yeni **CASE Kolonu**: çok dallı `CASE WHEN k1 THEN s1 WHEN k2 THEN s2 [ELSE v] END` (sonuç sabit ya da kolon; sabitler parametre; koşul tabloları otomatik join; aggregate'li sorguda ifade GROUP BY'a girer). Sözleşme `CaseColumn/CaseBranch` + üretici + tasarımcı diyaloğu (Kolonlar & Ölçüler adımı) + kayıt/oturum/rapor-yükleme; golden test `case_kolonu_satir_duzeyi...` | ✅ |
| M30 | Panodan açılan raporda üstteki geri tuşu panoya dönsün (şu an raporlara atıyor) — geri hedefi sabit `/raporlar` kodlanmıştı; pano kartı rapora giderken router state ile `donus: /panolar/:id` taşır, görüntüleyicinin geri tuşu varsa onu kullanır (doğrudan/URL ile gelişte yine raporlara) | ✅ |
| M31 | Raporlarım alt menüleri (tüm raporlarım / klasörlerim) birebir aynı ekranı veriyor — kök: "Klasörlerim" rotası yalnız İLK klasörü seçmeye çalışıyordu, klasör yoksa hiçbir şey değişmiyordu; ray tıklamaları da URL'yi değiştirmiyordu (menü vurgusu kopuktu). Fix: "Klasörlerim"e KENDİ görünümü (klasör kartları ızgarası + boş durum; karta tık → o klasörün raporları), sabit ray öğeleri artık rotaya gider (menü vurgusu ekranla eşleşir) | ✅ |
| M32 | Test amaçlı üretilen örnek sorgular çok daha karmaşık/farklı senaryolarla yenilenecek — `infra/ornek-raporlar.sql`'e 10 ileri örnek eklendi (RAPOR 6-15): CTE+RANK/pay %, LAG'li aylık trend, CROSS APPLY (servis başına top-3), korele NOT EXISTS (sigortasız hasarlı araçlar), PIVOT (bölge×yıl), HAVING+DISTINCT sayım, çok koşullu detay, LAG km-sayaç denetimi, GROUPING SETS dönüşüm oranı, ödeme/fatura mutabakatı. Hepsi tek SELECT (bekçi uyumlu), tarih filtreleri deterministik `'2026-08-06'` çapasıyla; 15 sorgunun tamamı KdsDemo'da SHOWPLAN ile derletildi (sıfır hata) | ✅ |
| M33 | **(Kullanıcı notu, 2026-08-14 — gelince konuşulacak)** Sorgu çalışmadığında/yavaş kaldığında kullanıcıya ÖNERİ alanı: "sorgunuzda indeks ihtiyacı olabilir" / "sorgu performansı sorun olabilir" gibi yönlendirme (zaman aşımı mesajının ötesinde, sorguya özel teşhis). M7 bulguları: (1) FK indeksleri (`infra/kdsdemo-indeksler.sql`) gerekli ama tek başına yetmedi; (2) asıl hız kaybı TOP+ORDER BY **row-goal** planı — `OPTION (USE HINT('DISABLE_OPTIMIZER_ROWGOAL'))` soğukta 182→7.5 sn (24×); üreticiye varsayılan eklenmesi konuşulacak (SELECT-only bekçisinin beyaz listesine OPTION/hint izni + ~20 altın-örnek testin güncellenmesi gerekir; riski düşük, kazancı büyük).<br>**✅ Yapıldı (14 Ağu 2026) — "Sorun ne?" tanılaması:** zaman aşımı mesajının yanında ve >10 sn süren koşumun sonuç çubuğunda **Sorun ne?** butonu (tasarımcı + SQL editörü); basınca sorgu ÇALIŞTIRILMADAN `SET SHOWPLAN_XML ON` ile tahmini plan alınır (`POST /api/sorgu/tanila`, `/tanila-model`; koşumla aynı bekçi ve aynı mod yönlendirmesi — Canlı/İçe Alım/M22b depo hedefi `QueryConnectionResolver`'da paylaşılır). Kural motoru (`QueryPlanRules`, saf): **eksik index** (kopyalanabilir `CREATE INDEX` + etki %), **büyük tablo taraması** (ölçüt OKUNAN satır `EstimatedRowsRead`; columnstore taraması BI'da normal sayılıp elenir), **TOP+büyük Sort = row-goal** (ipucu önerir), **koşulsuz join (kartezyen)**, **örtük tip dönüşümü**. Bulgular şiddet sıralı + tek cümle özet ("Sorun büyük olasılıkla veritabanında: eksik index" gibi); KDS kaynağa ASLA yazmaz, öneriyi DBA'ya götürülecek metin olarak verir.<br>**Plan alınamayan durumlar ayrı ayrı yanıtlanır (kullanıcı isteği):** SHOWPLAN yetkisi yok (Msg 262) → yetki bulgusu + `GRANT SHOWPLAN` metni + SQL metninden genel öneriler (hepsi "bilgi" şiddetinde); plan zaman aşımı → "sunucu yanıtsız/ilk derleme istatistik oluşturuyor, sonra tekrar deneyin"; bağlantı hatası → bağlantı bulgusu.<br>**ROWGOAL kararı: varsayılan YAPILMADI** — kanıt tek desene ait, her sorgunun planını değiştirmek riskli; tanılama deseni görünce ipucunu ÖNERİR (kullanıcı isterse ham SQL'de dener). Doğrulama: 6 kural birim testi (canlı DB'siz) + gerçek KdsDemo'da elle doğrulama (eksik index önerisi ~%11 etki, 2M satır tarama) + SHOWPLAN yetkisi kısıtlı kullanıcıyla yetki dalı | ✅ |
| M34 | **(Kullanıcı, 15 Ağu 2026)** "Join'de sorgu yazma" — `LEFT JOIN (SELECT TalepId, SUM(Tutar), COUNT_BIG(*) … GROUP BY TalepId) o ON o.TalepId = t.Id` gibi. **Tespit:** bu desen zaten **Bağlı Tablo Özeti** (M6) ile üretiliyordu, ama **alt sorgu başına TEK ölçü** vardı → iki ölçü = aynı tabloya iki türetilmiş tablo (iki tarama). **Yapıldı (A seçeneği):** aynı tablo + aynı bağ kolonu + **aynı filtreye** düşen özet alanlar TEK türetilmiş tabloda birleşir (`deger1..degerN`); tek ölçülü grupta kolon adı yine `[deger]` (mevcut SQL/testler değişmedi); SELECT kolon sırası model sırasını korur; filtreler farklıysa ayrı tablolarda kalır (birleştirme yanlış sonuç verirdi). Gruplama filtre SQL'i üretilmeden **yapısal imzayla** yapılır (aynı filtre her üretimde farklı `@p` adı alır) ve üyeler indeksle tutulur (record değer-eşitliği tuzağı). **Kanıt:** 2 yeni altın-örnek test (birleşme + farklı filtre ayrımı) · **185 test yeşil** · gerçek KdsDemo'da elle yazılan SQL ile üretilen biçim **satır satır aynı** (20=20, iki yönde EXCEPT farkı 0). **Kapsam dışı bırakılan (C seçeneği):** kullanıcının serbest SQL yazdığı alt sorgu — kolonlar haritadan bilinmediği için tasarımcı alan listesi/tip çıkarımı ayrı tasarım ister; ihtiyaç sürerse ayrıca planlanacak | ✅ |
| M35 | **(Kullanıcı, 15 Ağu 2026 — testi kilitleyen iki durum)** (a) `+` ile tablo eklenince alanları kendiliğinden açılıyor, kapalı gelsin; (b) JoinEkranGoruntusu'ndaki gibi **Odemeler yukarı taşınamıyor** ve "ilk eklediğim tablo base olsun" (otomatik kök farklı tablo alıyor). **Kökler:** (a) alan listesinde "kullanılan tablo AÇIK başlar" varsayılanı vardı, `+` tabloyu kullanılanlara sokunca otomatik açılıyordu; (b) ▲▼ **tam liste** üzerinde takas yapıyor, komşu FROM kökü olduğunda takası **sessizce iptal** ediyordu (buton basılıyor, satır kıpırdamıyor) — ekranda satırlar kök HARİÇ listelendiği için indeksler de tutarsızdı; (c) otomatik kök yalnız **ilk ALANIN** tablosuna bakıyordu, `+` ile eklenen tablo ve kullanıcı sırası hesaba katılmıyordu. **Yapıldı:** `+` tabloyu açıkça kapalı işaretler (alan tıklayarak eklemede davranış aynı); taşıma **görünen liste** üzerinde yapılır ve kök önceliğin başına yazılır (mantık saf çekirdeğe alındı: `joinSirasiDegistir`); her tablo (alan ya da `+`) **ekleme sırasına** yazılır, otomatik kök = **sorguya ilk giren tablo**; erişilebilirlik ("join yolu yok") hesabı da artık AYNI kökü kullanır (eskiden ayrı kök türetiyordu). **Kanıt:** 4 yeni birim test (kök komşuluğunda yukarı taşıma — eski kodda düşerdi, aşağı taşıma, sınırlar, köksüz durum) · **22 model testi + build yeşil** | ✅ |
| M36 | **(Kullanıcı, 17 Ağu 2026 — 5 madde)** (3) join'i yalnız LEFT'e çekince otomatiklik kaybolmasın/join'den düşmesin, alan değiştirirsem ayarlı olsun; (4) filtreler "hepsi VE ile birleşir" yazıyor — VEYA da olabilmeli; (5) Klasörlerim en son açılana göre sıralansın; (6) ekli tablolar alan listesinde açık geliyor, kapalı gelsin; (7) **AUDI raporu**: kaydedip "tasarımcıda aç" deyince "kolon yok" hatası — 2. join koşulu kayboluyor. **Kökler + yapılan:** (7) **veri kaybı hatası** — rapor yüklenirken join koşulunun **hedef tablosu** (`ebeveynTabloId`, M5 alanı) okunmuyordu: koşul ana ebeveyne kayıyor, kolon listesi değişince seçili kolon boşalıyor ve koşul sessizce düşüyordu → yükleme artık alanı taşıyor. (3) LEFT seçimi ebeveyni de **sabitliyor**, satırı "ayarlı"ya çeviriyordu; ebeveyn ara tablo ise satır join ağacından tümüyle düşüyordu → tür artık AYRI tutulur (`joinTurleri`), satır otomatik kalır ve SQL üretilirken o anki ebeveynle birleşir; ara-tablo satırında tür seçici açıklamalı biçimde kapalıdır. (4) WHERE ağacı satır bağlaçlarından kurulur (`filtreAgaciKur`): hepsi VE ise **tek düz grup** (eski SQL birebir korunur), VEYA varsa soldan sağa yuvalanır — `A VE B VEYA C → ((A VE B) VEYA C)`; backend her grubu zaten parantezler; yükleme `filtreDuzlestir` ile bağlaçları geri açar (eski düz gruplar da desteklenir). (5) klasör sırası "son açılan" damgasıyla (yerel depo; sunucuda tutulmayan kullanıcı bilgisi), eşitlikte ada göre. (6) alan listesinde "kullanılan tablo açık başlar" varsayılanı kaldırıldı — tablolar kapalı başlar, arama sırasında eşleşenler açılır. **Kanıt:** 5 yeni birim test (düz VE grubu korunuyor · VEYA yuvalaması · düzleştirme kurmanın tersi · eski kayıt uyumu · boş filtre) · **27 model testi + build yeşil**; backend'de iç içe VEYA + parantez zaten altın-örnek testli | ✅ |

**Kod inceleme turu (2026-08-04, kullanıcı isteği — derleme + test + canlı çalıştırma +
Edge/Playwright görsel doğrulama; bulguların hepsi düzeltildi):**
- ✅ **Sorgu tasarımcısı kenar hatası (gerçek):** kontrollü kenar state'i (`useEdgesState`)
  ile React Flow store `setEdges`'i karışmıştı — JOIN kenarı ilk node hareketinde
  kaybolabilirdi → `setKenarlar` + `addEdge` (label/type kenarın kendisinde). Canlı
  kanıt: kenar, node sürüklendikten sonra da duruyor.
- ✅ **.gitignore tuzağı (gerçek):** kökteki `.env.*` deseni `frontend/.env.development`'ı
  yoksayıyordu — kimlik bayrağı/API adresi repoya girmeyecek, başka makinede ön yüz
  Keycloak yokken kimlik açık başlayacaktı → Vite kuralına çevrildi
  (yalnız `.env.local` / `.env.*.local` yoksayılır).
- ✅ OpenAPI belgesi yalnız Development ortamında yayınlanır oldu.
- ✅ favicon 404'ü (tek konsol hatası) data-URI SVG ikonla giderildi; son turda konsol 0 hata.
- Görsel doğrulama (sistem Edge + Playwright, DnD simülasyonu): kabuk/menü/rozet,
  Panolar'da "API canlı 0.1.0" kartı, kanvasa 2 tablo bırakıldı, **tablolar-arası
  etiketli JOIN kenarı** kuruldu; LocalDB 17.0 bağlantısı sqlcmd ile doğrulandı;
  `/api/durum` 200, `/api/durum/kimlik` token'sız 401, CORS başlığı doğru.
- 📌 **S8 notu:** kolon kendi tablosunun kolonuna da bağlanabiliyor (self-join SQL'de
  meşrudur); model tabanlı `isValidConnection` doğrulaması S8'de eklenecek.

## Test dönüşleri — 2026-08-07 (kullanıcı, SIRALI çalışma listesi)

EG# = kullanıcının gönderdiği ekran görüntüsü sırası. Sırayla ilerlenecek; 3+9+14 tek dilim.
⚠ **Çalışma kuralı (kullanıcı, 2026-08-07):** bu maddeler yapılırken karar gerektiren her noktada
kullanıcıya SORULACAK (varsayılan-seç-ilerle tercihi bu liste için askıda).

**EG sözlüğü (görüntülerin kalıcı tarifi):**
- **EG1** — Sol panelde `Odemeler` grubu açık (Id, OdemeTarihi, OdemeTipiId, TalepId, Tutar,
  TaksitSayisi, BankaId, MakbuzNo), altta `ServisStoklari(7)`, `Talepler(19)`; kanvasta
  Talepler–Araclar (`Talepler.AracId = Araclar.Id ✎`) ve Odemeler–Talepler
  (`Odemeler.TalepId = Talepler.Id ✎`) çipli düğümler. Şikayet: alan ekleyince grup kapanamıyor.
- **EG2** — Kanvas kuşbakışı: Adresler, Ilceler, KisiAdresleri, Kisiler, GetirmeSebepleri,
  Servisler, Talepler, Araclar, Modeller, Markalar düğümleri dağınık; `TalepParcalari` kenarı
  sağda ekran dışına taşacak kadar uzakta. Şikayet: yeni düğüm çok uzağa doğuyor.
- **EG3** — "Birleşik Kolon" diyaloğu: Görünecek ad `KisiAdSoyad`; parçalar sırayla
  `Kolon·Kisiler·Ad` / `Metin·[boş görünen kutu]` / `Kolon·Kisiler·Soyad`; "+ PARÇA EKLE".
  Soru: ortadaki Metin kutusu ne işe yarıyor (cevap: ayraç — boşluk değeri görünmüyor).
- **EG4** — Sol paneldeki `<> SQL KOŞULU` düğmesi yakın çekim. Şikayet: WHERE koşulu Adım 1'de
  değil Adım 2 Filtreler'de olmalı; VE/VEYA da seçilebilsin.
- **EG5** — Adım 2 "Kolonlar & Ölçüler": Talepler.Id, Odemeler.Id, Araclar.MarkaId, Araclar.Id,
  Markalar.Id, Modeller.Id, Kisiler.Id, KisiAdresleri.Id, Adresler.Id, Ilceler.Id,
  GetirmeSebepleri.Id, Servisler.Id, TalepParcalari.GarantiKapsamindaMi, TalepParcalari.Id,
  Parcalar.Id, ServisStoklari.Id, Faturalar.Id — hepsi "Varsayılan". Şikayet: Adım 1'de kurulan
  `KisiAdSoyad` birleşik kolonu bu listede YOK.
- **EG6** — Aynı liste başka sorguda: Markalar.Ad … TalepParcalari.Tutar "Varsayılan" ama
  `ServisStoklari.MevcutAdet` ve `Faturalar.GenelToplam` otomatik **"Topla (Σ)"** gelmiş.
  Şikayet: ölçü rollü alan otomatik SUM almasın, hepsi Varsayılan başlasın.
- **EG7** — Sol menü Raporlar altı: Tüm Raporlar / Klasörlerim (tooltip: "Rapor klasör ağacın.") /
  Favorilerim / Benimle Paylaşılanlar. Şikayet: hangisine basılırsa basılsın sağ ekran değişmiyor.
- **EG8** — Raporlar sayfa başlığı: "Raporlar — Kayıtlı raporlar — aç, çalıştır, yıldızla;
  tasarımcıda düzenle." + sağda "+ YENİ RAPOR". Şikayet: çok basit, güzelleştirilsin.
- **EG9** — SQL Editörü ipucu şeridi: "Yalnız tek SELECT geçer (AST bekçisi) · satır sınırı +
  zaman aşımı zorunlu · her icra denetime yazılır · Ctrl+Enter = çalıştır". Şikayet: anlaşılmıyor
  ("icra", "AST bekçisi").
- **EG10** — "İlişkili Özet Alan" diyaloğu: Özetlenecek=Talepler, Bağlanacağı=Araclar,
  Özet=Say(#), Kolon boş ("Kolon seç…"), filtre `GarantiKapsamindaMi = 1`, UYGULA pasif.
  Kullanıcı yorumu: "SELECT'e iç sorgu penceresi sandım; CASE WHEN yazamıyorum"
  (ör: SUM(CASE WHEN t.GarantiKapsamindaMi = 1 THEN 1 ELSE 0 END) AS GarantiliTalep).
- **EG11** — Adım 2 alt şeridi: Gruplama (Markalar·Ad ×, Talepler·Id ×, Araclar·Id ×, "+ kolon") ·
  İlişkili Özetler "+ ÖZET ALAN" · Sıralama "+ SIRA EKLE" · Satır Limiti 1000. Soru: özet
  bölümünün bu konumda olması doğru mu?
- **EG12** — Toplama açılır menüsü: Varsayılan · Topla (Σ) · Ortalama · En az · En çok · Say (#) ·
  Benzersiz say. Şikayet: Say = COUNT mu, Benzersiz say = COUNT DISTINCT mi — adlandırılsın.

| # | EG | Konu | Plan | Durum |
|---|----|------|------|-------|
| 1 | EG1 (sol panel + kanvas) | Alan eklenince tablo grubu panelde kapanamıyor | Kullanılan-tablo "hep açık" kuralı yerine elle aç/kapa override'ı | ✅ (kullanıcı test etti) |
| 2 | — | Ekran değiştirince tasarım kayboluyor | Tasarımcı durumu sessionStorage'ta; dönünce kaldığı yerden | ✅ (kullanıcı test etti) |
| 3 | — | Çift FK'li tabloda istenmeyen join kaldırılamıyor | Ağaç modeli: tablo TEK ebeveyne bağlanır (Join Yolları'nda seçilir), fazla kenar hiç kurulmaz; kanvas yalnız gerçek kenarları çizer | ✅ (Join Yolları) |
| 4 | EG2 (dağınık kanvas) | Yeni düğüm çok uzağa düşüyor | 4 sütunlu sarmalı grid — yeni düğüm görünür alana yakın doğar | ✅ |
| 5 | EG3 (Birleşik Kolon) | Ortadaki "Metin" parçası ne işe yarıyor? | CEVAP: ayraç metni (boşluk/tire) — kalıyor; kutunun yanında "␣ boşluk — kolonların ARASINA girer" ipucu | ✅ |
| 6 | EG4 (SQL koşulu Adım 1'de) | WHERE koşulu Adım 2 Filtreler'de olmalı + VE/VEYA seçilebilsin | Düğme Filtreler bölümüne taşındı ("SQL koşulu ✓ (VE/VEYA)" rozetli); diyalogda VE/VEYA seçimi; model ElleKosulBaglaci (VEYA'da iki taraf parantezli) | ✅ |
| 7 | EG5 (Adım 2 listesi) | Birleşik kolon Adım 2'de görünmüyor | Kolonlar & Ölçüler listesinde birleşikler de satır olarak (parça özeti + Düzenle diyaloğu + kaldır) | ✅ |
| 8 | EG6 (Topla ön-seçimi) | Ölçü rollü alan otomatik Topla geliyor | Her alan "Varsayılan" başlar; toplamayı kullanıcı seçer | ✅ |
| 9 | — (SQL karşılaştırma) | Üretilen sorgu 0 satır; elle yazılan 99 | ANALİZ: (a) kök tablo "ilk alanın tablosu" (Markalar) — omurga yanlış; (b) BFS en kısa yolu seçip Servisler'i AnaBayiMarkaId, Araclar/Modeller'i MarkaId ile Markalar'a bağladı — oysa zincir Talepler üzerinden olmalı → yanlış topoloji + filtre kesişimi = 0 satır; (c) ServisStoklari BİLEŞİK join ister (ServisId+ParcaId) ve LEFT olmalı — modelde yok. ÇÖZÜM (3+14 ile tek dilim): **Join Yolları düzenleyicisi** — kök tablo seçimi, tablo başına ebeveyn seçimi, kolon çifti + EK koşul (bileşik), INNER/LEFT türü, ilişkiyi devre dışı bırakma | ✅ (2·Join Yolları adımı: KokTabloId + JoinAyari — ebeveyn/tür/bileşik koşul; 5 üretici testi; kanvas ağaç-kenar) |
| 10 | EG7 (Raporlar alt menü) | Alt menü tıklamaları ekranı değiştirmiyor | Rota değişimi grup'u günceller (baslangic yalnız ilk state'ti); Klasörlerim ilk klasörü açar | ✅ |
| 11 | EG8 (Raporlar başlığı) | Header çok basit | Marka gradient şeridi (lacivert→teal) + ikon + rapor/favori/klasör sayaç rozetleri + beyaz Yeni Rapor | ✅ |
| 12 | EG9 (bekçi ipucu) | "İcra/AST bekçisi" anlaşılmıyor | Sade dil: "Sadece tek bir SELECT sorgusu çalıştırılabilir (INSERT/UPDATE/DELETE engellenir) · satır limiti · otomatik durdurma · denetim kaydı" | ✅ |
| 13 | EG10+EG11 (Özet Alan) | CASE WHEN yazılamıyor; ad/konum belirsiz | KARAR (kullanıcı): yapısal **Koşullu Ölçü** + özet alan adı **"Bağlı Tablo Özeti (alt sorgu)"**. Koşullu Ölçü: toplama+değer kolonu+koşul satırları → `AGG(CASE WHEN … THEN … END)` (Topla ELSE 0; Say/AVG/MIN/MAX ELSE'siz); koşul tabloları otomatik join; aggregate sayılır (GROUP BY tetikler); 3 üretici testi | ✅ |
| 14 | — | JOIN türü seçilemiyor (hep INNER) | Join Yolları satırında INNER/LEFT seçimi | ✅ (Join Yolları) |
| 15 | EG12 (toplama menüsü) | "Say/Benzersiz say" belirsiz | Etiketler SQL karşılığıyla: Topla (SUM) · Ortalama (AVG) · En az (MIN) · En çok (MAX) · Say (COUNT) · Benzersiz say (COUNT DISTINCT) | ✅ |

## Araştırma kuyruğu (v1 sonrası)

- ~~İlişkili özet alan (correlated aggregate)~~ → **✅ TAMAMLANDI (2026-08-07)** — karar günlüğüne taşındı.

- ~~**Dashboard v2 — widget yerleşimi + pano-düzeyi filtreler**~~ → **✅ TAMAMLANDI** — widget
  yerleşimi/boyutu (2026-08-09, caeaa24) + pano-düzeyi filtreler (2026-08-10); karar günlüğüne taşındı.
- ~~**KDS Ajanı** — müşteri ağına kurulan outbound gateway (Power BI Gateway modeli);
  Mod B job'ının müşteri tarafında koşup itme (push) yapması (00-analiz §3)~~ →
  **✅ TAMAMLANDI (1 Eyl 2026, S45)** — `Kds.Ajan` + push protokolü; kaynak parolaları müşteri
  ağından çıkmaz, S44 artımlıyla birleşik. (Canlı sorgu — Mod A'nın ajan üzerinden aktarımı —
  kapsam DIŞI kaldı; ihtiyaç doğarsa ayrı iş.)
- **Çoklu motor** — `ISorguUretici` lehçe genişlemesi; ilk aday PostgreSQL (SQLST v3 deneyimi)
- ~~**Artımlı yükleme** — rowversion/CDC ile delta senkron~~ → **✅ TAMAMLANDI (27 Ağu 2026, S44)** —
  su seviyesi + staging + MERGE (CDC bilinçli elendi: kaynak salt-okunur); 2M satırda 118 sn → 1.3 sn.
- **Zamanlanmış rapor e-postası** → **✅ TAMAMLANDI (2026-08-10, S15)** — Hangfire yerine İçe Alım'ın
  hafif deseni (Cronos + BackgroundService). ~~Uyarı/eşik bildirimleri hâlâ v1 kuyruğunda~~ →
  **✅ TAMAMLANDI (S20)** — Uyarılar ekranı gerçek, aynı zamanlayıcı altyapısını paylaşıyor
  (bayat kayıt 27 Ağu 2026'da düzeltildi).
- ~~**Satır-düzeyi güvenlik (RLS)**~~ → **✅ TAMAMLANDI (18 Ağu 2026, S19)** — ayrıntı: [09-satir-duzeyi-guvenlik.md](09-satir-duzeyi-guvenlik.md).
- ~~**Gömme (embedding) + beyaz-etiket** — v2 (rakip analizi Kademe 3)~~ → **✅ TAMAMLANDI
  (26 Ağu 2026, S42)** — imzalı anahtarla dış siteye rapor gömme; kimlik + RLS anahtarla taşınır.
  Kalan dağıtım adımı: `Content-Security-Policy: frame-ancestors` (docs/07 §7c).
- ~~**OSI / Apache Ossie semantik uyumu** — metrik katmanına import/export (rakip analizi §5)~~ →
  **✅ TAMAMLANDI (2 Eyl 2026, S46)** — spec v0.1 (Apache Ossie) biçiminde dışa/içe aktarım;
  ANSI_SQL lehçesi + LST_KDS uzantısıyla kayıpsız round-trip.
- **Fiyatlandırma modeli** — hibrit (creator seat + tenant paketi); analizde yok, iş-modeli kararı (rakip analizi §6)
- ~~**PDF export** — QuestPDF lisans eşiği nedeniyle karar bekliyor (00-analiz §9)~~ →
  **✅ KARAR VERİLDİ + YAPILDI (2026-08-09, S13 Part 1):** QuestPDF lisans eşiğinden kaçınıldı,
  **PDFsharp-MigraDoc (MIT)** kullanıldı; cross-platform font çözücü ile Docker'da da çalışır.
  Kuyrukta kalması bayat kayıttı (17 Ağu 2026'da düzeltildi).
- ~~**Doğal dille sorgu (LLM)** — v1.x hedefi (rakip analizi Kademe 2)~~ → **✅ TAMAMLANDI
  (20 Ağu 2026, S23; S32/S39/S40 ile derinleşti)** — tamamen YEREL model (Ollama qwen2.5-coder:7b),
  metriklere/haritaya grounded. Açık tek ölçüm: **3B karşılaştırması** (kurumsal ağ engeli).
- **Barındırma maliyeti + K8s yeniden değerlendirme** — sinyaller: 00-analiz §10
- **Vault entegrasyonu** — bağlantı sırları için (v1: Data Protection)

## Karar günlüğü

- **2026-08-23** — **KADİ DEĞERLENDİRMESİ SÜREÇ KURALI (kullanıcı kararı).** Bundan sonra HER yeni
  ekran tasarımında ve her geliştirmede "Kadi buna göre bir hareket yapacak mı?" sorusu açıkça
  değerlendirilecek: ekran giriş cümlesi/pozu (KadiYoldas EKRANLAR haritası), işlem sahneleri
  (kadiYukleniyor / kadiGorevSahnesi) ve sonuç duyuruları (kadiBasarili / kadiHata) o işin
  tasarımının PARÇASIDIR — sonradan eklenecek süs değil. Karar "hareket yok" da olabilir ama
  bilinçli verilmeli.

- **2026-08-10** — **S15 Zamanlanmış Raporlar (rapor aboneliği).** Sol menüdeki ilk placeholder gerçek
  ekrana dönüştü. Abonelik bir raporu cron ya da elle çalıştırıp PDF/Excel/CSV ekiyle e-postayla teslim
  eder. **Mimari karar — Hangfire DEĞİL:** roadmap notu "Hangfire üstünde" diyordu ama İçe Alım (S12)
  bilinçli olarak Hangfire yerine hafif yolu (Cronos + `BackgroundService` + `AmbientTenant`, durum işin
  satırında) seçmişti; tutarlılık ve ağır/lisanslı bağımlılıktan kaçınma çizgisiyle abonelik de aynı
  deseni kullandı (`AbonelikZamanlayici`). **E-posta taşıma katmanı takılabilir:** `IEpostaGonderici`
  soyutlaması; SMTP yapılandırılmışsa (`Eposta:*`) `SmtpEpostaGonderici` (System.Net.Mail, `SYSLIB0014`
  izole pragma ile — yeni paket/MailKit yok), değilse dev/test'te `GunlukEpostaGonderici` günlüğe yazar →
  boru hattı (rapor→export→e-posta) SMTP olmadan da uçtan uca çalışır ("yarım özellik yok"). Rapor
  **görünürlük-kapısız** koşulur (abonelik zaten Admin/Gel tarafından, kendi tenant'ında kuruldu; aynı
  bekçi+sınır+denetim zinciri). `Kds.Business`→`Kds.Export` referansı eklendi (saf biçimlendirici; ADR
  ihlali değil). **Kanıt:** 3 birim test (CRUD + run pipeline: fake ISender→gerçek exporter→PDF→e-posta
  yakalama→"Başarılı" durumu) + canlı **E2E `abonelik.spec`** (dev günlüğünde `E2E Abonelik.pdf` 59 KB) +
  backend 157/158, tam E2E 27/27. **Sonraki:** pano aboneliği + gerçek SMTP canlı doğrulaması; aynı
  zamanlayıcı Uyarılar ekranına da temel olacak.
- **2026-08-10** — **Pano-düzeyi filtreler (S11 ertelemesinin kapanışı).** Pano görüntüleyici artık
  raporlarının parametrelerini **anahtara göre birleştirip** (ilk tanım kazanır) tek bir filtre şeridinde
  toplar; "Uygula" değerleri **her karta** (`DashboardCard.parametreDegerleri` → `runReport(id, değerler)`)
  taşır. Kart yalnız kendi tanımladığı parametreyi kullanır — üretici (`CozParametre`) fazla anahtarı sessizce
  yok sayar, dolayısıyla farklı parametreli raporlar aynı panoda güvenle bir arada durur. **Yeni backend yok**:
  parametreli-rapor altyapısı (çalıştırma-anı `ParametreDegerleri`) aynen kullanıldı. Rapor parametreleri panoya
  gömülü değil → görüntüleyici açılışta rapor detaylarını **salt-okunur** çeker, birleşik listeyi kurar
  (parametresiz panoda hiç şerit çıkmaz). **Kanıt:** tsc/lint/build/vitest temiz + yeni **E2E `pano-filtre.spec`**
  (parametreli rapor → panoya koy → şeritten değer ver → kartın `/calistir`'i o değeri taşır) canlıya karşı yeşil,
  **tam süit 26/26**. Böylece "Dashboard v2 — widget yerleşimi + pano-düzeyi filtreler" kuyruğu tümüyle kapandı
  (widget = caeaa24).
- **2026-08-10** — **Güvenlik borcu: DOMPurify açıkları (npm audit).** `monaco-editor@0.56` geçişli olarak
  `dompurify@3.4.8`'e sabit; 4 GHSA (XSS / hook-pollution / Trusted-Types) `<=3.4.12`'yi vuruyordu. `audit fix
  --force` monaco'yu 0.53'e **düşürürdü** (kırıcı); yerine `package.json > overrides` ile `dompurify@3.4.13`
  (patch bump, monaco kullanımıyla uyumlu). Sonuç: `npm audit` **0 açık**, build yeşil. Bonus: `npm run lint`
  eskiden e2e/fixtures.ts'te Playwright `use` fixture'ını React hook sanıp kırmızıydı → hedefli
  `oxlint-disable` + kullanılmayan değişken temizliğiyle yeşile alındı.
- **2026-08-10** — **Parametreli rapor (çalıştırma-anı parametreler).** Kullanıcı isteği: rapor, açılışta
  değer alan parametreler (ör. tarih aralığı) taşıyabilsin, çalıştırırken o değerlere göre koşsun. 3 faz:
  **(1) Backend** — `QueryModel.Parametreler` (`ReportParameter`: anahtar/etiket/tip/zorunlu/varsayılan/seçenekler;
  tipler Metin/Sayı/Tarih/**TarihAralık**/EvetHayır/Liste). Bir filtre koşulunun değeri sabit YA DA
  parametre-referansı `{ $param, parca? }` olur. `MssqlQueryGenerator.Generate(…, parametreDegerleri)` ref'i çalışma
  değerine (yoksa varsayılan) çözer, TarihAralık ucunu (baslangic/bitis) çıkarır, zorunlu+eksik→400, sonra kolon
  tipine çevirip yine **@pN** parametresine bağlar (**enjeksiyon güvenliği DEĞİŞMEZ**). RunQueryModel/GenerateQuerySql/
  `RunReportRequest` `ParametreDegerleri` taşır (mevcut uçlar bozulmadan). **(2) Tasarımcı UI** — "Parametreler" tanım
  bölümü + her filtre satırında **⟶ parametre** bağlama (TarihAralık seçimi op'u Arasında yapar) + Sonuç adımında
  test değer paneli; paylaşılan `ParametrePanel` (tip başına giriş); `QueryController` `/sorgu/sql`+`/calistir`
  artık request-record (`{ model, parametreDegerleri }`) alır. Taslak sakla/yükle + rapor modelinden geri yükleme
  (param-bağı dahil). **(3) Görüntüleyici** — rapor parametreliyse üst **parametre şeridi + "Uygula"**; `runReport`
  değerleri gövdede taşır, `ReportsController.Calistir` gövdeden alır. **Kanıt:** 6 üretici testi + 3 vitest + canlı
  (KdsDemo, altId 0→6/4→3 satır) + tam **E2E 25/25** (`parametreli.spec`) + uygulamalı walkthrough (FaturaArsiv,
  Dönem 2024↔2025 → sonuç veri değişir). Wire değişikliğini kaçıran `ApiIntegrationTests` güncellendi (backend süiti
  yakaladı). Commit'ler `95d26b1` (Faz1) · `504092c` (Faz2) · `db6a318` (Faz3).

- **2026-08-09** — **Kod standardı: IUnitOfWork kaldırıldı (K5 netleşti).** Tutarsızlık vardı: 2 handler
  `IUnitOfWork` enjekte ediyor, 14 handler doğrudan `context.SaveChangesAsync()` çağırıyordu. `IUnitOfWork`
  yalnız transaction sarmalı (`IslemdeCalistirAsync`) için değerliydi; `KaydetAsync` ise birebir
  `SaveChangesAsync` (saf dolaylama). Karar (kullanıcı): **tek standart** — soyutlama kaldırıldı, TÜM
  handler'lar doğrudan `context.SaveChangesAsync()`; çok-adımlı atomiklik gereken 2 handler için transaction
  bir **`KdsDbContext.IslemdeCalistirAsync` uzantı metoduna** taşındı (ExecutionStrategy/retry korunur).
  "A yolu" (handler+EF doğrudan, repository/UoW katmanı yok) felsefesiyle tutarlı. `UnitOfWorkTests` →
  `TransactionExtTests`. build 0/0, 124 test.

- **2026-08-09** — **S14 Olay günlüğü (KdsLog Mongo) — üretici tarafı.** KDS'e ÖZEL, QMS'ten AYRI Mongo
  DB `KdsLog` (`mongodb://localhost:27017`; QMS'in merkezi hub'ına bağlanmadı — kullanıcı ayrı depo
  istedi). **3 koleksiyon** `Action`/`Exception`/`DataChange`; Exception+DataChange, ait oldukları isteğe
  **`ActionId` ile** bağlanır (`Action._id`=ActionId → "bu aksiyon şunları değiştirdi/şu hatayı attı").
  **Yerleşim (referans grafiği gereği):** soyutlamalar (`IEventLog`/`IActionContext`/olay modelleri)
  **Kds.Contracts.Logging** (Kds.Data yalnız Contracts'ı görür); Mongo yazıcı izole **Kds.Logging**
  (`MongoDB.Driver` 3.10, Apache-2.0; Guid=Standard — 3.x zorunluluğu); `DataChangeInterceptor`
  **Kds.Data**'da (`AddKdsData` interceptor'ı ekler); `HttpActionContext`+`ActionLogMiddleware`+
  ExceptionHandler kancası **Kds.Api**'de. **Dayanıklılık:** `IEventLog.Write` bloklamaz (sınırlı
  Channel + BackgroundService; dolarsa en eski düşer; Mongo düşse istek bozulmaz; bağlantı yoksa no-op).
  Şifre alanları maskeli (`SifreSifreli:"***"`), Türkçe okunur (relaxed JSON encoder), `AppId=lst-kds`
  (çok-uygulama). **Uçtan-uca kanıt:** bağlantı ekle → `KdsLog.DataChange`(Insert) `ActionId`'si →
  `KdsLog.Action`(`POST /api/baglantilar`,200,admin) ile eşleşti. **124 test** (interceptor birim testi
  dahil). **Tüketici (aktivite/denetim ekranı) ayrı/gelecek uygulama** (kullanıcı: "farklı uygulamada
  kullanacağız").

- **2026-08-09** — **Üç katmanlı otomatik test + İngilizce geçişin 3 gizli regresyonu yakalandı/düzeltildi.**
  **(1) Backend HTTP-entegrasyon** (`tests/Kds.Tests/Integration`): `WebApplicationFactory<Program>` ile
  API bellekte — sahte "Test" JWT (Keycloak yok), paylaşımlı-önbellek SQLite (gerçek eşzamanlılık), her
  test kendi tenant'ını tohumlar. 10 test: 401/403, tenant izolasyonu, parola sızmaz, 400 ProblemDetails,
  CRUD, sürükle-bırak modeli→SQL üretimi, whoami-roller guard. **(2) Yük testi** (`tests/Kds.LoadTests`):
  NBomber 6.x **kurumsal lisans** istediğinden (K4/MediatR<13 deseniyle tutarlı) **bağımlılıksız özel
  koşucu** — p50/p95/p99 + RPS; 2500 istek 0 hata. **(3) E2E** (Playwright, gerçek Keycloak + tarayıcı;
  `frontend/e2e`): 23 test — login otomasyonu (ROPC kapalı → form + sessionStorage enjeksiyonu; token
  git-ignore), tüm ekranlar, Bağlantı/Rapor/Harita/Pano CRUD, menü/rol, **flagship: editör↔sürükle-bırak
  sonuç kıyası** (aynı sorgu iki yolla → `/api/sorgu/calistir` satırları = `/api/sorgu/ham`). Rehber:
  `docs/test-rehberi.md`. **Yakalanan + düzeltilen regresyonlar** (hepsi İngilizce geçişinde Türkçe kalması
  gereken wire/rota/menü'nün İngilizce'ye kayması): (a) 3 frontend api rotası `/api/connections|dashboards|
  reports`→Türkçe; (b) 7 navigasyon yolu (`menu.tsx` + geri/git butonları) `/dashboards|reports|connections`
  →Türkçe (menüden boş sayfa geliyordu); (c) whoami `Roles`→`Roller` (`StatusController`) — istemci rolleri
  boştu → rol-korumalı menü + "Yeni Pano" + tasarım butonları herkeste gizli. **Toplam: backend 122/122,
  E2E 23/23.** Ayrıca **kod içi yorumlar Türkçe bırakılır** kararı + güvenli iç tanımlayıcı adları İngilizce
  (backend yerelleri + frontend fonksiyonları; wire-çakışan yereller Türkçe) — `docs/ingilizce-kod-gecisi.md`.

- **2026-08-09** — **S11 Panolar (Dashboard) — birinci-sınıf özellik.** Pano = adlandırılmış SIRALI
  rapor koleksiyonu; kalıcı `Dashboard` entity + `Panolar` tablosu (yeni migration; Türkçe kolonlar) +
  CRUD (görünürlük rapordaki gibi: Özel→sahibi+admin, Ekip→tenant) + 3 test. Frontend: `/panolar`
  galeri (oluştur/aç/düzenle/sil), `/panolar/:id` görüntüleyici (panonun raporları canlı kart
  ızgarası), `PanoEditor` (ad + paylaşım + sıralı rapor seçimi). Grafik mantığı ortak yardımcıya
  çıkarıldı (`reportChartOption`/`scoreCardValue`) — rapor görüntüleyici + pano paylaşır. Widget
  yerleşimi/boyutu sonraki dilim.

- **2026-08-09** — **Proje dili İngilizce'ye geçiş (kural: kod İngilizce, DB + ekran + uyarılar
  Türkçe).** Referans/sözlük: `docs/ingilizce-kod-gecisi.md`. Yapılan (her adım build+test yeşil):
  `Kds.Ortak→Kds.Common` (ICommand/IQuery/ValidationBehavior), `Kds.Calistirma→Kds.Execution`,
  ~130 backend tip adı + dosya adı İngilizce (Connection/DataMap/Query/Report/User/Folder/Schema…),
  frontend `api/*.ts` fonksiyon + tip adları İngilizce. **Bilinçli olarak Türkçe bırakıldı:**
  özellik/DTO alan adları + enum ÜYE değerleri (= DB kolonları/saklanan değerler; `HasConversion`
  değişmeden korunur, migration yok) + JSON wire alanları (`ad/sunucu/mod`) + ekran metinleri +
  validator/exception mesajları. Migrated API boot doğrulandı (`/api/durum` 200).
  **TAMAMLANDI (aynı gün):** `Kds.Dal→Kds.Data`; tüm backend modül namespace + klasör adları
  (Connections/DataMaps/Queries/Reports/Users/SystemStatus/Identity/Entities); frontend klasörleri
  (pages/components/identity/layout/designer) + sayfa/bileşen/dosya adları + yaygın yardımcı export
  kimlikleri (theme sabitleri, hook'lar, designer helper'ları, `Edit*` tipleri). Kod okuma dili
  İngilizce; DB + wire + ekran + route Türkçe. build 0/0 · 111 backend + 9 frontend test yeşil.
  (Not: özellikler Türkçe bırakıldığından "DB'yi Türkçe tutma" EF eşleme mekanizması gereksiz kaldı —
  `docs/ingilizce-kod-gecisi.md` DURUM bölümüne bakın.)

- **2026-08-08** — **CQRS düzeni: tip başına dosya + iç Send temizliği + validator payı** (kod
  inceleme sohbeti dönüşü, 3 dilim): (1) Paketlenmiş `*Komutlari/*Handlerlari/*Dtolari` dosyaları
  açıldı — her istek/handler/doğrulayıcı/DTO **kendi dosyasında, dosya adı = tip adı**; "1 uç =
  1 istek = 1 handler" klasörde tek bakışta izlenir. Bilinçli istisna: `SorguModeli.cs` tek
  kompozit sözleşme (model + parça kayıtları birlikte anlamlı). Validator yalnız doğrulanacak
  girdisi olan istekte yazılır (parametresiz liste/tek-Guid isteklerine tören validator'ü
  eklenmez; boş Guid zaten 404/false üretir). (2) Handler içinden handler Send'i kaldırıldı:
  harita okuma `IHaritaOkuyucu` servisine indi. **Bilinçli tek istisna — "tek kapı":** ham SQL,
  model ve rapor çalıştırma yollarının ÜÇÜ de iç `SorguYurutKomutu`'na yönlenir; SELECT-only
  bekçisi + denetim (audit) + satır sınırı tek noktada yaşar (`SorguDenetimDavranisi` yalnız bu
  komutu sarar). Bu Send bir kısayol değil, güvenlik hunisidir — kaldırılacaksa audit servisle
  birlikte taşınmalı. (3) Model kuralları paylaşılan `SorguModeliDogrulayici`'ye çıktı; SQL üret
  ve model çalıştır yolları aynı kurallardan geçer. 108/108 test.

- **2026-08-08** — **Kod incelemesi + aynı gün kapatma** (`docs/kod-inceleme-2026-08-08.md`):
  1 Kritik (musteri raporu çalıştıramıyordu → `/api/raporlar/{id}/calistir`, rol-duyarlı hata
  mesajı), 3 Yüksek (satır limiti tutarsızlığı, para DECIMAL bağlama, ElleKosul literal/belirsiz-ad),
  9 Orta, 6 Düşük ve 4 test borcu kapatıldı — her madde ayrı commit, 105/105 test. Y4 tasarımcı
  refactor'u da 4 dilimde tamamlandı: `tasarimci/ortak.tsx` (sabitler+tipler+mini UI) ·
  `tasarimci/Kanvas.tsx` (düğüm/kenar) · `tasarimci/model.ts` (React'sız saf çekirdek:
  `agacKur`+`modelOlustur`) · `tasarimci/Diyaloglar.tsx` (5 diyalog) — ana dosya 1860 → 1222
  satır. Frontend test altyapısı kuruldu: **vitest** (`npm run test`), `model.test.ts` 9 birim
  testi (BFS/ayar ebeveni, ara tablo, oto-takma ad, filtre değer eşleme). İnceleme tümüyle kapandı.

- **2026-08-07** — **Birleşik Kolon + Elle SQL Koşulu (tasarımcı, Adım 1 sol paneli).** (1) Birleşik
  kolon: kolonlar + sabit metin ayraçları tek çıktı kolonunda — üretici `CONCAT(...)` (NULL parça
  `+` gibi tümünü NULL'a düşürmez), sabitler PARAMETRE (SQL'e gömülmez), parça tabloları otomatik
  join, aggregate'li sorguda ifade GROUP BY'a girer. (2) Elle SQL koşulu: WHERE'e serbest koşul
  (alt sorgu dahil); `{Tablo.Kolon}` yer tutucuları haritada doğrulanıp alias'a çevrilir, tablolar
  otomatik join'lenir, filtrelerle AND. Güven düzeyi SQL Editörü ile aynı (roller özdeş) + üretilen
  SQL icrada yine SELECT-only bekçisinden geçer. 6 yeni üretici testi (87/87); raporla saklanır.
- **2026-08-07** — **İlişkili Özet Alan (correlated aggregate) tasarımcıda.** Kullanıcı örneği:
  servis başına aktif çalışan sayısı — farklı granülaritede + kendi filtreli özet kolon. Naif join
  fan-trap yapardı (satır çoğalması SUM'ları şişirir), ham SQL gömmek güvenliği kırardı → yapısal
  `OzetAlan` (tablo+kolon+toplama+bağ tablo+takma ad+filtre); üretici türetilmiş tabloya çevirir:
  `LEFT JOIN (SELECT bagKol, AGG(kol) FROM t WHERE f GROUP BY bagKol)`. Bağ kolonları haritadaki
  ilişkiden (kullanıcının join seçimi varsa o); özet filtresi YALNIZ özet tablosunun kolonlarını
  kullanabilir (kapsam sızıntısı yok); COUNT/SUM'da ISNULL(...,0) (LEFT JOIN NULL yanıltmasın);
  aggregate'li sorguda özet ifadeleri GROUP BY'a otomatik girer. UI: Adım 2 "İlişkili Özetler"
  bölümü (çip → tıkla-düzenle) + diyalog (tablo/ölçü/bağ/filtre satırları); raporla saklanır.
  5 yeni üretici testi (81/81).
- **2026-08-07** — **SQL Editörü'nden rapor kaydetme.** Rapor artık İKİ tasarım türü taşır: model
  (tasarımcı, JSON AST) YA DA ham SQL + bağlantı (SQL Editörü). `Rapor` varlığına `HamSql`/`BaglantiId`
  eklendi (HaritaId/ModelJson null'lanabilir; migration `RaporHamSql`); kayıtta da SELECT-only bekçisi
  koşar (reddedilen SQL hiç saklanmaz — testli). Görüntüleyici SQL raporunu `api/sorgu/ham` ile koşar;
  boyut/ölçü değerlerden çıkarılır; "SQL Editöründe Aç" ↔ "Tasarımcıda Aç" türe göre. UI standartları:
  `window.confirm` yerine `useOnay()` diyaloğu (5 kullanım), sayfa ilk yüklemesi `YukleniyorPerde`,
  sorgu koşarken `CalisiyorGosterge` (hafif karartma + hap), sonuç tablosunda kolon-bazlı hizalama.
- **2026-08-07** — **ALTYAPI: columnstore standardı (rapor tablosu = NCCI).** LocalDB/Express bellek
  tavanı ~1.6 GB, KdsDemo 7.7 GB → satır-deposu aggregate taramaları soğukta zaman aşımı. Ölçüldü:
  Markalar raporu (2M×1.5M join) **39.170 ms → 923 ms SOĞUK** (42×), Talepler ciro 30.000+ → 309 ms.
  11 büyük tabloya sayısal/tarih kolonlu NCCI kuruldu (canlı + script kalıcı). Kural: rapor sorgusunun
  dokunduğu her büyük tabloya NCCI.
- **2026-08-07** — **Sorgu güvenilirliği: ORDER BY(aggregate) soğuk-önbellek timeout'u.** Belirti:
  aynı sorgu editörde saniyeler, uygulamada "Yürütme Zaman Aşımı"; ORDER BY'sız 0.4 sn, ORDER BY'lı
  ilk koşum timeout → tekrar 2 sn. Teşhis (denetim kayıtları + DMV): ORDER BY(aggregate) TOP'un
  erken-durmasını kapatır → 2M satırlık `talep.Talepler` TAM taranır; soğuk önbellekte 308 MB disk
  okuması + LocalDB bellek grant kuyruğu (`RESOURCE_SEMAPHORE`) 30 sn'yi aşar. Çözüm (ölçülerek):
  (1) **`NCCI_Talepler_Rapor` columnstore** — soğuk önbellekte 309 ms (100×, script'e kalıcı);
  (2) **KdsDemo `AUTO_CLOSE OFF`** (LocalDB varsayılanı ON; 7.7 GB DB boşta kapanıp soğuk açılıyordu);
  (3) komut zaman aşımı 30→60 sn (columnstore'suz tablolar için emniyet payı). İki yanlış hipotez
  denendi ve GERİ ALINDI: ARITHABORT ON (fark yok) ve literal TOP (parametreli TOP ile birebir aynı;
  "felaket plan" ölçümü öksüz oturum çekişmesiyle kirlenmişti — temiz zeminde ölç!).
- **2026-08-07** — 📌 **Log mantığı konuşulacak** (kullanıcı notu): sorgu koşumu/hata/denetim
  loglarının tasarımı — mevcut DenetimKayitlari + API logları üstüne ne gerekiyor, ayrı oturumda
  ele alınacak.
- **2026-08-07** — **KdsDemo: büyük test veritabanı + örnek raporlar** (kullanıcı istedi: uzak müşteri
  DB'sine benzer YEREL test kaynağı — her geliştirmede taşıma derdi olmasın). `infra/ornek-veritabani.sql`:
  araç/kişi/servis/talep domain'i, **184 tablo** (64 gerçek + 120 yıllık arşiv deseni — S6'nın 600-tablo
  arama senaryosu), **~25M satır** (Kisiler 2M, Adresler 2.5M, Talepler 2M, TalepIslemleri 2.5M…),
  **83 FK** (harita FK-önerisi tam beslenir), **10 konu şeması** (kullanıcı istekleri: `ortak`=36 lookup
  tek yerde; kisi/arac/adres/servis/talep/stok/finans/arsiv), ~7.7GB, deterministik + idempotent
  (~8 dk kurar), TC/plaka/VIN sahte. `infra/ornek-raporlar.sql`: 5 uçtan-uca örnek rapor (S9 SQL
  Editörü'ne yapıştır-çalıştır; R1 = Etimesgut×Audi×ödeme>5000×parça+stok+fatura, 99 satır/14 sn).
  ⚠ **Deterministik üretim dersleri** (script yorumlarında da): (1) saf doğrusal mod zincirleri filtreli
  raporlarda alanları tek sınıfa kilitler ("hep Fatih" / servis başına tek parça) → kritik dağıtımlara
  doğrusal-olmayan terim (`n/41` vb.); (2) çarpanlar ortak böleniyle korelasyon kurar (30000 ≡ 0 mod
  5000); (3) Ankara ilçe adları gerçek — Etimesgut Id 70 seçimi bilinçli (adres üretimi yalnız 0/3/6/9
  ofsetlerini doldurur, talep/araç sınıflarıyla kesişen tek yuva 9). Stok kullanım-güdümlü (870k:
  talepte kullanılan her servis+parça çiftinin stok kaydı var) + 3 rapor indeksi (R1: 120sn→14sn).
- **2026-08-06** — **S9 · Ham SQL ekranı tamam.** Küçük dilim (S5 çekirdeği + S8 grid'i hazırdı):
  Monaco 0.56 (yeni yerleşim: kök import tüm dilleri kaydeder, `basic-languages` kalktı; worker
  `editor.worker.start.js?worker`), sayfa lazy-load (Monaco ayrı chunk — ana bundle 1.5MB'ta kaldı);
  `POST /api/sorgu/ham` aynı tek kapıdan (bekçi+limit+denetim); `SorguGuvenlikException`→400 eşlemesi
  eklendi (bekçi retleri artık okunur). **Ayrıca (beklemede):** uzak müşteri DB'sine erişim sorunu için
  karar — API DB'yi görebilen makinede koşmalı (v1'de taşınmak yerine) **yerelde benzer demo DB** ile
  geliştirme; `infra/ornek-veritabani.sql` taslağı hazır, kullanıcı şema girdisi verecek, kurulmadı.
- **2026-08-06** — **S10 · Rapor kaydetme + grafikler tamam** (kullanıcı onaylı; mockup ile — açık temaya sabitlendi).
  Rapor MODEL saklar, SQL değil (açılınca güncel haritayla yeniden üretilir); ModelJson wire ile aynı
  biçim (camelCase + string enum + polimorfik `tur`). **Kararlar:** klasörler düz liste (v1);
  paylaşım Özel/Ekip (kişi-bazlı sonraki dilim); favori kullanıcı-bazlı; görüntüleyicide tür değişimi
  oturumluk (kalıcı tercih tasarımcıdan kaydedilir); musteri görüntüleyebilir, tasarlayamaz.
  **Yeni altyapı:** `IKullaniciBaglami` (Business arayüzü, Api'de token'dan — sahip/favori/görünürlük).
  **ECharts 6** tree-shaken (bar/line/pie + 3 bileşen). SQLite testleri için liste sıralaması bellekte
  (DateTimeOffset ORDER BY çevrilmiyor). **70/70 test** (round-trip model dahil, görünürlük 3 rol,
  favori toggle, klasör-sil→klasörsüz). ⚠ **Keşif:** `npm run build` env'siz çalışınca kimlik
  fail-fast'ımız (`VITE_KIMLIK_YETKILI yok` throw) rolldown'da module-level DCE'ye dönüşüyor →
  bundle "başarılı" ama uygulamasız (321KB). Bilinçli davranış; ama GERÇEK üretim derlemesi
  `VITE_KIMLIK_YETKILI` (+istemci/api adresi) env'iyle alınmalı — S13 yayın adımına not. Env ile tam
  bundle 1.5MB/483KB gzip doğrulandı. ~~⚠ **Borç:** `npm audit` react-router advisory'si
  (GHSA-qwww-vcr4-c8h2, RSC-mode CSRF)~~ → **✅ kapandı (25 Ağu 2026)**, aşağıya bakınız.
- **2026-08-06** — **S8 · Sorgu Tasarımcısı UI tamam** (kullanıcı onaylı; mockup ile). S7 motorunu
  gerçek ekrana bağladık: harita seçici + alan paneli (tıkla-ekle, rol renkleri) + Kolonlar/Ölçüler
  (kolon başına toplama) + filtreler (alan/op/değer, hepsi VE) + otomatik gruplama + sıralama + limit;
  "Kullanılan tablolar & ilişkiler" şeridi join'leri haritadan gösterir. **"SQL'i Göster"** (üret) +
  **"Çalıştır"** (üret → S5 çalıştırıcı). **Backend:** `SorguModelCalistirKomutu` + `POST /api/sorgu/calistir`;
  `SorguYurutKomutu`'na opsiyonel `Parametreler` eklendi (audit + bağlantı mantığı yeniden kullanılır);
  JSON'dan gelen filtre değerleri (`JsonElement`) SqlParameter için CLR'a çözülür. **Wire testi:** frontend'in
  gönderdiği JSON (polimorfik `tur` filtresi + string enum) deserialize + üretim kilitlendi — bu test,
  `FiltreKosulu`'nun İKİ constructor'ı yüzünden System.Text.Json'un deserialize edememesi (canlıda her
  filtreli sorgu 500 olurdu) hatasını **yakaladı**; kısayol constructor kaldırıldı. Ölü React Flow
  scaffold'ı (`TabloDugumu`) temizlendi. **Kullanıcı geri bildirimiyle 2. tur (aynı gün):**
  (1) **React Flow ilişki kanvası** eklendi (mockup'taki gibi) — yalnız sorguya giren tablolar düğüm,
  join kenarları haritadan, düğümler taşınabilir; **join yolu olmayan tablo kanvasta kırmızı** işaretlenir
  ve istek atılmadan alanlı hata gösterilir (ilk canlı testte kullanıcının aldığı "join yolu yok" 400'ü
  aslında DOĞRU korumaydı — haritadaki HaritaIliskiler tablosunun diğerlerine FK yolu yoktu; sorun
  gösterimdeydi). (2) Soldan **sürükle-bırak** (+ tıkla-ekle). (3) **Yoğunluk standardı temaya taşındı:**
  taban 13px, h5 18px, tüm buton/input varsayılan small + kompakt dolgu — "yazılar büyük/kalabalık"
  şikayeti tüm uygulamada tek yerden çözüldü; kabuk içerik dolgusu daraltıldı. (4) `apiGonder` hata
  gövdesi ProblemDetails ise `detail`/`title`/`errors` çıkarılır — kullanıcı ham JSON görmez.
  **3. tur (kullanıcı geri bildirimi):** ekran **3 adımlı sihirbaza** çevrildi — 1) sürükle-bırak
  sorgu oluşturma (BÜYÜK kanvas, ~ekran boyu), 2) kolonlar & ölçüler + filtre/sıra/limit, 3) sonuç +
  üretilen SQL (adıma girince SQL otomatik üretilir). ⚠ **Kanvas bug'ı düzeltildi:** React Flow v12
  controlled modda `onNodesChange`'e gelen **dimensions** değişimleri de uygulanmalı (yalnız pozisyon
  uygulanırsa düğüm ölçülemez ve HİÇ görünmez) → düğümler state'e alınıp `applyNodeChanges` kullanıldı;
  "sürükledim, hiçbir şey gelmedi"nin nedeni buydu. **4. tur — canlı 500 kökten çözüldü:** kullanıcının
  Çalıştır'daki hatası (a) bayat API (yine `--no-build` tuzağı: FiltreKosulu düzeltmesi sonrası API eski
  Contracts DLL'iyle koşuyordu → deserialize patlıyordu) + (b) **uniqueidentifier kolona string "1"
  filtresi** (SqlException: conversion failed). Kalıcı düzeltmeler: **üreticide tipli parametre
  dönüşümü** — filtre değeri kolonun harita `VeriTipi`'ne çevrilir (guid/int/bit/decimal/date;
  JsonElement dahil), çevrilemezse üretim anında anlaşılır 400 ("'1' değeri HaritaTabloId kolonunun
  tipine (uniqueidentifier) çevrilemedi"); `HataYakalayici`'ye **ValidationException→400** ve
  **SqlException→400** ("Sorgu çalıştırılamadı" + DB mesajı) eşlemeleri. **Sıfırdan canlı E2E test**
  (gerçek Keycloak token'ıyla, geçici direct-grant aç/kapat): harita çek → join+Say+gruplama+sıralama+
  GUID filtreli model → `/api/sorgu/sql` (tipli `@p0=guid`) → `/api/sorgu/calistir` → **gerçek satır
  döndü** (HaritaTablolar|7, 39 ms); "1" senaryosu → temiz 400 doğrulandı. **66/66 test.** ⚠ v1: filtre düz VE listesi
  (backend AND/OR ağacını destekler; UI'da iç içe VEYA sonra); react-querybuilder/TanStack yerine sade MUI.
- **2026-08-06** — **S7 · Sorgu modeli + SQL üretici tamam + code review** (kullanıcı onaylı; mockup ile başlandı).
  JSON sorgu modeli (`Kds.Contracts/SorguModeli`: alanlar + filtre AĞACI AND/OR + sıralama + limit) →
  `MssqlSorguUretici` (`Kds.Calistirma`) → parametreli SQL. **Güvenlik çekirdeği:** kimlikler
  QUOTENAME'li (`]`→`]]`), değerler parametre, yalnız haritadaki tablo/kolon; join'ler haritanın FK
  ilişkilerinden (BFS spanning tree, ara tablolar dahil); üretilen SQL S5 bekçisinden geçer (tek kapı,
  testle kanıtlı). Deterministik → **altın-örnek** testleri (JSON → birebir beklenen SQL). **Mimari
  kararı:** `Kds.Calistirma` artık `Kds.Contracts`'a referans verir (sorgu modeli + harita şeması
  sözleşmeleri için); iki-dünya kuralı korunur — yasak olan **`Kds.Dal`/DbContext** referansıdır,
  Contracts saf DTO. **Code review (bağımsız düşmanca):** enjeksiyon YOK (kullanıcı `TakmaAd`'ı dahil
  tüm kimlik yolları `Q()`'dan geçer); düzeltilen robustluk bulguları — aralık-dışı `Toplama` enum'u
  (sayısal JSON token) geçersiz SQL yerine reddedilir; bozuk haritada fantom ara-tablo 500 yerine
  reddedilir; LIKE 'İçerir' joker kaçışı (`[%]`/`[_]`/`[[]`); GROUP BY dedup; sessiz OR kaldırıldı.
  Ek: global exception handler (`SorguModeliException`→400, `KeyNotFound`→404) — böylece handler'larda
  elle try/catch gerekmez. **63/63 test.** ⚠ **v1 sınırı:** self-join ve aynı tablo çifti arası çoklu
  ilişki disambiguasyonu yok (belgelendi; gerekirse S8'de ilişki UI'dan seçtirilir).
- **2026-08-06** — **S6 · Veri Haritası tamam** (kullanıcı onaylı; canlı test etti).
  Editör: soldan haritadaki tablolar + orta kolon tablosu (özelleştirilmiş isim + kullanım) +
  ilişkiler. **600 tablo ölçeği:** kanvasa tüm şema değil, haritanın alt kümesi; tablo ekleme
  sunucu-tarafı arama (`sys.tables` LIKE + top-N), kolonlar tembel, FK'lar `sys.foreign_keys`'ten
  otomatik önerilir. **UX kararları (kullanıcı geri bildirimiyle):** "dostça ad" → **"özelleştirilmiş
  isim"**; kriptik rol simgeleri (🔑/B/Ö/∅) → renkli nokta + açıklamalı **Select** ("Kullanım":
  kırılım/sayısal/anahtar/gizli); **mod seçici editörden kaldırıldı** (bağlantıda belli — devralınır);
  kaydedilmemiş değişiklik uyarısı. **⚠ Backend hatası (bulundu+düzeltildi):** güncellemede istemci
  üretimli Guid Id taşıyan alt varlıklar, kök *takipli-ama-Added-değil* olduğu için EF'te "mevcut
  kayıt" sanılıp INSERT yerine UPDATE→0 satır (`DbUpdateConcurrency`) atıyordu → ilk kayıttan sonra
  her Kaydet patlıyordu; açık `Add`'leme ile çözüldü + LocalDB round-trip regresyon testi (yeni +
  güncelleme). **Görünüm:** tema `shape.borderRadius=12` yüzünden `borderRadius:3`=36px "oval"
  kutular; bu ekranda net px yarıçap (kart 10 / buton-satır 8) + kompakt input'lar. **50/50 test.**
  ⚠ Tuzak (yine): migration sonrası API'yi çalıştırmadan önce TAM `dotnet build` (çalışan API DLL'i
  kilitler → önce durdur, derle, başlat).
- **2026-08-05** — **"Semantik katman / Veri Modeli" → "Veri Haritası" yeniden adlandırma** (kullanıcı).
  "Semantik katman" jargonu son kullanıcıya bir şey anlatmıyordu; kullanıcı 4 seçenek arasından
  **"Veri Haritası"**nı seçti (ilişki/harita vurgusu). Menü/tooltip/rota (`/veri-haritalari`) +
  dokümanlar (00/03/04/05/roadmap) güncellendi. **Rakip-analizi (06)**'daki "semantik katman/model"
  RAKİPLERİ anlattığı için (sektör terimi) olduğu gibi kaldı. Kavram aynı: ham tabloları dostça ad +
  ilişki + boyut/ölçü ile kullanıma hazır hâle getiren katman (Power BI "semantic model" karşılığı).
  Not: `04-veri-modeli.md` dosya adı/başlığı **metadata DB şemasını** kastediyor (özelliği değil) — değişmedi.
- **2026-08-05** — **S5 · Çalıştırma çekirdeği tamam + code review** (kullanıcı onaylı).
  SELECT-only bekçisi (ScriptDom AST — string değil), sorgu çalıştırıcı (**tek kapı**: her icra
  bekçiden geçer; timeout/satır sınırı/iptal), `sys.*` şema keşfi, denetim (`DenetimKaydi` + pipeline
  davranışı — başarılı+engellenmiş icrayı yazar, hatayı yutmaz), yürütme orkestratörü.
  ScriptDom 161.9142.1 (MIT). **Code review (bağımsız düşmanca):** ana bulgu — kara-liste
  tablo-değerli fonksiyonları kaçırıyordu (`fn_get_audit_file`/`::fn_trace_gettable` = dosya/UNC
  okuma → SMB/NTLM sızıntısı, EXEC'siz saf SELECT) + iç içe DML. Düzeltme: **kara-liste → beyaz-liste**
  (yalnız bilinen-güvenli tablo kaynakları geçer, fail-closed). Belgelenen kalan riskler: kiracı-kapsam
  ifşası (bekçi sözdizimsel; gerçek izolasyon = salt-okunur login + **RLS/Kademe-1**), skaler UNSAFE-CLR
  UDF (yüksek önkoşul), DoS (timeout+satır sınırı hafifletir). Ölü EXEC-ziyaretçi dalı kaldırıldı.
  **48/48 test yeşil** (28 bekçi kabul/red vakası) + LocalDB canlı (gerçek SELECT + satır sınırı +
  şema keşfi). ⚠ Tuzak: `dotnet test` Api'yi derlemez → migration sonrası çalıştırmadan önce TAM
  `dotnet build` şart (yoksa Api/bin'de eski snapshot → PendingModelChangesWarning).
- **2026-08-05** — **S4 · Bağlantı yönetimi tamam** (kullanıcı onaylı; localhost/LocalDB canlı test).
  Ekran tasarımı mockup ile onaylandı: kart varsayılan + **Kart/Tablo geçişi**, **yeni=modal**,
  **düzenle=çekmece**. Backend: `Baglanti` entity + `BaglantiEkle` migration; CQRS handler'lar;
  parola **Data Protection** ile şifreli (Business `ISifreKoruyucu` → Api `SifreKoruyucu`), özet
  parolayı döndürmez. **Test Et gerçek** — çalıştırma dünyası `IBaglantiDenetci`+`SqlBaglantiDenetci`
  (Microsoft.Data.SqlClient 6.0.2, `SELECT 1`, kısa timeout; **KullaniciAdi boşsa entegre kimlik**
  → LocalDB testine uygun). Alan adı **"Veri alma modu"** olarak netleşti (eski "varsayılan mod").
  Uçlar Admin+Geliştirici; Api JSON string enum; `Dal→Contracts` (paylaşılan `VeriAlmaModu`).
  Kullanıcı bulgusu düzeltildi: **kaydetten sonra otomatik test-by-id** ile form testinin sonucu
  ana ekrana yansır. **12/12 test yeşil; LocalDB'ye canlı gerçek bağlantı doğrulandı.**
- **2026-08-05** — **Login yeniden tasarım + kabuk cilası** (kullanıcı görseliyle iteratif).
  Sol ray: ince/yuvarlak yarı-saydam scrollbar (yeni menüyle ray uzadı). Keycloak login teması
  (`infra/keycloak-theme/kds`): (1) her zaman **markalı zemin** — sol **%25** lacivert→teal marka
  paneli + sağ **%75 grimsi**; (2) form artık gölgeli/yuvarlak **beyaz kart**; (3) inputlar 46px
  tek tip, focus'ta gölge halka (kutu boyutu değişmez → tıklamada kayma yok) +
  `scrollbar-gutter:stable`; (4) imleç dikey ortalı; (5) **şifre-göster: buton/hücre/ayırıcı
  kaldırıldı** → input içine bindirilen küçük saydam göz simgesi (PatternFly `pf-m-control`
  `::after` kenarlığı + odak halkası da kapatıldı). Dev'de tema önbelleği kapalı; güncel tema
  çalışan Keycloak `themes/kds`'e kopyalanır. Canlı (kullanıcı) onaylı.
- **2026-08-05** — **Rakip/piyasa analizi + menü eksik özellik eklemeleri** (kullanıcı isteği).
  15 ürün + trend/fiyat/Türkiye taraması → [06-rakip-analizi.md](06-rakip-analizi.md). En yakın
  rakipler: açık kaynak (Metabase/Superset) + gömülü çok-kiracılı SaaS (Bold BI/Qrvey/GoodData/
  Luzmo/Holistics). Boşluk analizi: çekirdeğimiz (sürükle-bırak+SQL+veri haritası+iki mod+
  çok-kiracılık) piyasayla **eşit**; ama **RLS, zamanlanmış rapor, eşik uyarıları, NLQ, gömme,
  mobil** bizde yok/ertelenmişti — çoğu 2026'da "olmazsa-olmaz". **Karar (kullanıcı: "hepsini
  ekleyelim"):** eksik özellikler sol menüye eklendi — **Soru Sor** (NLQ) → Çalışma Alanı; yeni
  **OTOMASYON** grubu = **Zamanlanmış Raporlar** + **Uyarılar** (M/G/A); **Satır Güvenliği (RLS)**
  ve **Gömme & Marka** → Yönetim (A). Menü grup sayısı: admin 5 / gelistirici 4 / musteri 2.
  Öncelik kademeleri (06 §9): **Kademe 1/v1** = RLS + zamanlanmış rapor + uyarılar; **Kademe 2/
  v1.x** = veri haritasına grounded **Türkçe NLQ** + responsive/PWA; **Kademe 3/v2** = gömme +
  OSI uyumu + anomali/push; ayrıca **fiyat modeli** (hibrit) iş-modeli kararı olarak açık. Kritik
  ders: **AI, veri haritası olmadan halüsinasyon üretir** → NLQ, Metrikler/Veri Haritaları
  olgunlaştıktan sonra. Menü ekranları şimdilik yer tutucu (BosSayfa); kademelerin dilimlere
  bağlanması (yeni dilim mi / mevcuda ek mi) ilerleyen turda. 05 §3-§6 + araştırma kuyruğu güncellendi.
- **2026-08-05** — **Sol menü kesinleşti ve uygulandı** (kullanıcı onayı). Araştırma:
  [05-menu-arastirma.md](05-menu-arastirma.md) (Power BI/Tableau/Metabase/Superset taraması).
  Kararlar: **A (grup başlıklı)** düzen · gruplar ÇALIŞMA ALANI / OLUŞTUR / VERİ / YÖNETİM ·
  **Ana Sayfa** = kişisel içerik (kayıtlı panolar/grafikler + son sorgular) · **Panolar** ayrı ·
  **Metrikler** v1'de · Bağlantılar+İçe Alım = geliştirici+admin · Yönetim = yalnız admin.
  Her öğede **görev açıklaması** (rayda tooltip); tam görev/rol tablosu 05 §6. Boş grup
  başlığıyla gizlenir; canlı doğrulandı (admin 4 grup / gelistirici 3 / musteri 1).
  **Kural (kullanıcı):** "kim neyi kullanıyor" sorulduğunda açıklama **05 §6 biçimiyle**
  (grup → öğe → ne iş yapar → roller) verilir.
- **2026-08-04** — **Keycloak Docker'sız çalıştırılıyor** (S3): makinede Docker yok ama
  JDK 17 var; Keycloak ZIP dağıtımı `start-dev --import-realm` ile koşuyor. `docker-compose.yml`
  SaaS/başka ortam için korunur. Realm deklaratif JSON'dan import edilir (tekrar üretilebilir).
- **2026-08-04** — **UI bileşen kiti: MUI (Material UI)** (kullanıcı kararı; MIT —
  00-analiz §9'a eklendi). Sürükle-bırak için analizdeki **React Flow** seçimi teyit
  edildi ve kanvas iskeleti kuruldu.
- **2026-08-04** — **Geçici kimlik bayrağı:** Keycloak realm'i kurulana kadar ön yüzde
  `VITE_KIMLIK_KAPALI=true` (AppBar'da açıkça rozetli); S3'te kaldırılacak.
- **2026-08-04** — **Sıra değişikliği (kullanıcı):** backend iskeleti öne alındı —
  S2'nin backend yarısı S1'den önce yapıldı. S2'nin kalanı (React iskeleti + docker-compose)
  hemen ardından tamamlandı; **S1 (gereksinim + ekran tasarımı) retroaktif olarak 2026-08-10'da
  yazıldı** (01-gereksinim-analizi.md + 02-ui-tasarim.md, kurulmuş üründen türetildi).
- **2026-08-04** — Proje açıldı; K1–K11 kararları kullanıcıyla netleşti (00-analiz §2).
  MediatR v12'de kalma kararı (v13 ticari lisans); iki çalıştırma modu onayı; SaaS-only;
  v1 MSSQL-only; dashboard v1'de; SQLST'den tamamen ayrı ürün. Ürün adı açık — "KDS"
  çalışma adıdır.
