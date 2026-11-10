# LST KDS — Kurulum Kılavuzu (ürün)

> Bu belge **ürünün bir müşteri sunucusuna kurulmasını** anlatır. Tasarımın gerekçesi:
> [docs/07-kurulum-ve-sistem-ayarlari.md](../docs/07-kurulum-ve-sistem-ayarlari.md).
> Geliştirme ortamı (Keycloak vb.) için: [README.md](README.md).

**Temel ilke:** üründe **hiçbir bağlantı dizesi koda/derlemeye gömülü değildir**. Aynı çıktı
(binary/imaj) her müşteride çalışır; yapılandırma dışarıdan gelir.

---

## 1. Yapılandırma nereden okunur? (öncelik sırası)

| # | Kaynak | Ne zaman |
|---|---|---|
| 1 | **Ortam değişkenleri** | Docker / Kubernetes / bulut — "değişmez dağıtım"; dosya hiç kullanılmaz |
| 2 | **Şifreli `kurulum.json`** | Klasik sunucu kurulumu — **kurulum sihirbazı** buraya yazar |
| 3 | `appsettings*.json` | Yalnız geriye uyumluluk (eski kurulum/dev); kullanıldığında uyarı loglanır |
| 4 | Hiçbiri | **Kurulum modu** — uygulama ayakta kalır ve sihirbazı açar |

### Ortam değişkenleri (Docker/K8s)

| Değişken | Zorunlu | Açıklama |
|---|---|---|
| `KDS_METADATA_BAGLANTI` | ✔ | Yönetim veritabanı bağlantı dizesi |
| `KDS_KIMLIK_YETKILI` | ✔ | Keycloak realm adresi (token issuer) |
| `KDS_KIMLIK_ISTEMCI` | ✔ | API audience (ör. `kds-api`) |
| `KDS_ONYUZ_ADRES` | ✔ | Önyüzün adresi (CORS) |
| `KDS_KIMLIK_METADATA` | — | Metadata iç ağ adresi (ters vekil arkasında) |
| `KDS_KIMLIK_HTTPS` | — | `false` ise HTTPS zorunluluğu kapanır (yalnız dev) |
| `KDS_DEPO_BAGLANTI` | — | İçe alım depo SUNUCUSU (DB adı verilmez) |
| `KDS_LOG_BAGLANTI` | — | Olay günlüğü (MongoDB); boş = günlük kapalı |
| `KDS_VERI_DIZINI` | — | Veri dizini (aşağıya bakın) |
| `KDS_KIMLIK_YONETIM_ISTEMCI` | — | Rol yönetimi servis istemcisi (ör. `kds-yonetim`); boş = Roller ekranı "yapılandırılmamış" der |
| `KDS_KIMLIK_YONETIM_SIR` | — | Rol yönetimi istemci sırrı |

---

## 1b. Rol yönetimi istemcisi (`kds-yonetim`) — Roller ekranı için

Roller ekranı (Yönetim → Roles & Yetkiler) rolleri **Keycloak'ta** yönetir; bunun için API'nin
Keycloak Admin API'ye çıkabildiği bir **servis hesaplı** istemci gerekir:

1. Keycloak admin konsolunda `kds` realm'ine **confidential client** ekleyin: Client ID
   `kds-yonetim`, *Client authentication* AÇIK, *Service accounts roles* AÇIK, *Standard flow* ve
   *Direct access grants* KAPALI. Oluşan **client secret**'ı not edin.
2. İstemcinin **Service account roles** sekmesinden `realm-management` istemcisinin şu rollerini
   atayın: `view-realm`, `view-users`, `manage-users`, `manage-realm`.
3. Yapılandırmaya yazın: ortam değişkenleri (`KDS_KIMLIK_YONETIM_ISTEMCI/SIR`) ya da kurulum
   sihirbazı / `appsettings` `Kimlik:YonetimIstemci` + `Kimlik:YonetimSir`.

