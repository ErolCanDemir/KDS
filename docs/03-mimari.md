# 03 · Mimari

> Bağlayıcı kararların kaynağı [00-analiz.md](00-analiz.md) (K1–K11). Bu doküman o
> kararların koda nasıl yansıdığını anlatır: §1–6 backend, §8 kimlik, §9 frontend,
> §10 geliştirme altyapısı, §11 kod standartları.

## 1. Çözüm yapısı ve bağımlılık grafiği

```
KDS.slnx
├─ src/
│  ├─ Kds.Api          ASP.NET Core Web API — controller'lar + DI kompozisyon kökü
│  ├─ Kds.Contracts    Komut/sorgu sözleşmeleri + yanıt kayıtları (yalnız MediatR.Contracts)
│  ├─ Kds.Business     CQRS handler'ları + pipeline davranışları (MediatR 12.5.0 + FluentValidation)
│  ├─ Kds.Dal          Metadata dünyası: EF Core DbContext + ince UnitOfWork
│  └─ Kds.Calistirma   Çalıştırma dünyası: sorgu icra sözleşmeleri (S5'te gerçekleştirme)
└─ tests/
   └─ Kds.Tests        xUnit — CQRS hattı, doğrulama davranışı, UnitOfWork

Bağımlılık yönü:   Api ──► Business ──► { Contracts, Dal, Calistirma }
```

**Bağımlılık kuralları (derlemede korunur, gevşetilmez):**

1. **`Kds.Calistirma` ↔ `Kds.Dal` arasında referans YASAK** — iki veri dünyası ayrımı
   (00-analiz §6). Çalıştırma dünyası metadata DbContext'ini göremez; metadata dünyası
   sorgu icra edemez. İkisini yalnız Business handler'ları orkestre eder.
2. `Kds.Contracts` yalnız `MediatR.Contracts` paketini bilir — EF, ASP.NET, FluentValidation
   Contracts'a giremez. Frontend'e üretilecek istemci tipleri de buradan türeyecek.
3. `Kds.Api` iş mantığı içermez: controller = `ISender.Send()` çağrısı + HTTP kaygıları.
   Tüm servis kayıtları `AddKdsBusiness()` / `AddKdsDal()` uzantılarından gelir.

## 2. CQRS hattı

```
Controller ──► ISender.Send(istek)
                 │
                 ▼
           DogrulamaDavranisi<TIstek,TYanit>   (FluentValidation; hata → ValidationException,
                 │                              handler hiç çalışmaz)
                 ▼
           IRequestHandler (Business)
                 │
        ┌────────┴─────────┐
        ▼                  ▼
   IUnitOfWork        ISorguCalistirici / ISemaKesfi
   (metadata dünyası)  (çalıştırma dünyası — S5)
```

