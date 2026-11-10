# 07 — Kurulum ve Sistem Ayarları (SaaS ürün tasarımı)

**Karar sahibi:** kullanıcı (14 Ağu 2026) · **Kural:** *"Bağımsız olsun, kodda bir connection string
olmasın, her yerde çalışabilsin — bu bir üründür."*

Bu belge, KDS'in **kurulumunun ve çalışma-zamanı ayarlarının** koddan/dosyadan çıkarılıp
**ekrandan yönetilebilir** hâle getirilmesinin tasarımıdır.

---

## 1. Bugünkü durum (14 Ağu 2026 itibarıyla, koddan doğrulandı)

| Değer | Bugün nereden geliyor | Sorun |
|---|---|---|
| Yönetim DB (`KdsMetadata`) | `ConnectionStrings:KdsMetadata` (appsettings/env) — yoksa açılışta patlar | Dağıtımda dosya düzenlemek gerekir; ekran yok |
| Depo sunucusu | `ConnectionStrings:KdsDepo` | Aynı |
| Olay günlüğü (Mongo) | `ConnectionStrings:KdsLog` (boşsa no-op) | Aynı |
| Kimlik (Keycloak) | `Kimlik:Yetkili` / `Istemci` / `MetadataAdresi` | Aynı |
| Önyüz adresi (CORS) | `Onyuz:Adres` | Aynı |
| Önyüzün API adresi | `VITE_API_ADRES` (derleme zamanı) | Derlemeye gömülü |
| **Tasarım-zamanı DB** | `KdsDbContextFactory` içinde **sabit LocalDB dizesi** | Koda gömülü bağlantı — kaldırılacak |
| E-posta gönderen | `EpostaAyarlari.Gonderen = "kds@localhost"` varsayılanı | Müşteride yanlış görünür |
| Depo DB adı | `IceAlimAyarlari.DepoDbAdi()` → `KdsDepo_<tenant>` (kod) | Ad koddan üretiliyor (sunucu config'ten) |
| Sorgu zaman aşımı / satır tavanı | `RunQueryHandler` sabitleri (60 sn · 1000 · 10000) | Müşteriye göre değişmeli |
| Tanı eşikleri (M33) | `QueryPlanRules` + `SqlQueryDiagnostician` sabitleri (500K · 100K · 30 sn) | Aynı |
| "Yavaş sorgu" eşiği | Önyüzde 10 sn | Aynı |

> Not: C#'taki `static` **anahtar sözcüğü** (ör. `QueryPlanRules`) "durumsuz yardımcı sınıf"
> demektir; içinde ortam bilgisi yoktur ve dağıtım riski değildir. Bu belgenin konusu
> **ortama bağlı değerler**dir.

---

## 2. Hedef ilkeler

1. **Kodda bağlantı dizesi yok** — tasarım-zamanı factory dahil.
2. **Her yerde çalışır:** IIS, Windows servisi, Docker/K8s, bulut. Dağıtım biçimi ayarların
   *kaynağını* değiştirir, kodu değil.
3. **Yapılandırılmamış uygulama çöker değil, KURULUM MODUNA girer** — admin ekrandan yapılandırır.
4. **Sır düz metin yazılmaz:** parolalar Data Protection ile şifreli.
5. **Kurulum modu ele geçirilemez:** tek kullanımlık kurulum anahtarı zorunlu.
6. **Mevcut kurulumlar kırılmaz:** config'te bağlantı varsa o kullanılmaya devam eder (göç yolu §7).

---

## 3. Yapılandırma kaynak sırası (öncelik)

```
1) Ortam değişkenleri        →  KDS_METADATA_BAGLANTI, KDS_KIMLIK_YETKILI, ...
   (Docker/K8s/bulut: değişmez dağıtım; dosya hiç kullanılmaz)
2) Şifreli kurulum dosyası   →  <veriDizini>/kurulum.json
   (klasik sunucu kurulumu; sihirbaz buraya yazar)
3) appsettings (GEÇİŞ)       →  ConnectionStrings:* — sadece geriye uyumluluk (§7)
4) Hiçbiri yok               →  KURULUM MODU (sihirbaz)
```

**Veri dizini:** `KDS_VERI_DIZINI` env'i; yoksa Windows `%ProgramData%\LST\KDS`,
Linux `/var/lib/kds`. İçinde: `kurulum.json`, `anahtarlar/` (kurulum dosyasının halkası),
`anahtarlar-uygulama/` (uygulama geneli halka — bağlantı parolaları), `kurulum-anahtari.txt`
(yalnız kurulum bitene kadar).

**Şifreleme:** ASP.NET Core Data Protection, purpose `"KDS.Kurulum.v1"`. Anahtarlar
`<veriDizini>/anahtarlar` altında **kalıcı**.

> **Karar (A1'de alındı):** kurulum dosyasının anahtar halkası, uygulama genelindeki halkadan
> **AYRIDIR** (`SetApplicationName("LST-KDS-Kurulum")`, DI'dan bağımsız `DataProtectionProvider`).
> Sebep: kurulum dosyası uygulama daha ayağa kalkmadan okunur; iki halkayı ayırmak birinin
> kaybının diğerini kilitlememesini sağlar.

**Uygulama geneli halka — bağlantı parolaları (açık 17 Ağu 2026'da kapatıldı):**
`PasswordProtector` eskiden çerçevenin *varsayılan* halkasını kullanıyordu (Windows'ta kullanıcının
`%LOCALAPPDATA%`'sı, container'da `~/.aspnet`). Container yeniden yaratıldığında burası **uçuyor**,
kayıtlı bağlantı parolaları çözülemez hâle geliyordu. Artık halka
`<veriDizini>/anahtarlar-uygulama` altındadır (`PersistKeysToFileSystem`, Program.cs) — compose'da
`kds-veri` birimiyle kalıcıdır.

- **Uygulama adı (discriminator) bilerek DEĞİŞTİRİLMEDİ:** değiştirilseydi aynı anahtarlarla bile
  eski şifreli değerler çözülemezdi. Bedeli: halka uygulamanın *içerik kök yoluna* bağlıdır
  (container'da `/app` — sabit).
- **Göç kayıpsız:** açılışta yeni dizin BOŞsa ve eski konumda anahtar varsa anahtarlar
  **kopyalanır** (taşınmaz — geri dönüş mümkün kalır) ve bir log satırıyla bildirilir.
  Kanıt: dev makinede 1 anahtar kopyalandı, mevcut parolalar çözülmeye devam etti.
- **Kanıt (container):** compose ile kalkan API'de anahtar `/var/lib/kds/anahtarlar-uygulama`
  altında oluştu; container **silinip yeniden yaratıldıktan sonra aynı anahtar** yerindeydi.
- **Bilerek açık kalan:** Linux/container'da anahtar dosyaları diskte *şifresiz* durur
  (`No XML encryptor configured` uyarısı) — çerçevenin standart davranışıdır; korunma dosya
  izinleri/birim şifrelemesiyledir. Sertifika ya da vault ile şifreleme araştırma kuyruğunda
  (00-analiz §7).

⚠️ `<veriDizini>/anahtarlar` yedeklenmelidir: kaybı = kurulum dosyasının okunamaması (uygulama
açıkça hata verir; dosya silinip kurulum tekrarlanabilir).

---

## 4. Mimari

### 4.1 Önyükleme (`Kds.Api/Kurulum/`)

- **`KurulumDeposu`** — kaynak sırasını uygular; `Oku()` / `Yaz(...)`; dosyayı Data Protection ile
  şifreler/çözer.
- **`KurulumDurumu`** (singleton) — `Yapilandirildi`, `MetadataBaglantisi`, `Yenile()`.
  Kurulum bitince `Yenile()` çağrılır → **uygulamayı yeniden başlatmaya gerek yok**.

### 4.2 Çalışma-zamanı bağlantısı (asıl mimari değişiklik)

Bugün `AddKdsData(dize)` bağlantıyı **açılışta closure'a hapsediyor**. Değişecek:

```csharp
// Kds.Data
public interface IMetadataBaglantiSaglayici { string Dize(); }   // yapılandırılmamışsa KurulumGerekliException

servisler.AddDbContext<KdsDbContext>((sp, s) =>
    s.UseSqlServer(sp.GetRequiredService<IMetadataBaglantiSaglayici>().Dize()) ...);
```

DbContext seçenekleri **her scope'ta** kurulduğu için bağlantı çalışma zamanında değişebilir.
`KurulumGerekliException` → `503 + { kurulumGerekli: true }` (ExceptionHandler).

### 4.3 Kurulum uçları — `KurulumController` (`/api/kurulum`, `[AllowAnonymous]`)

| Uç | İş | Anahtar ister mi |
|---|---|---|
| `GET /durum` | `{ yapilandirildi, surum }` | Hayır (yalnız bool) |
| `POST /veritabani-test` | Verilen sunucu/DB/kimlikle bağlanır, sürüm + yetki döner | Evet |
| `POST /kimlik-test` | Keycloak metadata erişilebilir mi | Evet |
| `POST /tamamla` | Şema kur (`Migrate()`) → ilk kiracı + admin → `kurulum.json` yaz → `Yenile()` | Evet |

**Güvenlik:** `X-Kurulum-Anahtari` başlığı zorunlu. Anahtar, uygulama **yapılandırılmamışken**
açılışta üretilir; loga **ve** `<veriDizini>/kurulum-anahtari.txt` dosyasına yazılır (sunucuya
erişebilen = kurabilen). Kurulum tamamlanınca: uçlar `410 Gone`, anahtar dosyası silinir.
5 hatalı denemeden sonra kurulum uçları kilitlenir (log + yeniden başlatma gerekir).

### 4.4 Kurulum sihirbazı (önyüz `/kurulum`)

Açılışta `GET /api/kurulum/durum` → `yapilandirildi:false` ise sihirbaza yönlendirilir (menü/oturum yok).

1. **Kurulum anahtarı** (sunucudaki dosyadan/logdan)
2. **Yönetim veritabanı** — sunucu · veritabanı · kimlik (Windows/SQL) → **Test et** (sürüm + yetki gösterilir)
3. **Kimlik sağlayıcı** — Keycloak adresi + istemci → **Test et**
4. **İlk kiracı + yönetici** — kiracı adı (SaaS: müşteri), admin kullanıcı eşlemesi
5. **Özet → "Kur"** — şema kurulur, kayıtlar atılır, dosya şifreli yazılır → giriş ekranı

Önyüzün API adresini derlemeye gömmemek için: `VITE_API_ADRES` boşsa **aynı origin** kullanılır
(nginx/ters vekil arkasında ürün varsayılanı budur).

### 4.5 Sistem Ayarları (yönetim DB'sinde, ekrandan)

Yeni tablo **`SistemAyarlari`** (`Anahtar`, `Deger`, `Tip`, `SirMi`, `TenantId?`): `TenantId` null =
kurulum geneli, dolu = kiracıya özel (SaaS'ta kiracı kendi eşiklerini ezebilir). `SirMi` olanlar
Data Protection ile şifreli saklanır, API'ye **asla** düz dönmez.

**Ekran:** Yönetim → **Sistem Ayarları**, sekmeler:

| Sekme | Ayarlar |
|---|---|
| Depo | İçe alım depo sunucusu (bağlantı), varsayılan depo DB adı deseni (`KdsDepo_{tenant}`) |
| E-posta | SMTP sunucu/port/TLS/kimlik, **gönderen adresi (zorunlu)** |
| Sorgu | Zaman aşımı (60 sn), varsayılan satır (1000), sert tavan (10000) |
| Tanı (M33) | Plan zaman aşımı (30 sn), tarama eşiği (500K), sıralama eşiği (100K), "yavaş" eşiği (10 sn) |
| Günlük | Mongo bağlantısı (boş = kapalı) |

**Backend:** `ISistemAyarlari` (bellek-cache'li; yazımda invalidate). Tüketiciler bugünkü
sabitlerden buraya bağlanır: `IceAlimAyarlari`, `RunQueryHandler`, `SqlQueryDiagnostician`,
`QueryPlanRules` (eşikler parametreye döner), `EpostaAyarlari`. Önyüz eşikleri
`GET /api/ayarlar/onyuz` ile alınır (oturum açılışında bir kez).

---

## 5. Çok kiracılılık (SaaS) etkisi

- Kiracı bugün JWT `tenant_id` claim'inden geliyor (`HttpTenantContext`) — bu **doğru** ve korunur.
- Sihirbaz **ilk** kiracıyı yaratır; sonraki kiracılar Yönetim → Kiracılar ekranından (ayrı iş).
- Kiracıya özel ayar (`SistemAyarlari.TenantId`) sayesinde "müşteri A'nın sorgu zaman aşımı 120 sn"
  gibi ihtiyaçlar kod değişmeden karşılanır.
- Depo hedefi zaten kiracı bazlı seçilebiliyor (M22b — bağlantı düzeyinde `DepoBaglantiId`).

---

## 6. Kaldırılacak kod-gömülü değerler (kontrol listesi)

- [x] `KdsDbContextFactory` sabit LocalDB dizesi → `KDS_METADATA_BAGLANTI` env'i; yoksa **açık hata**
      (A1; bu yalnız `dotnet ef` içindir)
- [x] `EpostaAyarlari.Gonderen = "kds@localhost"` → Sistem Ayarları (A3; sınıf tümüyle kaldırıldı,
      SMTP her gönderimde ayardan okunur)
- [x] `IceAlimAyarlari.DepoDbAdi` deseni → ayar (A3 · `Depo.DbAdiDeseni`, `{kiraci}` yer tutucusu)
- [x] Depo sunucusu bağlantısı → ayar (A3 · sır; öncelik ayar → kurulum)
- [x] `RunQueryHandler`: 60 sn / 1000 / 10000 → ayar (A3)
- [x] `SqlQueryDiagnostician` 30 sn + `QueryPlanRules` 500K/100K → ayar (A3; kural motoru eşiği
      parametre alır, saf kaldı)
- [x] Önyüz "yavaş" eşiği 10 sn → ayar (A3 · `/api/ayarlar/onyuz` + `useYavasEsik`)
- [x] Mongo (olay günlüğü) → kurulum düzeyi (A1 env/kurulum.json + A2 sihirbaz alanı). Bilinçli
      karar: altyapı bağlantısı, işletme ayarı değil — metadata DB ile aynı yerde yaşar
- [ ] `appsettings*.json` bağlantıları → yalnız geçiş yolu; ürün dağıtımında gerekmez (dev
      kolaylığı için duruyor; kaldırma A4'te belgelenecek)

---

## 7. Göç (mevcut kurulum/dev makinesi kırılmasın)

Açılış mantığı:

```
env var?        → kullan (dosyaya dokunma)
kurulum.json?   → kullan
appsettings'te ConnectionStrings:KdsMetadata? → KULLAN + "eski yapılandırma" uyarısı logla
                  (Sistem Ayarları'nda "Kurulumu dosyaya taşı" düğmesi)
hiçbiri         → KURULUM MODU
```

Böylece bugünkü dev makinesi ve S13 Compose dağıtımı **olduğu gibi çalışmaya devam eder**.

---

## 7b. Veritabanı sunucusu ayarı — RAPOR HIZI İÇİN ZORUNLU *(24 Ağu 2026, ölçümle bulundu)*

KDS ile **yerel model (Ollama) aynı makinede** koşuyorsa SQL Server'a alt bellek sınırı verilmelidir.
Yoksa model belleğe girdiğinde (7B ≈ 5 GB) SQL Server önbelleğini bırakır ve her rapor tabloyu
yeniden diskten okur.

```sql
EXEC sp_configure 'show advanced options', 1;  RECONFIGURE;
EXEC sp_configure 'min server memory (MB)', 2048;  RECONFIGURE;   -- veritabanı bunu ASLA bırakmaz
EXEC sp_configure 'max server memory (MB)', 4096;  RECONFIGURE;   -- modele yer kalsın
```

**Ölçüm (2M satırlık tablo, aynı sorgu, ardışık ayrı bağlantılar):** model bellekteyken
`min server memory = 16 MB` (LocalDB varsayılanı) ile **24 230 ms**; 2 GB garantiyle
**85 / 86 / 99 ms**. CPU her iki durumda da ~130 ms — aradaki fark tamamen disk beklemesidir.
Değerler makinenin toplam RAM'ine göre ayarlanır; kural: *veri kümesinin sıcak kısmı sığacak kadar
min, modele yer bırakacak kadar max*.

İlgili ve ayrı bir konu: içe alım deposu artık **`AUTO_CLOSE OFF`** ile yaratılır
(`SqlIngestRunner`); LocalDB'de `CREATE DATABASE` ile doğan veritabanı aksi hâlde son bağlantı
kapanınca kapanıp önbelleğini atıyordu.

---

## 7c. Gömme (embed) — dağıtım gereksinimi *(S42, 26 Ağu 2026)*

Gömülü rapor sayfası (`/gomulu`) müşterinin sitesinde bir **iframe** içinde açılır. Tarayıcının buna
izin vermesi için ön yüzü sunan katman (nginx / reverse proxy / statik sunucu) doğru
`Content-Security-Policy` başlığını vermelidir:

```nginx
# Gömülü sayfa: YALNIZ anahtarlarda tanımlı alan adları çerçeveleyebilsin
location /gomulu {
    add_header Content-Security-Policy "frame-ancestors https://portal.firma.com https://bayi.firma.com";
}

# KDS'in kendi ekranları: hiçbir yerde çerçevelenmesin (clickjacking koruması)
location / {
    add_header Content-Security-Policy "frame-ancestors 'none'";
}
```

**Neden sunucu tarafında:** `frame-ancestors` yalnız HTTP başlığıyla çalışır (meta etiketiyle
geçersizdir) ve tarayıcı kararıdır. Uygulama içindeki izinli-alan denetimi ayrı bir katmandır:
o, **veriyi** korur (`/api/gomulu/calistir` Origin'i anahtarın listesiyle karşılaştırır); CSP ise
**sayfanın çerçevelenmesini** kısıtlar. İkisi birbirinin yerine geçmez.

Anahtardaki izinli alan listesi değişirse bu başlık da güncellenmelidir — aksi hâlde anahtar geçerli
olsa bile tarayıcı iframe'i engeller.

---

## 8. Aşamalar (uygulama sırası; her biri ayrı commit + kullanıcı testi)

| Aşama | Kapsam | Neden bu sıra |
|---|---|---|
| **A1** | Önyükleme çekirdeği: veri dizini, Data Protection kalıcı anahtarlar, `KurulumDeposu`, `KurulumDurumu`, `IMetadataBaglantiSaglayici` + lazy `AddKdsData`, `KurulumGerekliException`→503, tasarım-zamanı factory temizliği | Diğer her şey buna dayanıyor; kodda dize bırakmama sözünü burada tutuyoruz |
| **A2** | Kurulum uçları + kurulum anahtarı + sihirbaz ekranı (`/kurulum`) | "Admin panelden bağlantı girer" isteğinin kalbi |
| **A3** | `SistemAyarlari` tablosu + Yönetim → Sistem Ayarları ekranı + tüketicilerin bağlanması (depo, SMTP, sorgu, tanı, günlük) | Kalan kod-gömülü değerler buradan çıkar |
| **A4** | Kurulum kılavuzu (`infra/README`), yedekleme notu (anahtar dizini), E2E: sıfırdan kurulum senaryosu | Ürünleşme: kurulumu belgelenmemiş ürün eksiktir |

**Durum (14 Ağu 2026): A1·A2·A3·A4 tamamlandı** — ayrıntı ve kanıtlar roadmap S16 satırında.
A4'te "E2E" için Playwright yerine **entegrasyon testi** seçildi (`KurulumHizmetiTests`): kurulum
akışı ikinci bir API örneği + ayrı veri dizini gerektirdiğinden tarayıcı testi kırılgan olurdu;
sihirbaz arayüzü A2'de gerçek tarayıcıda elle doğrulandı. Compose ürün yoluna (`KDS_*` +
kalıcı veri birimi) çevrildi; Docker bu makinede olmadığı için **statik** doğrulandı.

**Riskler / dikkat:**
- Data Protection anahtar dizini kaybı → kurulum dosyası okunamaz (belgeye yaz, yedekle).
- Kurulum modu açık portta durur → anahtar zorunluluğu + tek kullanım + kilit.
- Kurulum sırasında migration çalıştırma yetkisi: hedef DB kullanıcısı **DDL** yetkili olmalı;
  sihirbazın "Test et" adımı bunu kontrol edip söyler.