Yapılandırılmazsa ürünün geri kalanı etkilenmez; yalnız Roller ekranı "rol servisi
yapılandırılmamış" uyarısı gösterir. Dev ortamında `infra/kds-realm.json` bu istemciyi
`kds-yonetim-dev-sir` sırrıyla hazır getirir (yalnız geliştirme içindir).

---

## 2. Veri dizini (kalıcı olmalı)

Varsayılan: Windows `%ProgramData%\LST\KDS`, Linux `/var/lib/kds`. `KDS_VERI_DIZINI` ile değiştirilir.

```
<veri dizini>/
  kurulum.json          → ŞİFRELİ yapılandırma (sihirbazın yazdığı)
  anahtarlar/           → kurulum dosyasının şifreleme anahtarları
  kurulum-anahtari.txt  → yalnız kurulum tamamlanana kadar vardır
```

> **Container kullanıyorsanız bu dizini kalıcı bir birime (volume) bağlayın.** Bağlamazsanız her
> yeniden yaratmada kurulum sıfırlanır.

---

## 3. Kurulum sihirbazı (ekrandan kurulum)

1. Uygulamayı başlatın (yapılandırma yoksa **kurulum moduna** girer, günlüğe `KDS KURULUM MODUNDA` yazar).
2. Sunucudaki `<veri dizini>/kurulum-anahtari.txt` dosyasından **kurulum anahtarını** alın
   (aynı anahtar açılış günlüğüne de yazılır).
3. Önyüzü açın — otomatik olarak sihirbaza yönlenir:
   1. **Kurulum anahtarı**
   2. **Yönetim veritabanı** — sunucu/veritabanı/kimlik → *Bağlantıyı test et*
      (sürüm + tablo oluşturma yetkisi kontrol edilir; veritabanı yoksa "oluşturulacak" der)
   3. **Kimlik sağlayıcı** — Keycloak realm + istemci → *test et*
   4. **Kiracı & adresler** — kiracı adı, (ops.) kiracı kimliği, önyüz adresi, depo sunucusu, Mongo
   5. **Kur** — veritabanı (gerekirse) yaratılır, şema kurulur, ilk kiracı eklenir, yapılandırma
      **şifreli** olarak yazılır
4. Sonunda gösterilen **kiracı kimliğini** Keycloak'ta kullanıcıların `tenant_id` talebine yazın.
5. Kimlik ayarlarının etkinleşmesi için uygulamayı bir kez yeniden başlatın.

**Güvenlik:** kurulum uçları yalnız uygulama kurulmamışken açıktır ve **anahtar** ister; 5 hatalı
denemede kilitlenir. Kurulum bitince uçlar `410 Gone` döner ve anahtar dosyası silinir.

**Gereken veritabanı yetkileri:** şema kurulumu için hedef veritabanında `CREATE TABLE`;
veritabanını sihirbaza yarattıracaksanız sunucuda `CREATE ANY DATABASE`.

---

## 4. Kurulumdan sonra: Sistem Ayarları

**Yönetim → Sistem Ayarları** (Admin): sorgu zaman aşımı/satır limitleri, tanı eşikleri,
depo sunucusu ve DB adı deseni, SMTP (sunucu/port/SSL/kullanıcı/parola/gönderen).

- Değerler **kurulum geneli**dir; kiracıya özel ezme desteklenir (satırda kaynak rozeti görünür).
- Parola/bağlantı gibi **sırlar şifreli** saklanır ve ekrana **geri dönmez** (yalnız "girili" bilgisi).
- Depo sunucusu boşsa kurulumdaki (ortam/dosya) değer kullanılır.
- SMTP boşsa abonelikler **gönderilmez, günlüğe yazılır** (sistem çalışmaya devam eder).

---

## 5. Yedekleme (KRİTİK)