- Sözleşme işaretleri: `IKomut<TYanit>` (durum değiştirir) / `ISorgu<TYanit>` (okur) —
  ikisi de `IRequest<TYanit>` üstüne ince işaret arayüzü. Handler'lar Business'ta,
  modül klasörlerinde yaşar (`Business/SistemDurumu/…` gibi dikey dilim düzeni;
  Contracts'ta aynı modül adıyla eşleşir).
- Pipeline'a sonraki dilimlerde eklenecekler: tenant bağlamı (S3), audit (S5),
  yetki süzgeci (S6+). Hepsi `IPipelineBehavior` olarak girer, handler'lara dokunmaz.
- MediatR **12.5.0'a sabit**, NuGet aralığı `[12.5.0,13.0.0)` — v13+ ticari lisans (K4).

## 3. Metadata dünyası: EF Core + ince UnitOfWork (K5)

- `KdsDbContext` + varlıklar (`Varlik`/`ITenantVarlik` temeli, `Tenant`, `Kullanici`) —
  şema ve çok-kiracılık ayrıntısı: [04-veri-modeli.md](04-veri-modeli.md). Kalan gruplar
  (bağlantı S4, veri haritası S6, rapor S10…) kendi dilimlerinde eklenir.
- **Çok-kiracılık:** `ITenantBaglami` (Dal arayüzü) mevcut tenant'ı verir; gerçekleştirme
  `HttpTenantBaglami` (Api) token'ın `tenant_id` claim'inden okur. `ITenantVarlik`
  sorgularına global query filter (`TenantId == MevcutTenantId`) uygulanır; SaveChanges
  TenantId + denetim zaman damgalarını otomatik doldurur.
- **JIT provisioning:** `KullaniciSaglamaMiddleware` kimliği doğrulanmış her API isteğinde
  (kısa önbellekli) Keycloak kullanıcısını local `Kullanici`'ye sağlar; eşzamanlı ilk-istek
  yarışı (unique ihlali) handler'da yakalanıp mevcut kayda düşülür.
- **Migration:** `Kds.Dal/Migrations` (`IlkMetadata`); dev'de API açılışında `Database.Migrate()`.
- `IUnitOfWork` yalnız iki şey verir: `KaydetAsync` ve `IslemdeCalistirAsync`
  (execution strategy + transaction sarmalayıcı; gövde hata fırlatırsa transaction
  kendiliğinden geri alınır). Generic repository YOK — sorgular handler içinde
  DbContext üzerinden yazılır.
- `UnitOfWork` `internal`'dır; dışarıya yalnız arayüz çıkar (`InternalsVisibleTo`
  sadece test projesine).

## 4. Çalıştırma dünyası sözleşmeleri

`Kds.Calistirma` şu an yalnız dikişleri tanımlar; gerçekleştirme S5'te:

- `ISorguCalistirici.CalistirAsync(SorguIstegi)` → `SorguSonucu`
  — `SorguIstegi` satır sınırı + zaman aşımını ZORUNLU alan taşır (sınırsız sorgu
  tipte bile yok); SQL daima parametreli.
- `ISemaKesfi.TablolariGetirAsync` → tablo/kolon envanteri (S6 veri haritası tasarımı
  bunun üstüne kurulur).
- **S5 gerçekleşti:** saf ADO.NET çalıştırıcı (`Microsoft.Data.SqlClient`), ScriptDom
  SELECT-only bekçisi (tek kapı, beyaz-liste), audit (pipeline davranışında).
- **S7 gerçekleşti:** `ISorguUretici` (`MssqlSorguUretici`) — sorgu modelini (AST) QUOTENAME'li,
  PARAMETRELİ SQL'e çevirir (00-analiz §5); üretilen SQL yine aynı bekçiden geçer (altın-örnek
  testleriyle kilitli). Bunun için `Kds.Calistirma` artık `Kds.Contracts`'a referans verir (sorgu
  modeli + harita şeması sözleşmeleri). İki-dünya kuralı korunur: yasak olan **`Kds.Dal` (DbContext)**
  referansıdır; `Kds.Contracts` saf DTO'dur (EF yok), serbesttir.

## 5. Api katmanı

- Controller tabanlı (ekip alışkanlığı), OpenAPI belgesi `MapOpenApi()` ile
  (`/openapi/v1.json`); Swagger UI ihtiyacı olursa sonra değerlendirilir.
- CORS: yalnız ön yüz adresi (`Onyuz:Adres`, varsayılan `http://localhost:5173`).
- Bağlantı dizesi `ConnectionStrings:KdsMetadata`; geliştirme ortamı LocalDB
  (bu makinede SQLEXPRESS yok). SaaS sırrı yönetimi S13/araştırma kuyruğu konusu.

## 6. İskeletin dikey kanıtı

`GET /api/durum` → `SistemDurumuSorgusu` → pipeline → `SistemDurumuHandler` →
`{ surum, sunucuZamani }`. DB'ye inmez; amacı katman dikişlerinin (controller → MediatR
→ davranış → handler) çalıştığını her an kanıtlamak. İlk gerçek modüller bu deseni
kopyalayarak açılır.

## 8. Kimlik (Keycloak · OIDC)

