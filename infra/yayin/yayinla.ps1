# S48 - KDS deneme yayini: BU makineden, Cloudflare quick tunnel ile (hesapsiz, 0 TL).
# NOT: Bu dosya SAF ASCII olmalidir (PS 5.1 BOM'suz .ps1'i ANSI okur; keycloak-baslat.ps1 kurali).
#
# Mimari:  internet -> cloudflared (https, adres her koscuda degisir) -> Caddy :8085
#            /          -> frontend uretim derlemesi (statik)
#            /api/*     -> Kds.Api :5210
#            /kimlik/*  -> Keycloak :8080 (relative-path=/kimlik, hostname=tunel adresi)
#
# Guvenlik (deneme asamasi): on yuz + kimlik yollari tek kullanicilik BASIC-AUTH arkasinda
# (parola ilk koscuda uretilir, .gizli-parola'da saklanir, repoya girmez). API'nin kendi JWT
# kapisi vardir. Gelistirme kullanicilari (admin/admin) boylece internete CIPLAK ACILMAZ.
#
# Kullanim:
#   powershell -File infra\yayin\yayinla.ps1            # baslat (dist yoksa once derler)
#   powershell -File infra\yayin\yayinla.ps1 -Derle     # on yuzu yeniden derleyerek baslat
#   powershell -File infra\yayin\yayinla.ps1 -Durdur    # tum yayin sureclerini durdur
param(
    [switch]$Durdur,
    [switch]$Derle,
    # Tunel saglayici (9 Eki 2026 siralamasi):
    #   "ngrok"       = SABIT ucretsiz adres (.ngrok-token + .ngrok-domain dosyalari gerekli;
    #                   443/TLS cikisli - kurumsal agda calisir; adres hic degismez) - VARSAYILAN
    #   "lhr"         = localhost.run (hesapsiz ama adres surekli degisir; nobetci toparlar)
    #   "cloudflared" = Cloudflare quick tunnel (7844 ister; bu agda kapali - 5 Eki 2026)
    [ValidateSet("ngrok", "lhr", "cloudflared")]
    [string]$Tunel = "ngrok"
)

$ErrorActionPreference = "Stop"
$yayinDizin = $PSScriptRoot
$kok = Split-Path (Split-Path $yayinDizin)
$araclar = Join-Path $yayinDizin "araclar"
$cloudflared = Join-Path $araclar "cloudflared.exe"
$caddy = Join-Path $araclar "caddy.exe"
$pidDosya = Join-Path $yayinDizin ".pidler"
$parolaDosya = Join-Path $yayinDizin ".gizli-parola"
$cfLog = Join-Path $yayinDizin "cloudflared.log"
$dist = Join-Path $kok "frontend\dist"

function SurecleriDurdur {
    if (Test-Path $pidDosya) {
        foreach ($satir in Get-Content $pidDosya) {
            $p = 0
            if ([int]::TryParse($satir, [ref]$p) -and $p -gt 0) {
                try { Stop-Process -Id $p -Force -ErrorAction Stop; Write-Host "Durduruldu: PID $p" }
                catch { }
            }
        }
        Remove-Item $pidDosya -Force
    }
    $sshPidDosya = Join-Path $yayinDizin ".ssh-pid"
    if (Test-Path $sshPidDosya) {
        $p = 0
        if ([int]::TryParse((Get-Content $sshPidDosya -TotalCount 1), [ref]$p) -and $p -gt 0) {
            Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
        }
        Remove-Item $sshPidDosya -Force
    }
    # Betik yarida olmus olabilir (.pidler yazilmadan) - sahipsiz kalanlari adla ve portla supur.
    # cloudflared/caddy bizim araclarimizdir (baska kullanim yok); java/dotnet ise port sahibinden bulunur.
    foreach ($ad in @("Kds.Api", "cloudflared", "caddy", "ngrok")) {
        Get-Process -Name $ad -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    }
    foreach ($port in @(8080, 5210, 8085)) {
        $c = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($c) { try { Stop-Process -Id $c.OwningProcess -Force -ErrorAction Stop } catch { } }
    }
    Start-Sleep -Seconds 2
}

if ($Durdur) {
    SurecleriDurdur
    Write-Host "Yayin durduruldu."
    exit 0
}

# --- On kosullar ---
if ($Tunel -eq "cloudflared" -and -not (Test-Path $cloudflared)) { throw "cloudflared yok: $cloudflared (indirme adimi icin infra\yayin\README.md)" }
if (-not (Test-Path $caddy)) { throw "caddy yok: $caddy" }

