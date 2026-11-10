# KDS Deneme Yayını — bu makineden, tünelle (0 ₺)

Yöntem karşılaştırması ve kararın kaydı: [docs/10-yayin-yontemleri.md](../../docs/10-yayin-yontemleri.md).

## Tek seferlik hazırlık

Araçlar `araclar/` klasörüne indirilir (repoya girmez — `.gitignore`):

```powershell
# ngrok (varsayılan tünel) — https://download.ngrok.com adresinden Windows zip'i, içinden ngrok.exe
# caddy (sürümü github.com/caddyserver/caddy/releases'tan güncelle)
curl -L -o caddy.zip https://github.com/caddyserver/caddy/releases/download/v2.11.4/caddy_2.11.4_windows_amd64.zip
# cloudflared (bu kurumsal ağda ÇALIŞMAZ — 7844 kapalı; başka ağ için yedek)
curl -L -o infra\yayin\araclar\cloudflared.exe https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe
```

ngrok için iki dosya (repoya girmez — `.gitignore`):
- `.ngrok-token` — dashboard.ngrok.com → Your Authtoken
- `.ngrok-domain` — ngrok panelinde Domains'ten alınan ücretsiz SABİT alan (örn. `xxx.ngrok-free.dev`)

Ön koşul: `dotnet build Kds.slnx` bir kez koşmuş olmalı (betik `--no-build` çalıştırır).

## Kullanım

```powershell
powershell -File infra\yayin\yayinla.ps1            # başlat (varsayılan tünel: ngrok, SABİT adres)
powershell -File infra\yayin\yayinla.ps1 -Derle     # ön yüzü yeniden derleyerek başlat
powershell -File infra\yayin\yayinla.ps1 -Tunel lhr # hesapsız yedek (adres her kopuşta değişir; nöbetçi toparlar)
powershell -File infra\yayin\yayinla.ps1 -Durdur    # durdur
```

Betik sonunda adres + kapı parolası yazar. **ngrok'ta adres SABİTTİR** — kopsa da aynı adrese
yeniden bağlanır; lhr'de her kopuşta değişir (`GUNCEL-ADRES.txt` + nobetci.ps1 izler). Geliştirme
oturumu (5173/8080/5210) ile AYNI portları kullanır — ikisi birlikte çalışmaz; betik çakışmayı
görürse açıklayıp çıkar.

ngrok ücretsiz planda tarayıcı İLK ziyarette "Visit Site" ara sayfası gösterir (bir kez; tıklanıp
geçilir). Uygulamanın kendi API çağrıları bu sayfaya takılmaz — `frontend/src/api/client.ts`
tüm isteklere `ngrok-skip-browser-warning` başlığı ekler.

## Güvenlik notları (deneme aşaması)

- Tünel yalnız Caddy'nin 8085 kapısını yayınlar; makinenin başka hiçbir portu dışarı açılmaz.
- Ön yüz + kimlik yolları tek kullanıcılık **basic-auth** arkasındadır (parola `.gizli-parola`,
  repoya girmez) — geliştirme kullanıcıları (admin/admin) internete çıplak açılmaz.
- `/api` basic-auth DIŞINDADIR (Bearer başlığıyla çakışır); API'yi kendi JWT kapısı korur.
- ngrok authtoken'ı `.ngrok-token` dosyasındadır (repo dışı); gerekirse dashboard'dan tek tıkla
  yenilenir (eskisi geçersizleşir).
- Kalıcı/gerçek yayına geçerken: üretim Keycloak realm'i (güçlü parolalar, test kullanıcısız)
  + sabit adres + bu basic-auth katmanının kaldırılması — docs/10'daki VPS konuşması.