| Ne | Neden |
|---|---|
| **Yönetim veritabanı** | Bağlantılar, haritalar, raporlar, panolar, ayarlar |
| **`<veri dizini>/anahtarlar/`** | Bu dizin kaybolursa `kurulum.json` **okunamaz** |
| `<veri dizini>/kurulum.json` | Yapılandırmanın kendisi |

Anahtar dizini kaybolursa uygulama açıkça hata verir; `kurulum.json` silinip kurulum tekrarlanabilir
(veri kaybı olmaz — yönetim veritabanı yerinde durur, sihirbazda aynı veritabanı gösterilir).

> **Bilinen açık madde:** kayıtlı **bağlantı parolaları** hâlâ çerçevenin *varsayılan* Data
> Protection halkasıyla şifrelidir (kurulum dosyasınınkinden ayrı). Container'da bu halka geçici
> olduğundan kap yeniden yaratılınca bağlantı parolaları okunamaz hâle gelebilir; kalıcı halkaya
> taşıma (mevcut sırların yeniden şifrelenmesi) ayrı bir iş olarak planlanmıştır.

---

## 6. Yükseltme

Yeni sürümü kurun ve başlatın: şema göçleri (migration) açılışta uygulanır. Yapılandırma
dosyası/ortam değişkenleri korunur; kurulum sihirbazı **tekrar açılmaz** (uygulama kurulu görünür).

---

## 7. Geliştirici notu — `dotnet ef`

Tasarım-zamanı komutları artık koda gömülü bağlantı kullanmaz:

```powershell
$env:KDS_METADATA_BAGLANTI = 'Server=(localdb)\MSSQLLocalDB;Database=KdsMetadata;Trusted_Connection=True;TrustServerCertificate=True'
dotnet ef migrations add <Ad> --project src/Kds.Data
dotnet ef database update --project src/Kds.Data
```

Değişken verilmezse komut, yanlış sunucuya şema atmak yerine **açık hata** verir.

## 8. KDS Ajanı — müşteri ağındaki kaynaklar için (S45)

Kaynak MSSQL müşteri ağındaysa ve KDS sunucusu ona ULAŞAMIYORSA (firewall), içe alım işlerini
müşteri tarafında koşturan **KDS Ajanı** kullanılır. Ajan KDS'ye yalnız DIŞARI DOĞRU (HTTPS)
bağlanır; ağda içeri doğru delik açılmaz.

**Güvenlik modeli:** kaynak bağlantı dizeleri/parolalar ajanın makinesindeki `ajan.json`'da
durur ve KDS'ye HİÇ gönderilmez. KDS'deki bağlantı kaydında sunucu/DB alanları bilgi amaçlıdır,
parola boş bırakılır. Ajan anahtarı yalnız ajanı KDS'ye tanıtır; içinde yetki taşımaz
(iptal → Yönetim → Ajanlar, ANINDA etkili).

Kurulum adımları:

1. **KDS → Yönetim → Ajanlar → "Ajan üret"** — üretilen `ajan.json` içeriğini kopyala
   (anahtar bir daha gösterilmez; kaybedilirse ajan iptal edilip yenisi üretilir).
2. **Bağlantılar** ekranında İçe Alım modlu kaynağa bu ajanı seç (parola alanı boş kalsın).
3. Müşteri makinesine `Kds.Ajan` çıktısını kopyala, yanına `ajan.json` koy;
   `baglantilar` bölümüne ajanlı bağlantının kimliğini ve o ağdaki GERÇEK dizeyi yaz.
4. Çalıştır: `Kds.Ajan.exe ajan.json` (Windows servisi/Görev Zamanlayıcı ile kalıcı yapılabilir).

Ajan her `yoklamaSaniye`'de bir işleri sorar (vadesi gelen cron + ekrandan "Şimdi Çalıştır"
tetikleri), tabloları YEREL kaynaktan okur ve depoya iter; artımlı yükleme (S44) ajanla da
aynen çalışır. Ekrandaki "çevrimiçi" çipi son yoklamadan türetilir; iş sürerken kart
"Ajanda koşuyor…" gösterir.