# Onceki yayin kalintilarini temizle; dev surecleriyle cakismayi kontrol et.
SurecleriDurdur
foreach ($port in @(8080, 5210, 8085)) {
    $dinleyen = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($dinleyen) {
        throw "Port $port zaten dinleniyor (muhtemelen gelistirme oturumu acik). Once onu kapatin - yayin ayni portlari kullanir."
    }
}

# --- 1) On yuz uretim derlemesi ---
if ($Derle -or -not (Test-Path (Join-Path $dist "index.html"))) {
    Write-Host "On yuz derleniyor (npm run build)..."
    Push-Location (Join-Path $kok "frontend")
    try { & npm run build; if ($LASTEXITCODE -ne 0) { throw "npm run build basarisiz." } }
    finally { Pop-Location }
}

# --- 2) Basic-auth parolasi (ilk koscuda uretilir, sonra sabit kalir) ---
if (Test-Path $parolaDosya) {
    $parola = (Get-Content $parolaDosya -TotalCount 1).Trim()
} else {
    $parola = ([guid]::NewGuid().ToString("N").Substring(0, 16))
    Set-Content -Path $parolaDosya -Value $parola -Encoding ascii
}
$hash = (& $caddy hash-password --plaintext $parola | Select-Object -First 1).Trim()
if (-not $hash.StartsWith("$")) { throw "caddy hash-password beklenmedik cikti verdi: $hash" }

$pidler = @()

# --- 3) Keycloak (/kimlik yolunda; hostname SABITLENMEZ - issuer istegin Host'undan turetilir,
#         API issuer'i DESENLE dogrular; lhr adresi oturum icinde degisebildigi icin tek yol bu) ---
$kc = Start-Process -FilePath "powershell" -ArgumentList @(
    "-NoProfile", "-File", (Join-Path $kok "infra\keycloak-baslat.ps1"),
    "-RelativePath", "/kimlik", "-ProxyBasliklari"
) -WindowStyle Hidden -PassThru
$pidler += $kc.Id
$hazir = $false
foreach ($i in 1..90) {
    Start-Sleep -Seconds 2
    try {
        $y = Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:8080/kimlik/realms/kds" -TimeoutSec 3
        if ($y.StatusCode -eq 200) { $hazir = $true; break }
    } catch { }
}
if (-not $hazir) { SurecleriDurdur; throw "Keycloak acilmadi (infra\keycloak klasorunu ve Java 17'yi kontrol edin)." }
Write-Host "Keycloak hazir (/kimlik)."

