# 04 · Veri Modeli (Metadata DB)

> Metadata dünyasının (00-analiz §6) şeması. **İlke:** bu DB ürünün *içeriğini ve
> yapılandırmasını* tutar; **tutmaz:** parola/kimlik (Keycloak'ta), müşterinin gerçek iş
> verisi (hedef DB / tenant içe-alım deposunda), uygulama yapısı/rota/statik menü (kodda).
> Çok-kiracılık: tek DB + `TenantId` + EF global query filter; içe-alım deposu tenant
> başına ayrı DB (00-analiz §8).

## Durum: ne kuruldu, ne bekliyor

| Grup | Tablolar | Durum |
|---|---|---|
| **Tenant** | `Tenantlar` (Ad, Durum) | ✅ kuruldu (S3) |
| **Kullanıcı** | `Kullanicilar` (KeycloakId, TenantId, KullaniciAdi, EPosta, AdSoyad, SonGirisTarihi, AnaSayfaPanoId) | ✅ kuruldu (S3; `AnaSayfaPanoId` S43) |
| **Bağlantılar** | `Baglantilar` (sunucu/DB/şifreli kimlik, veri alma modu, son test) | ✅ kuruldu (S4) |
| **Veri haritası** | `VeriHaritalari`, `HaritaTablolar`, `HaritaKolonlar`, `HaritaIliskiler` | ✅ kuruldu (S6) |
| **Sorgu & rapor** | `Raporlar`, `RaporKlasorleri`, `RaporFavorileri` (JSON sorgu modeli + görselleştirme; sürümleme ileride) | ✅ kuruldu (S10) |
| **Dashboard** | `Panolar` (grid layout), `PanoOgeleri` | ⬜ S11 |
| **İçe alım** | `IceAlimIsleri` (cron + son durum/süre/satır) | ⬜ S12 |
| **Denetim** | `DenetimKayitlari` (tenant/kullanıcı?/sorgu/bağlantı/zaman/satır/ms/başarılı/hata) | ✅ kuruldu (S5) |
| **Yetki (ince)** | `KaynakYetkileri` (rapor/pano/klasör bazlı kim görür/düzenler; v1: Rapor.Paylasim Ozel/Ekip) | ⬜ sonraki |

> Her grup, hesaplanmış kolon kararları (ör. `Rapor` JSON şeması S7'de, `Baglanti`
> şifreleme S4'te, `DenetimKaydi` alanları S5'te) kendi diliminde netleşeceği için o
> dilimde eklenir — şimdi spekülatif tablo açıp sonra elden geçirmek yerine.

## Kurulan çekirdek (S3)

```
Tenant (Kiracı/Müşteri)                Kullanici
├─ Id (Guid) = Keycloak tenant_id      ├─ Id (Guid)
├─ Ad                                  ├─ TenantId ──FK──► Tenant.Id  (Cascade)
├─ Durum (Aktif/Deneme/Askida)         ├─ KeycloakId (Guid, UNIQUE) = token 'sub'
└─ OlusturmaTarihi / GuncellemeTarihi   ├─ KullaniciAdi / EPosta / AdSoyad
                                        ├─ SonGirisTarihi
   1 ──────────────< N                  ├─ AnaSayfaPanoId (Guid?, FK YOK — S43)
                                        └─ OlusturmaTarihi / GuncellemeTarihi
```

- **`AnaSayfaPanoId` (S43):** kullanıcının ana sayfasında açılacak pano; null = seçim yok.
  Panoya **bilerek FK yok** — pano silinse ya da paylaşımı daralsa da kullanıcı kaydı bozulmaz;
  görünürlük her okumada yeniden denetlenir, düşen seçim sessizce karşılama görünümüne düşer
  (kayıt temizlenmediği için paylaşım geri açılınca tercih yerinde durur). Tekil tercih olduğu
  için ayrı tabloya gerek yok — çoklu olan `RaporFavorileri` ayrı tabloda.

- **Ortak temel `Varlik`:** `Id` + `OlusturmaTarihi`/`GuncellemeTarihi` (SaveChanges'te
  otomatik damgalanır).
- **`ITenantVarlik`:** `TenantId` taşıyan varlıklar; global query filter ve eklemede
  TenantId damgalama bunlara uygulanır. (`Kullanici` uygular; `Tenant` kendisi hariç.)
- **Tohum (dev):** iki tenant — `Deneme A.Ş.` (1111…) ve `Örnek Ltd.` (2222…) —
  Keycloak realm'indeki `tenant_id`'lerle birebir (infra/kds-realm.json). HasData ile
  migration'a gömülü.

## Kurulan: Bağlantılar (S4)

```
Baglanti (Kaynak DB bağlantı profili) : Varlik, ITenantVarlik
├─ Id (Guid)
├─ TenantId ──filter/damga──►  (çok-kiracılık)
├─ Ad
├─ Sunucu / Veritabani
├─ KullaniciAdi (nullable; boş = entegre/Windows kimlik)
├─ SifreSifreli (Data Protection ile şifreli; boş = parolasız)
├─ Mod (VeriAlmaModu: Canli | IceAlim — string olarak saklanır)
├─ SonTestBasarili? / SonTestMs? / SonTestTarihi?   (son bağlantı denemesi)
└─ OlusturmaTarihi / GuncellemeTarihi
   İndeks: (TenantId, Ad)
```

- **Parola (00-analiz §7):** düz metin hiçbir yerde durmaz. `ISifreKoruyucu` (Business arayüzü)
  → `SifreKoruyucu` (Api, ASP.NET Data Protection) ile şifrelenir; `BaglantiOzeti` parolayı
  **hiçbir uçtan döndürmez**.
- **Veri alma modu (K11 revize, 2026-08-06):** yalnız **bağlantı düzeyinde** belirlenir; veri
  haritası bu modu **devralır** (harita düzeyi override kaldırıldı — kullanıcı gereksiz buldu:
  mod zaten bağlantının özelliği). Enum, okunabilir migration için **string** kolonda.
- **Test Et = çalıştırma dünyası:** `Kds.Calistirma/IBaglantiDenetci` + `SqlBaglantiDenetci`
  (saf ADO.NET, Microsoft.Data.SqlClient; kısa timeout + `SELECT 1`; Kds.Dal'a referans vermez).
  Kullanıcı boşsa entegre kimlik (LocalDB'ye uygun). `SonTest*` alanları, kayıtlı bağlantı test
  edildiğinde (test-by-id) güncellenir; UI durum çipi bunu gösterir.
- **Paylaşılan enum:** `VeriAlmaModu` Contracts'ta (Dal→Contracts) — hem entity hem DTO aynı tipi kullanır.
- **Yetki:** uçlar yalnız **Admin + Geliştirici** (`[Authorize(Roles=...)]`).
- **Kanıt (canlı + test):** LocalDB'ye gerçek bağlantı (entegre kimlik) canlı doğrulandı; birim
  testleri: şifre round-trip + tenant damgası, liste izolasyonu, güncelle-şifre-koru, test-by-id
  işleme, sil. **12/12 test yeşil.**

## Kurulan: Denetim (S5)

```
DenetimKaydi (Sorgu icra denetimi) : Varlik, ITenantVarlik
├─ Id · TenantId ──filter/damga──►
├─ KullaniciId? (kim — kullanıcı bağlamlı icra ucu S8'de doldurur)
├─ BaglantiId? (yumuşak referans; bağlantı silinse de denetim kalır)
├─ Sql (nvarchar(max) — çalıştırılan metin)
├─ Basarili · SatirSayisi? · SureMs · Hata? (nvarchar(1000))
└─ OlusturmaTarihi = ne zaman
   İndeks: (TenantId, OlusturmaTarihi)
```

- **Başarılı VE engellenmiş** her icra yazılır (güvenlik: reddedilen/başarısız denemeler de görünür).
  Kayıt MediatR **pipeline davranışıyla** (`SorguDenetimDavranisi`) bırakılır — hata yutulmaz, kaydedilir
  ve yükseltilir.
- Parola / gerçek iş verisi TUTULMAZ; yalnız SQL metni + ölçümler.
- **Çalıştırma çekirdeği** (kod, şema değil — 00-analiz §6-§7): SELECT-only bekçisi
  (`ScriptDomSorguBekcisi`, AST beyaz-liste) + `SqlSorguCalistirici` (tek kapı: bekçi + timeout +
  satır sınırı) + `SqlSemaKesfi` — hepsi `Kds.Calistirma`'da, Kds.Dal'a referans vermez.

## Kurulan: Veri Haritası (S6)

```
VeriHaritasi (Kullanıma hazır tablo kümesi) : Varlik, ITenantVarlik
├─ Id · TenantId ──filter/damga──►
├─ Ad
├─ BaglantiId (yumuşak referans — hangi bağlantı üstünde)
├─ Mod (VeriAlmaModu — bağlantıdan devralınır)
├─ Tablolar[]  ──cascade──►  HaritaTablo
│                            ├─ Id (İSTEMCİ üretir; ilişkiler bu Id'ye referans verir)
│                            ├─ Sema / HamAd / DostcaAd (özelleştirilmiş isim)
│                            └─ Kolonlar[] ──cascade──► HaritaKolon
│                                                       ├─ HamAd / DostcaAd? / VeriTipi?
│                                                       └─ Rol (KolonRolu: Boyut|Olcu|Anahtar|Gizli — string)
└─ Iliskiler[] ──cascade──► HaritaIliski
                            └─ KaynakTabloId/Kolon → HedefTabloId/Kolon · FktenMi
   İndeks: (TenantId, Ad) · Yalnız kök tenant-filtreli; alt varlıklar kök üzerinden yüklenir.
```

- **Aggregate (kök = VeriHaritasi):** alt varlıklar (tablo/kolon/ilişki) yalnız kök üzerinden
  yüklenir/kaydedilir; kök tenant-filtreli, alt varlıklar cascade silinir. `HaritaIliski`'nin
  tablo referansları **yumuşak** (FK kısıtı yok — çoklu cascade yolu karmaşasını önler).
- **Tablo Id'lerini istemci üretir** (ilişkiler kararlı Id'lere bağlansın diye). Kaydetmede
  (güncelleme) alt varlıklar tümüyle değiştirilir: eski çocuklar silinir (ayrı SaveChanges →
  tracker'dan düşer) sonra yeni çocuklar **açıkça `Add`**'lenir. ⚠ Tuzak (düzeltildi 2026-08-06):
  kök *takipli-ama-Added-değil* olduğundan istemci-Id'li yeni çocuklar EF'te "mevcut kayıt"
  sanılıp UPDATE→0 satır (`DbUpdateConcurrency`) atıyordu; açık `Add` ile çözüldü + LocalDB
  round-trip regresyon testi (yeni + güncelleme).
- **Şema keşfi (canlı, S5 çalıştırma dünyası):** `Kds.Calistirma/ISemaKesfi` 3 uç — tablo ara
  (`sys.tables` LIKE + top-N; 600+ tabloyu ölçekler), kolon getir (tembel), FK ilişki önerisi
  (`sys.foreign_keys`). `BaglantiDizesiCozucu` (Business) bağlantıyı çözer + parolayı deşifre eder.
- **Kolon "kullanımı" (Rol):** son kullanıcıya sade dille — Kırılım (boyut), Sayısal (ölçü),
  Anahtar, Gizli. Varsayılan sezgisel: PK/`id`→Anahtar, sayısal tip→Ölçü, diğer→Boyut.
- **Yetki:** yalnız **Admin + Geliştirici** (`[Authorize(Roles=...)]`). **50/50 test** (round-trip +
  şema keşfi LocalDB canlı).

## Kurulan: Raporlar (S10)

```
Rapor (Kayıtlı rapor) : Varlik, ITenantVarlik
├─ Id · TenantId ──filter/damga──►
├─ Ad · KlasorId? (yumuşak) · HaritaId (yumuşak)
├─ ModelJson (nvarchar(max) — SorguModeli JSON'u: camelCase + string enum + polimorfik 'tur')
├─ Gorsellestirme (Tablo|Cubuk|Cizgi|Pasta|SayiKarti — string)
├─ Paylasim (Ozel|Ekip — string) · SahipId (local Kullanici.Id, yumuşak)
└─ Favoriler[] ──cascade──► RaporFavori (RaporId + KullaniciId, unique)
RaporKlasoru : Varlik, ITenantVarlik — düz liste (v1), (TenantId, Ad) unique
   İndeksler: Rapor(TenantId, Ad) · Rapor(TenantId, KlasorId)
```

- **Rapor MODEL saklar, SQL değil** (00-analiz §5): açılınca model güncel haritayla yeniden
  parametreli SQL'e çevrilir; harita silinirse açılışta anlaşılır hata (rapor kaybolmaz).
- **Görünürlük:** Özel = sahibi + admin; Ekip = tenant'taki herkes (musteri dahil — görüntüleyici
  musteri rolüne açık, tasarım/kaydetme Admin+Geliştirici). Karar mercii `IKullaniciBaglami`
  (Business arayüzü; Api'de token'dan `sub` + roller).
- **Kanıt:** round-trip (model JSON dahil), görünürlük (sahip/başkası/admin), favori toggle,
  klasör-sil→klasörsüz testleri; canlı migration + uçlar doğrulandı.

## Kimlik ↔ Keycloak bağı

- Keycloak kimliğin tek doğruluk kaynağı (parola, kaba roller). Local `Kullanici` tablosu,
  kendi verimizde kullanıcıya referans (rapor sahibi, audit, ince yetki) verebilmek için.
- Bağ: `Kullanici.KeycloakId` = token'daki `sub`. Gerçek cross-DB FK değil ama benzersiz.
- **JIT provisioning:** kimliği doğrulanmış her API isteğinde (middleware, kısa önbellekli),
  kullanıcı yoksa oluşturulur — parola asla kopyalanmaz. Eşzamanlı ilk-istek yarışı
  (unique ihlali) yakalanıp mevcut kayda düşülür.

## Çok-kiracılık mekaniği

- **`ITenantBaglami`** (Dal arayüzü): mevcut isteğin `tenant_id`'si. Gerçekleştirme
  `HttpTenantBaglami` (Api) token claim'inden okur — Dal, ASP.NET bağımlılığı taşımaz.
- **Global query filter:** `ITenantVarlik` sorguları otomatik `TenantId == MevcutTenantId`
  ile süzülür. JIT'in varlık kontrolü `IgnoreQueryFilters` ile (KeycloakId global benzersiz).
- **Kanıt (canlı + test):** admin/gelistirici (Tenant A) birbirini görür, musteri
  (Tenant B) yalnız kendini; birim testleri: global-filter izolasyonu + JIT idempotensi.

## Migration / şema yönetimi

- EF Core migration'ları `Kds.Dal/Migrations/`'ta: `IlkMetadata` (S3), `BaglantiEkle` (S4),
  `DenetimKaydiEkle` (S5), `VeriHaritasiEkle` (S6), `RaporEkle` (S10).
- Tasarım-zamanı: `KdsDbContextFactory` (boş tenant bağlamı).
- Geliştirmede API açılışında `Database.Migrate()` otomatik uygular (LocalDB `KdsMetadata`).
- Üretimde migration'lar dağıtım adımıyla uygulanır (otomatik-migrate dev'e özel).
