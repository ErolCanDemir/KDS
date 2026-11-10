# Test Rehberi — LST-KDS

Üç katmanlı otomatik test: **birim/entegrasyon** (backend), **E2E** (gerçek tarayıcı), **yük**.

## 1) Backend testleri (xUnit) — 121 test

- **Birim/handler** (SQLite in-memory + `FixedTenantContext`): iş kuralları, tenant izolasyonu,
  doğrulama, sorgu üretici/bekçisi. `tests/Kds.Tests/*.cs`.
- **HTTP entegrasyon** (`tests/Kds.Tests/Integration/`): API'yi bellekte ayağa kaldırır
  (`KdsWebFactory` = `WebApplicationFactory<Program>`), gerçek controller → handler → EF boru hattı.
  - Kimlik **Keycloak'sız**: `TestAuthHandler` sahte JWT üretir (`X-Test-*` başlıklarıyla tenant/rol
    taklidi). Keycloak/canlı sunucu gerekmez.
  - Metadata deposu **paylaşımlı-önbellek SQLite** → EF her istekte kendi bağlantısını açar (gerçek
    eşzamanlılık). Her test kendi tenant'ını tohumlar (`TenantTohumlaAsync`) → izolasyon + FK karşılanır.
  - Kapsam: anonim 401, tenant izolasyonu, parola sızmaması, ProblemDetails 400, rol kapısı (403),
    Bağlantı/Harita/Rapor/Pano CRUD, **sürükle-bırak modeli → geçerli SQL üretimi**.

```
dotnet test KDS.slnx
```

## 2) E2E — Playwright (gerçek tarayıcı, gerçek Keycloak) — 12 test

`frontend/e2e/`. Chromium'da gerçek login + tüm ekranlar + KdsDemo'da gerçek SQL çalıştırma.

- **Kimlik:** ROPC kapalı olduğundan `auth.setup.ts` gerçek Keycloak login formunu sürer; token
  sessionStorage'da olduğundan oidc kullanıcısı dosyaya yazılır, `fixtures.ts` her sayfaya enjekte
  eder (login yönlendirmesi olmadan kimlikli açılır). `e2e/.auth/` **git-ignore** (gerçek token içerir).
- **Kapsam:** login · tüm ana ekranlar hatasız açılıyor mu (İngilizce geçiş regresyon kontrolü) ·
  KdsDemo bağlantısı oluşturma · SQL editörü uçtan uca (SELECT → sonuç ızgarası) · tasarımcı açılışı.

**Ön koşul — yığın ayakta olmalı** (üç ayrı terminal):
```
powershell -File infra/keycloak-baslat.ps1     # Keycloak 8080 (realm kds)
dotnet run --project src/Kds.Api               # API 5210
cd frontend && npm run dev                     # Vite 5173
```
Sonra:
```
cd frontend
npx playwright test                 # tüm E2E
npx playwright test --ui            # görsel mod
npx playwright show-report e2e-rapor
```
Login kimliği env ile değişir: `KDS_KULLANICI` / `KDS_PAROLA` (varsayılan admin/admin — yalnız yerel realm).

## 3) Yük testi — bağımlılıksız özel koşucu

`tests/Kds.LoadTests/`. API'yi bellekte (aynı `KdsWebFactory`) ayağa kaldırır, eşzamanlı yük
bindirir; p50/p95/p99 + RPS + hata oranı raporlar. **NBomber kullanılmadı** — 6.x sürümü kurumsal
kullanımda lisans ister (MediatR<13 = K4 deseniyle tutarlı); yerine hafif açık-döngü koşucu yazıldı.

```
dotnet run --project tests/Kds.LoadTests           # 10 sn/senaryo (varsayılan)
dotnet run --project tests/Kds.LoadTests 30        # 30 sn/senaryo
```
Ölçülen: API → MediatR → EF boru hattı verimi (pano/bağlantı listeleme, sorgu SQL üretimi). Gerçek
hedef-DB (KdsDemo) sorgu **çalıştırma** yükü token gerektirdiğinden canlı ortam/E2E katmanına aittir.

## E2E spec dosyaları (`frontend/e2e/`)

- `auth.setup.ts` · `fixtures.ts` · `paths.ts` — Keycloak login + oturum enjeksiyonu.
- `screens.spec.ts` — tüm ana ekranlar hatasız açılıyor mu (7 ekran + home).
- `nav.spec.ts` — sol menü linkleri doğru rotaya gidiyor mu (menü yolu ↔ rota).
- `roles.spec.ts` — admin rolleri istemcide çözülüyor mu (rol-korumalı menü + "Yeni Pano" görünür).
- `crud.spec.ts` — Bağlantı (oluştur/düzenle/Test Et/sil) · Rapor (SQL'den kaydet/listede/çalıştır) ·
  Harita oluştur · Pano (oluştur/galeri/aç).
- `query.spec.ts` — SQL editörü uçtan uca + tasarımcı açılışı.
- `flagship.spec.ts` — **editör ↔ sürükle-bırak sonuç kıyası** (aynı sorgu iki yolla → aynı satırlar).

> Monaco editörüne yazarken `page.keyboard.type` karakterleri karıştırır (async giriş yarışı);
> `page.keyboard.insertText` (atomik) kullanılır.

## Not — İngilizce geçişinin E2E ile yakalanan regresyonları (2026-08-09)

Kapsamlı test iki dalga regresyon yakaladı (hepsi aynı sınıf: İngilizce'ye çevrilmemesi gereken
**wire/rota/menü** İngilizce'ye kaymış):

1. **Frontend api rotaları** — `/api/connections|dashboards|reports` → `/api/baglantilar|panolar|raporlar`.
   Bağlantılar/Panolar/Raporlar 404 alıyordu.
2. **Menü + navigasyon yolları** (7 yer) — `menu.tsx` + geri/git butonları `/dashboards|reports|connections`
   → Türkçe rotalar. Menüden tıklayınca boş sayfa.
3. **Whoami rol alanı** — [StatusController](../src/Kds.Api/Controllers/StatusController.cs) `Roles`
   (İngilizce) → `Roller`. Frontend `y.roller` okuduğundan istemci rolleri hep boştu → rol-korumalı
   menü (Oluştur/Veri/Yönetim) + "Yeni Pano" + tasarım butonları herkeste gizleniyordu.
   Backend entegrasyon testi `whoami_roller_alani_turkce_wire_ile_doner` bunu bir daha kaçırmaz.

Kural kaynağı: [ingilizce-kod-gecisi.md](ingilizce-kod-gecisi.md) (DB/wire/rota/menü Türkçe kalır).

## Editör ↔ sürükle-bırak sonuç kıyası (Task 2) — `flagship.spec.ts`

Aynı mantıksal sorgu iki bağımsız yolla oluşturulup **yürütme sonuçları** kıyaslanır:
- **A) Sürükle-bırak:** tasarımcıda harita seç → tabloyu aç → kolonu tıkla → Çalıştır → `/api/sorgu/calistir`.
- **B) Editör:** SQL editöründe aynı bağlantı + eşdeğer ham SQL → Çalıştır → `/api/sorgu/ham`.
- İki yanıtın `satirlar` dizisi API'den yakalanıp sıralamadan bağımsız kıyaslanır (eşit olmalı).

Örnek: `harita1` haritasındaki `EdinmeTurleri.Ad` (KdsDemo, şema `ortak`). Mevcut geliştirme
haritaları kullanılır (KDSDemoHarita1/2/3, harita1, test).