- **Backend:** JWT bearer (`Kimlik` config bölümü: `Yetkili` = realm adresi,
  `Istemci` = kds-api). `KeycloakRolDonusumu` (`IClaimsTransformation`) Keycloak'ın
  `realm_access.roles` claim'ini `ClaimTypes.Role`'e çevirir →
  `[Authorize(Roles = Roller.Admin)]` doğrudan çalışır. Rol adları
  `Kds.Contracts.Kimlik.Roller` sabitlerinde (admin/gelistirici/musteri).
- **Dikey kanıt:** `GET /api/durum/kimlik` `[Authorize]` — token'sız 401 döndüğü canlı
  doğrulandı; geçerli token'la kullanıcı adı + rolleri döndürür.
- **Frontend:** `react-oidc-context` (`kds-web` istemcisi, authorization code + PKCE).
  `KimlikSaglayici` + `KorumaliAlan` tüm ekranları oturum şartına bağlar.
- **S3'te canlı kuruldu ve doğrulandı (2026-08-04):** `kds` realm'i deklaratif
  ([infra/kds-realm.json](../infra/kds-realm.json)) — `kds-web` (public+PKCE, `kds-api`
  audience + `tenant_id` claim mapper'lı), `kds-api` (bearer-only), 3 rol, tenant nitelikli
  3 test kullanıcısı. `VITE_KIMLIK_KAPALI` varsayılanı `false` (kaçış olarak korunur).
  Kanıt: token'da `aud=kds-api` + `realm_access.roles` + `tenant_id`; backend token'sız
  401, geçerli token 200 (ad+rol+tenant), `[Authorize(Roles=admin)]` admin 200 /
  musteri 403; tarayıcıda (Edge) admin ve musteri login akışları uçtan uca çalıştı.
- **Keycloak Docker'sız çalışır (S3 kararı):** Docker yok → ZIP + JDK 17
  ([infra/README.md](../infra/README.md)); `docker-compose.yml` SaaS/başka ortam için durur.

### 8.1 Güvenlik duruşu (kod incelemesi sertleştirmeleri, 2026-08-04)

- **Config ayrımı:** ortam-özel + güvensiz-dev değerleri (Authority, `HttpsZorunlu:false`,
  LocalDB, CORS origin) `appsettings.Development.json`'da; **taban `appsettings.json`
  güvenli/boş** — prod bunları miras almaz. Eksik anahtar (`Kimlik:Yetkili`, `Istemci`,
  `Onyuz:Adres`) açılışta **açıkça patlar** (sessizce localhost'a düşmez). Frontend'de de
  aynı: prod build env'siz kimlik/API adresiyle **başlamaz**.
- **Fallback authorization policy:** `RequireAuthenticatedUser` — bir uca `[Authorize]`
  koymayı unutmak onu anonim açmaz; public uçlar açıkça `[AllowAnonymous]` (ör. `/api/durum`).
- **ROPC kapalı:** `kds-web`'de direct-grant `false` — token yalnız tarayıcı akışıyla
  (auth code + PKCE) alınır. Canlı kanıt: password grant `unauthorized_client` ile reddedilir.
- **Token deposu `sessionStorage`** (localStorage değil): sekme kapanınca temizlenir,
  XSS penceresi daralır.
- **Rol-bazlı menü:** kullanıcı yalnız rolünün eriştiği ekranları görür (musteri → yalnız
  Panolar+Raporlar; canlı doğrulandı). Gerçek yetki yine backend'de — bu UI temizliği.
- **Roller:** `admin` (tüm ekranlar), `gelistirici` (tasarım ekranları), `musteri`
  (yalnız görüntüleme). Login ekranı özel **kds** teması — split "Panel Böl" düzen
  (`template.ftl` override), **LST-KDS** markası, Türkçe (realm i18n).
- **Savunmacı rol dönüşümü:** `realm_access` bozuk JSON gelse bile 500 üretmez (rol eklemeden geçer).
- **401 → yeniden giriş:** oturum düşerse frontend sessizce login akışını tetikler.

## 9. Frontend

```
frontend/  (React 19 + TypeScript + Vite; UI kiti MUI — kullanıcı kararı 2026-08-04)
└─ src/
   ├─ main.tsx            sağlayıcı zinciri: Tema → Kimlik → Router
   ├─ tema.ts             MUI teması — ürün kimliği (lacivert #0f2d4e + teal #0e6f7a)
   ├─ api/istemci.ts      fetch sarmalayıcı (kimlik açıkken Bearer başlığı)
   ├─ kimlik/             kimlikAyar + KimlikSaglayici + KorumaliAlan (§8)
   ├─ duzen/UygulamaKabugu.tsx   AppBar + kalıcı menü (7 ekran) + Outlet
   └─ sayfalar/
      ├─ PanolarSayfasi           /api/durum canlı kanıt kartı (S11'e kadar yer tutucu)
      ├─ SorguTasarimcisiSayfasi  React Flow kanvası (aşağıda)
      ├─ tasarimci/TabloDugumu    kolon başına join tutamaçlı özel düğüm
      └─ BosSayfa                 dilimi gelmemiş ekranların yer tutucusu (hangi dilim, yazar)
```

- **Sürükle-bırak: React Flow (`@xyflow/react`, MIT)** — K seçimi 00-analiz §9.
  Tasarımcı deseni bugünden çalışır durumda: sol palet → kanvasa sürükle-bırak →
  `TabloDugumu` kartı; kolon tutamaçları birbirine bağlanınca JOIN kenarı oluşur
  (`defaultEdgeOptions`). Palet şimdilik örnek varlıklarla; S6'da `ISemaKesfi`/veri
  modeline, S8'de JSON sorgu modeline bağlanır.
- Yönlendirme react-router-dom 7; her ekran menüde, dilimi gelmemiş olanlar
  `BosSayfa` ile hangi dilimde geleceğini söyler.
- Ekran adları/etiketler Türkçe; dosya/dizin adları Türkçe-ASCII (`sayfalar/`,
  `duzen/`, `kimlik/`).

## 10. Geliştirme altyapısı

- **Keycloak — Docker'sız (S3'te devreye girdi):** Bu makinede Docker kurulu değil, ama
  JDK 17 var. Keycloak ZIP dağıtımı JDK 17 ile `start-dev --import-realm` olarak koşar
  (admin gerekmez). Scriptler: [infra/keycloak-indir.ps1](../infra/keycloak-indir.ps1)
  (bir kez indir) + [infra/keycloak-baslat.ps1](../infra/keycloak-baslat.ps1) (başlat).
  Ayrıntı + kullanıcılar: [infra/README.md](../infra/README.md).
- **`docker-compose.yml`** (kök, Keycloak 26.3 + MSSQL 2022) SaaS/başka ortam için korunur;
  Docker kurulu bir makinede compose doğrulaması hâlâ açık bir iyileştirme.
- **DB:** geliştirmede LocalDB (bu makinede SQLEXPRESS yok); metadata migration'ları
  ilk varlıkla (tenant/kullanıcı) birlikte gelecek.

## 11. Kod standartları

- Sınıf/üye adları Türkçe (SQLST geleneği: `DogrulamaDavranisi`, `SorguIstegi`);
  proje/namespace adları ASCII (`Kds.Calistirma`).
- `Directory.Build.props`: net10.0 + nullable + `TreatWarningsAsErrors` — NuGet
  güvenlik denetimi (NU1903) dahil her uyarı derlemeyi kırar; açıklı geçişli paket
  yamalı sürüme sabitlenerek çözülür (örnekleri csproj yorumlarında).
- Elle try/catch + logger deseni yok; hatalar zengin sonuç/istisna olarak akar,
  yakalama yalnız izinli bölgelerde (middleware/job) yapılacak.
- `nuget.config` yalnız nuget.org'a bakar (şirket feed'i VPN istiyor); her yeni paket
  00-analiz §9 lisans denetiminden geçer.
