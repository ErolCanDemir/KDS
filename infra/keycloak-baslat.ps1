# Keycloak'i Docker'siz calistirir (bu makinede Docker yok - S3 karari 2026-08-04).
# Keycloak bir Java uygulamasidir; ZIP dagitimini JDK 17 ile kaldiririz (admin gerekmez).
# NOT: Bu dosya SAF ASCII olmalidir - PowerShell 5.1 BOM'suz .ps1'i ANSI okur ve
# ASCII-disi karakterleri (tire, Turkce harf) bozup ayristirmayi kirar.
#
# Onkosul: infra\keycloak\ altinda cikartilmis Keycloak (keycloak-indir.ps1 yapar).
# Kullanim:  powershell -File infra\keycloak-baslat.ps1
#
# realm: kds | admin konsol: http://localhost:8080 (admin/admin - YALNIZ gelistirme)
param(
    [int]$Port = 8080,
    # S48 yayin modu (infra\yayin\yayinla.ps1 kullanir): Keycloak /kimlik yolunda calisir ve
    # dis (tunel) adresini sabit hostname olarak bilir. Bos birakilirsa davranis ESKISIYLE AYNI.
    [string]$RelativePath = "",
    [string]$DisAdres = "",
    # Ters vekil arkasinda hostname SABITLEMEDEN kosus (S48 lhr.life: adres oynak) - issuer
    # istegin X-Forwarded-* basliklarindan turetilir.
    [switch]$ProxyBasliklari
)

$ErrorActionPreference = "Stop"
$kcDizin = Join-Path $PSScriptRoot "keycloak"
$kcBat = Join-Path $kcDizin "bin\kc.bat"

if (-not (Test-Path $kcBat)) {
    throw "Keycloak bulunamadi: $kcBat . Once calistir: powershell -File infra\keycloak-indir.ps1"
}

# Keycloak 26+ Java 17+ ister. Bu makinede PATH'teki java 8; JDK 17 JAVA_HOME'da.
$jdk17 = "C:\Program Files\Eclipse Adoptium\jdk-17.0.19.10-hotspot"
if (Test-Path $jdk17) { $env:JAVA_HOME = $jdk17 }
Write-Host "JAVA_HOME = $env:JAVA_HOME"

# Ilk yonetici (Keycloak 26+ bootstrap degiskenleri). Yalniz gelistirme.
$env:KC_BOOTSTRAP_ADMIN_USERNAME = "admin"
$env:KC_BOOTSTRAP_ADMIN_PASSWORD = "admin"

# Realm dosyasini import dizinine kopyala (--import-realm data\import\*.json okur).
$importDizin = Join-Path $kcDizin "data\import"
New-Item -ItemType Directory -Force $importDizin | Out-Null
Copy-Item (Join-Path $PSScriptRoot "kds-realm.json") $importDizin -Force

# Ozel login temasini (kaynak repo'da) calisan Keycloak'in themes dizinine kopyala.
$temaKaynak = Join-Path $PSScriptRoot "keycloak-theme\kds"
$temaHedef = Join-Path $kcDizin "themes\kds"
if (Test-Path $temaKaynak) {
    if (Test-Path $temaHedef) { Remove-Item $temaHedef -Recurse -Force }
    Copy-Item $temaKaynak $temaHedef -Recurse -Force
    Write-Host "kds login temasi kopyalandi."
}

Write-Host "Keycloak baslatiliyor (port $Port), realm 'kds' import ediliyor..."
$ekArgs = @()
if ($RelativePath -ne "") { $ekArgs += @("--http-relative-path", $RelativePath) }
if ($DisAdres -ne "") {
    # Yayin: issuer dis adrese sabitlenir; yereldeki API/kcadm icin backchannel dinamik kalir
    # (localhost'tan metadata/cli erisimi dis adrese zorlanmasin).
    $ekArgs += @("--hostname", $DisAdres, "--hostname-backchannel-dynamic", "true", "--proxy-headers", "xforwarded")
} elseif ($ProxyBasliklari) {
    $ekArgs += @("--proxy-headers", "xforwarded")
}
& $kcBat start-dev --http-port $Port --import-realm @ekArgs
