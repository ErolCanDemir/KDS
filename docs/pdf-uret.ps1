# Dokümanı HTML'den PDF'e çevirir (SQLST docs\pdf-uret.ps1'den uyarlandı — aynı gerekçeler:
# Edge makinede kurulu, ek bağımlılık/lisans yok; --no-pdf-header-footer olmazsa her
# sayfaya tarayıcı üstbilgisi basılır; açık Edge penceresi varsa headless örnek sessizce
# ona bağlanıp çıktı üretmeden çıktığından her seferinde taze GUID'li profil kullanılır).
#
# Kullanım:  powershell -File docs\pdf-uret.ps1                    → 00-analiz.html → KDS-Analiz.pdf
#            powershell -File docs\pdf-uret.ps1 -Kaynak x.html -Hedef y.pdf
param(
    [string]$Kaynak = "00-analiz.html",
    [string]$Hedef = "KDS-Analiz.pdf"
)

$ErrorActionPreference = "Stop"
$kaynakYol = Join-Path $PSScriptRoot $Kaynak
$hedefYol = Join-Path $PSScriptRoot $Hedef

if (-not (Test-Path $kaynakYol)) { throw "kaynak bulunamadi: $kaynakYol" }

$edge = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw "Microsoft Edge bulunamadi" }

$profil = Join-Path $env:TEMP ("kds-pdf-" + [Guid]::NewGuid().ToString("N"))
if (Test-Path $hedefYol) { Remove-Item $hedefYol -Force }

& $edge --headless=new --disable-gpu --no-first-run --user-data-dir="$profil" --no-pdf-header-footer `
    --print-to-pdf="$hedefYol" "file:///$($kaynakYol -replace '\\', '/')" 2>$null

$i = 0
while ((-not (Test-Path $hedefYol)) -and $i -lt 30) { Start-Sleep -Milliseconds 500; $i++ }
if (-not (Test-Path $hedefYol)) { throw "PDF uretilemedi: $hedefYol" }
Remove-Item $profil -Recurse -Force -ErrorAction SilentlyContinue

$bayt = [System.IO.File]::ReadAllBytes($hedefYol)
$metin = [System.Text.Encoding]::GetEncoding(28591).GetString($bayt)
$sayfa = ([regex]::Matches($metin, "/Type\s*/Page[^s]")).Count

Write-Host ("HAZIR: {0}  ({1:N2} MB, {2} sayfa)" -f $hedefYol, ($bayt.Length / 1MB), $sayfa)
