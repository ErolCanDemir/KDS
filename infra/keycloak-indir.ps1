# Keycloak ZIP dagitimini indirip infra\keycloak\ altina cikartir (bir kez).
# Docker yok - S3 karari 2026-08-04. Binary gitignore'lu; realm/script repo'da.
# NOT: SAF ASCII olmalidir (PowerShell 5.1 BOM'suz .ps1'i ANSI okur).
# Kullanim:  powershell -File infra\keycloak-indir.ps1
param(
    [string]$Surum = "26.3.5"
)

$ErrorActionPreference = "Stop"
$hedef = Join-Path $PSScriptRoot "keycloak"
if (Test-Path (Join-Path $hedef "bin\kc.bat")) {
    Write-Host "Keycloak zaten kurulu: $hedef"; return
}

$zipUrl = "https://github.com/keycloak/keycloak/releases/download/$Surum/keycloak-$Surum.zip"
$zipYol = Join-Path $env:TEMP "keycloak-$Surum.zip"
Write-Host "Indiriliyor: $zipUrl"
Invoke-WebRequest -Uri $zipUrl -OutFile $zipYol

Write-Host "Cikartiliyor..."
$gecici = Join-Path $env:TEMP ("kc-" + [Guid]::NewGuid().ToString("N"))
Expand-Archive -Path $zipYol -DestinationPath $gecici -Force
$icDizin = Get-ChildItem $gecici -Directory | Select-Object -First 1
Move-Item $icDizin.FullName $hedef
Remove-Item $gecici -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zipYol -Force -ErrorAction SilentlyContinue
Write-Host "HAZIR: $hedef"
