# infra — Geliştirme Altyapısı

> 📦 **Ürünü müşteri sunucusuna kuracaksanız:** [KURULUM.md](KURULUM.md) — kurulum sihirbazı,
> ortam değişkenleri, veri dizini, **yedekleme**, yükseltme. Bu dosya GELİŞTİRME ortamını anlatır.

> ⚠ **YALNIZ GELİŞTİRME (kod incelemesi D5):** buradaki kullanıcılar/şifreler
> (`kds-realm.json`: admin/admin…), Keycloak master admin'i ve demo veritabanı geliştirme
> içindir. Üretim realm'i AYRI yönetilir ve bu repo'ya asla girmez.

## Keycloak (Docker'sız — S3 kararı, 2026-08-04)

Bu makinede Docker kurulu olmadığından Keycloak, **ZIP dağıtımı + kurulu JDK 17** ile
çalıştırılır (admin yetkisi gerekmez). `docker-compose.yml` (kökte) SaaS/başka ortam için
korunur; yerelde aşağıdaki iki script kullanılır.

**İlk kurulum (bir kez):**
```
powershell -File infra\keycloak-indir.ps1     # Keycloak 26.3.5 ZIP -> infra\keycloak\ (gitignore'lu)
```

**Başlatma (her seferinde):**
```
powershell -File infra\keycloak-baslat.ps1    # JDK 17 ile start-dev + realm import
```
- Admin konsol: <http://localhost:8080> — `admin` / `admin` (yalnız geliştirme; **master realm**)
- Realm: `kds` — her açılışta [`kds-realm.json`](kds-realm.json)'dan içe aktarılır (üzerine yazar)
- Login ekranı: özel **kds** teması ([keycloak-theme/kds](keycloak-theme/kds)) — **split "Panel Böl"**
  tasarım (`template.ftl` override iki sütuna böler: sol marka paneli + sağ form) + CSS,
  **LST-KDS** wordmark, Türkçe arayüz; başlatma scripti temayı `themes/`'e kopyalar

> **NOT:** Scriptler **saf ASCII** olmalıdır. PowerShell 5.1 BOM'suz `.ps1`'i ANSI okur;
> tire/Türkçe karakter içerirse ayrıştırma kırılır (CLAUDE.md kuralının script hâli).

## Realm içeriği ([kds-realm.json](kds-realm.json))

| İstemci | Tür | Not |
|---|---|---|
| `kds-web` | public + PKCE (S256) | React SPA; `kds-api` audience mapper + `tenant_id` claim mapper; **direct-grant (ROPC) KAPALI** (kod incelemesi #2 — token yalnız tarayıcı akışıyla alınır) |
| `kds-api` | bearer-only | Kaynak sunucu; backend `Audience=kds-api` doğrular |

> **Bu realm YALNIZ geliştirme içindir:** `sslRequired: none`, düz-metin test parolaları.
> Üretimde ayrı ve sertleştirilmiş bir realm kurulur (HTTPS zorunlu, gerçek kullanıcı
> federasyonu, ROPC kapalı).

**Roller (realm):** `admin`, `gelistirici`, `musteri` — `Kds.Contracts.Kimlik.Roller` ile birebir.

**Test kullanıcıları** (kullanıcı adı = parola; her biri `tenant_id` nitelikli):

| Kullanıcı | Rol | Görür | Tenant |
|---|---|---|---|
| `admin` | admin | tüm ekranlar | A · `11111111-1111-1111-1111-111111111111` |
| `gelistirici` | gelistirici | tasarım ekranları (Yönetim/Bağlantılar hariç) | A · `11111111-1111-1111-1111-111111111111` |
| `musteri` | musteri | yalnız Panolar + Raporlar | B · `22222222-2222-2222-2222-222222222222` |

Token, `realm_access.roles` (roller) + `aud: kds-api` + `tenant_id` taşır; hepsi canlı
doğrulandı (S3, 2026-08-04).