# --- 4) Tunel ---
Remove-Item $cfLog -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $yayinDizin "GUNCEL-ADRES.txt") -Force -ErrorAction SilentlyContinue
if ($Tunel -eq "ngrok") {
    # SABIT adres: baglanti kopsa da ngrok ajani ayni adrese yeniden baglanir -> nobetci ve
    # "adres degisti" dertleri yok. Token/domain dosyalardan (repoya girmez).
    $tokenDosya = Join-Path $yayinDizin ".ngrok-token"
    $domainDosya = Join-Path $yayinDizin ".ngrok-domain"
    if (-not (Test-Path $tokenDosya)) { throw ".ngrok-token yok - dashboard.ngrok.com'dan authtoken alin (hesapsiz yol icin: -Tunel lhr)" }
    if (-not (Test-Path $domainDosya)) { throw ".ngrok-domain yok - ngrok panelinde Domains'ten ucretsiz sabit alan alin" }
    $ngrokDomain = (Get-Content $domainDosya -TotalCount 1).Trim()
    $env:NGROK_AUTHTOKEN = (Get-Content $tokenDosya -TotalCount 1).Trim()
    $cf = Start-Process -FilePath (Join-Path $araclar "ngrok.exe") -ArgumentList @(
        "http", "8085", "--url", "https://$ngrokDomain", "--log", "stdout", "--log-format", "logfmt"
    ) -RedirectStandardOutput $cfLog -RedirectStandardError (Join-Path $yayinDizin "cloudflared-out.log") `
        -WindowStyle Hidden -PassThru
    Remove-Item env:NGROK_AUTHTOKEN -ErrorAction SilentlyContinue
    $pidler += $cf.Id
    # Baglanti kurulana kadar bekle (log'da "started tunnel").
    $hazir = $false
    foreach ($i in 1..30) {
        Start-Sleep -Seconds 2
        if ((Test-Path $cfLog) -and (Select-String -Path $cfLog -Pattern "started tunnel" -Quiet)) { $hazir = $true; break }
        if ((Test-Path $cfLog) -and (Select-String -Path $cfLog -Pattern "ERR_NGROK" -Quiet)) { break }
    }
    if (-not $hazir) { SurecleriDurdur; throw "ngrok baglanamadi (bkz. $cfLog)." }
    $adres = "https://$ngrokDomain"
    Set-Content -Path (Join-Path $yayinDizin "GUNCEL-ADRES.txt") -Value $adres -Encoding ascii
} elseif ($Tunel -eq "cloudflared") {
    # NOT: Cloudflare edge baglantisi 7844 portunu ister (QUIC/UDP ya da http2/TCP). Bu agda
    # 7844 HER IKI protokolde de kapali cikti (5 Eki 2026) - adres verilir ama baglanamaz.
    $cf = Start-Process -FilePath $cloudflared -ArgumentList @("tunnel", "--url", "http://127.0.0.1:8085", "--no-autoupdate", "--protocol", "http2") `
        -RedirectStandardError $cfLog -RedirectStandardOutput (Join-Path $yayinDizin "cloudflared-out.log") `
        -WindowStyle Hidden -PassThru
    $pidler += $cf.Id
} else {
    # localhost.run (SSH 22): tunel kurma + kopunca YENIDEN kurma + adres degisince Keycloak
    # redirect tazeleme isleri NOBETCIDE (nobetci.ps1) - ucretsiz tunel kendiliginden koptugu
    # icin tek seferlik kurulum yetmiyor (5 Eki 2026 dersi).
    $nb = Start-Process -FilePath "powershell" -ArgumentList @(
        "-NoProfile", "-File", (Join-Path $yayinDizin "nobetci.ps1")
    ) -WindowStyle Hidden -PassThru
    $pidler += $nb.Id
}

# Adres: nobetci (ya da cloudflared logu) uretene kadar bekle (ngrok'ta zaten sabit/elde).
if ($Tunel -ne "ngrok") { $adres = $null }
foreach ($i in 1..60) {
    if ($adres) { break }
    Start-Sleep -Seconds 2
    if ($Tunel -eq "cloudflared") {
        if (Test-Path $cfLog) {
            $eslesme = Select-String -Path $cfLog -Pattern "https://[a-z0-9-]+\.trycloudflare\.com" | Select-Object -First 1
            if ($eslesme) { $adres = $eslesme.Matches[0].Value; break }
        }
    } else {
        $adresDosya = Join-Path $yayinDizin "GUNCEL-ADRES.txt"
        if (Test-Path $adresDosya) { $adres = (Get-Content $adresDosya -TotalCount 1).Trim(); if ($adres) { break } }
    }
}
if (-not $adres) { SurecleriDurdur; throw "Tunel adresi alinamadi (bkz. $cfLog / nobetci.log)." }
Write-Host "Tunel hazir: $adres"

# --- 5) Keycloak kds-web redirect'ini guncel adrese cek (Keycloak host jokerini desteklemez;
#     tam adres sart). lhr'de ADRES DEGISTIKCE ayni isi nobetci de yapar; ngrok/cloudflared
#     dallarinda nobetci olmadigi icin BURASI tek yer (9 Eki 2026: ngrok'ta eksikti -
#     "Gecersiz parametre: redirect_uri" bununla cozuldu).
$jdk17 = "C:\Program Files\Eclipse Adoptium\jdk-17.0.19.10-hotspot"
if (Test-Path $jdk17) { $env:JAVA_HOME = $jdk17 }
$kcadm = Join-Path $kok "infra\keycloak\bin\kcadm.bat"
# kcadm stderr'e bilgi satiri basar; PS 5.1 bunu hataya cevirir -> cmd /c + 2>nul (bilinen tuzak).
cmd /c "`"$kcadm`" config credentials --server http://localhost:8080/kimlik --realm master --user admin --password admin 2>nul" | Out-Null
$json = cmd /c "`"$kcadm`" get clients -r kds -q clientId=kds-web --fields id,redirectUris,webOrigins 2>nul" | Out-String
$istemci = ($json | ConvertFrom-Json) | Select-Object -First 1
if ($istemci) {
    $kokAlan = (([uri]$adres).Host.Split(".") | Select-Object -Last 2) -join "."
    $redirects = @($istemci.redirectUris | Where-Object { $_ -notmatch [regex]::Escape($kokAlan) }) + "$adres/*"
    $origins = @($istemci.webOrigins | Where-Object { $_ -notmatch [regex]::Escape($kokAlan) }) + $adres
    # cmd icinden gecen JSON tirnaklari \" kacisi ister - yoksa update SESSIZCE kaydolmaz.
    $rJson = ($redirects | ForEach-Object { '\"' + $_ + '\"' }) -join ","
    $oJson = ($origins | ForEach-Object { '\"' + $_ + '\"' }) -join ","
    cmd /c "`"$kcadm`" update clients/$($istemci.id) -r kds -s `"redirectUris=[$rJson]`" -s `"webOrigins=[$oJson]`" 2>nul" | Out-Null
    # Dogrula: yazilmis mi? (sessiz kacis hatasi + sessiz stderr = kor nokta dersi)
    $kontrol = cmd /c "`"$kcadm`" get clients/$($istemci.id) -r kds --fields redirectUris 2>nul" | Out-String
    if ($kontrol -notmatch [regex]::Escape($adres)) { SurecleriDurdur; throw "Keycloak redirect guncellenemedi (kcadm update kaydolmadi)." }
    Write-Host "Keycloak redirect guncellendi: $adres/*"
} else {
    SurecleriDurdur; throw "Keycloak kds-web istemcisi okunamadi - redirect guncellenemedi."
}

