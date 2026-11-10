# LST-KDS — Tek-VM Yayın (Docker Compose)

Tüm yığın tek komutla ayağa kalkar: **MSSQL + Keycloak + Mongo + API + önyüz**
(00-analiz §10: v1 barındırma da Compose'dur).

## Önkoşul
- Docker + Docker Compose (Docker Desktop ya da Docker Engine).

## Kurulum
```bash
cd infra/deploy
cp .env.example .env          # parolaları ve PUBLIC_* adreslerini doldur
docker compose up -d --build
```
İlk açılış: imajlar derlenir, MSSQL sağlıklı olunca API **migration'ları uygular** (metadata
şeması), Keycloak `kds` realm'ini içe alır.

## Erişim
| Servis | Adres |
|---|---|
| Önyüz | http://localhost:5173 |
| API | http://localhost:5210 |
| Keycloak | http://localhost:8080 (yönetici: `.env`'deki `KC_ADMIN`) |

Test kullanıcıları realm'de: **admin / gelistirici / musteri** (parolalar `infra/kds-realm.json`).
Gerçek sunucuda `.env`'de `PUBLIC_KEYCLOAK/PUBLIC_API/PUBLIC_ONYUZ` değerlerini sunucunun
IP/domain'ine göre ayarlayın (tarayıcının göreceği adresler). API, token issuer'ını bu **public**
Keycloak URL'i olarak doğrular; Keycloak metadata'sını iç ağdan (`keycloak:8080`) çeker
(`Kimlik:MetadataAdresi`).

## Güvenlik — başarı ölçütü #4 (iki katman)
1. **SELECT-only bekçisi** (uygulama): ScriptDom AST, fail-closed — hiçbir yazma komutu
   üretilmez/çalıştırılmaz (28 bekçi testi).
2. **Read-only DB kullanıcısı** (veritabanı): KDS'in kaynak veritabanına bağlandığı SQL
   kullanıcısı yalnız `db_datareader` olmalı → bekçi bir gün atlansa bile yazma DB tarafından
   reddedilir. Kurulum: [`readonly-user.sql`](readonly-user.sql) (her kaynak DB'de bir kez);
   kanıt: `ReadOnlyKullaniciTests`.

## Üretim sertleştirme (v1 sonrası notlar)
- **HTTPS + ters vekil** (Caddy/Nginx/Traefik): tek origin altında TLS; `Kimlik:HttpsZorunlu=true`.
- **Keycloak üretim modu** (`start --hostname ...`): şimdilik demo için `start-dev`.
- **Secret yönetimi**: `.env` yerine Docker secrets / vault; `.env` git'e girmez.
- **Yedekleme**: `mssql-veri` / `keycloak-veri` / `mongo-veri` volume'leri.

## Durdurma / temizleme
```bash
docker compose down          # durdur (veriyi korur)
docker compose down -v       # volume'lerle birlikte SİL
```