# --- 6) API (issuer=tunel, metadata=yerel - Kimlik:MetadataAdresi S13 deseni) ---
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5210"
$env:Kimlik__Yetkili = "$adres/kimlik/realms/kds"
$kokAlanApi = (([uri]$adres).Host.Split(".") | Select-Object -Last 2) -join "."
$env:Kimlik__IssuerDeseni = "^https://[a-z0-9-]+\." + [regex]::Escape($kokAlanApi) + "/kimlik/realms/kds$"
$env:Kimlik__MetadataAdresi = "http://localhost:8080/kimlik/realms/kds/.well-known/openid-configuration"
$env:Kimlik__HttpsZorunlu = "false"
$env:Onyuz__Adres = $adres
$api = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--no-build", "--no-launch-profile", "--project", (Join-Path $kok "src\Kds.Api")) `
    -WindowStyle Hidden -PassThru
$pidler += $api.Id
foreach ($k in @("Kimlik__Yetkili", "Kimlik__IssuerDeseni", "Kimlik__MetadataAdresi", "Kimlik__HttpsZorunlu", "Onyuz__Adres", "ASPNETCORE_URLS")) {
    Remove-Item "env:$k" -ErrorAction SilentlyContinue
}
$hazir = $false
foreach ($i in 1..60) {
    Start-Sleep -Seconds 2
    try {
        Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:5210/health" -TimeoutSec 3 | Out-Null
        $hazir = $true; break
    } catch {
        if ($_.Exception.Response) { $hazir = $true; break }   # 401 de "ayakta" demektir
    }
}
if (-not $hazir) { SurecleriDurdur; throw "API acilmadi (once 'dotnet build Kds.slnx' gerekebilir)." }
Write-Host "API hazir."

# --- 7) Caddy (tek kapi) ---
$env:KDS_YAYIN_KULLANICI = "kds"
$env:KDS_YAYIN_HASH = $hash
$env:KDS_YAYIN_DIST = $dist
$cd = Start-Process -FilePath $caddy -ArgumentList @("run", "--config", (Join-Path $yayinDizin "Caddyfile")) `
    -WorkingDirectory $yayinDizin -WindowStyle Hidden -PassThru
$pidler += $cd.Id

Set-Content -Path $pidDosya -Value $pidler -Encoding ascii

Write-Host ""
Write-Host "================= KDS YAYINDA ================="
Write-Host "  Adres    : $adres"
Write-Host "  Kapi     : kullanici 'kds'  parola '$parola'   (tarayici soracak)"
Write-Host "  Uygulama : KDS girisi icin her zamanki kullanicilar (admin / gelistirici / musteri)"
Write-Host "  Durdurma : powershell -File infra\yayin\yayinla.ps1 -Durdur"
Write-Host "  NOT      : $(if ($Tunel -eq 'ngrok') { 'adres SABITTIR - kopsa da ayni adrese yeniden baglanir.' } else { 'adres degisebilir; gunceli infra\yayin\GUNCEL-ADRES.txt dosyasindadir.' }) Makine kapaninca yayin durur."
Write-Host "==============================================="
