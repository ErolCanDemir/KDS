# S48 - YAYIN NOBETCISI: localhost.run SSH tunelini izler. Ucretsiz tunel KENDILIGINDEN kopar
# ve ssh sureci olur/askida kalir; nobetci koptugunu gorur, tuneli YENIDEN kurar, yeni adresi
# GUNCEL-ADRES.txt'ye yazar ve Keycloak kds-web redirect listesini tazeler (Keycloak host
# jokerini desteklemedigi icin adres degisiminde redirect de degismek zorunda).
# yayinla.ps1 bunu arka planda baslatir; -Durdur PID uzerinden oldurur.
# NOT: SAF ASCII (PS 5.1 kurali).
param(
    [int]$KontrolSaniye = 15
)

$ErrorActionPreference = "Continue"
$yayinDizin = $PSScriptRoot
$kok = Split-Path (Split-Path $yayinDizin)
$cfLog = Join-Path $yayinDizin "cloudflared.log"
$adresDosya = Join-Path $yayinDizin "GUNCEL-ADRES.txt"
$sshPidDosya = Join-Path $yayinDizin ".ssh-pid"
$nobetciLog = Join-Path $yayinDizin "nobetci.log"
$kcadm = Join-Path $kok "infra\keycloak\bin\kcadm.bat"
$jdk17 = "C:\Program Files\Eclipse Adoptium\jdk-17.0.19.10-hotspot"
if (Test-Path $jdk17) { $env:JAVA_HOME = $jdk17 }

function Yaz($mesaj) {
    Add-Content -Path $nobetciLog -Value ("[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $mesaj)
}

function SshBaslat {
    $p = Start-Process -FilePath "ssh" -ArgumentList @(
        "-R", "80:127.0.0.1:8085",
        "-o", "StrictHostKeyChecking=no", "-o", "ServerAliveInterval=30", "-o", "ServerAliveCountMax=2",
        "-o", "ExitOnForwardFailure=yes",
        "nokey@localhost.run"
    ) -RedirectStandardOutput $cfLog -RedirectStandardError (Join-Path $yayinDizin "cloudflared-out.log") `
        -WindowStyle Hidden -PassThru
    Set-Content -Path $sshPidDosya -Value $p.Id -Encoding ascii
    Yaz "ssh tuneli baslatildi (PID $($p.Id))."
    return $p.Id
}

function SonAdres {
    if (-not (Test-Path $cfLog)) { return $null }
    $eslesmeler = Select-String -Path $cfLog -Pattern "https://[a-z0-9]+\.lhr\.life" -AllMatches
    if (-not $eslesmeler) { return $null }
    return ($eslesmeler | Select-Object -Last 1).Matches[-1].Value
}

function RedirectTazele($adres) {
    cmd /c "`"$kcadm`" config credentials --server http://localhost:8080/kimlik --realm master --user admin --password admin 2>nul" | Out-Null
    $json = cmd /c "`"$kcadm`" get clients -r kds -q clientId=kds-web --fields id,redirectUris,webOrigins 2>nul" | Out-String
    $istemci = ($json | ConvertFrom-Json) | Select-Object -First 1
    if (-not $istemci) { Yaz "UYARI: kds-web okunamadi; redirect tazelenemedi."; return }
    $kokAlan = (([uri]$adres).Host.Split(".") | Select-Object -Last 2) -join "."
    $redirects = @($istemci.redirectUris | Where-Object { $_ -notmatch [regex]::Escape($kokAlan) }) + "$adres/*"
    $origins = @($istemci.webOrigins | Where-Object { $_ -notmatch [regex]::Escape($kokAlan) }) + $adres
    $rJson = ($redirects | ForEach-Object { '\"' + $_ + '\"' }) -join ","
    $oJson = ($origins | ForEach-Object { '\"' + $_ + '\"' }) -join ","
    cmd /c "`"$kcadm`" update clients/$($istemci.id) -r kds -s `"redirectUris=[$rJson]`" -s `"webOrigins=[$oJson]`" 2>nul" | Out-Null
    Yaz "Redirect tazelendi: $adres"
}

Yaz "Nobetci basladi."
$bilinenAdres = if (Test-Path $adresDosya) { (Get-Content $adresDosya -TotalCount 1).Trim() } else { "" }
$pespese502 = 0
$adresZamani = Get-Date

while ($true) {
    try {
        # 1) ssh sureci yasamiyor mu -> yeniden kur (log'a YENI adres dusecek).
        $sshYasiyor = $false
        if (Test-Path $sshPidDosya) {
            $sshPid = 0
            if ([int]::TryParse((Get-Content $sshPidDosya -TotalCount 1), [ref]$sshPid)) {
                $sshYasiyor = $null -ne (Get-Process -Id $sshPid -ErrorAction SilentlyContinue)
            }
        }
        if (-not $sshYasiyor) {
            Yaz "ssh olu - tunel yeniden kuruluyor."
            Remove-Item $cfLog -Force -ErrorAction SilentlyContinue
            SshBaslat | Out-Null
        }

        # 2) Su anki adres bilinenden farkliysa: dosyayi ve Keycloak redirect'ini tazele.
        $adres = SonAdres
        if ($adres -and $adres -ne $bilinenAdres) {
            Set-Content -Path $adresDosya -Value $adres -Encoding ascii
            RedirectTazele $adres
            $bilinenAdres = $adres
            $adresZamani = Get-Date
            $pespese502 = 0
            Yaz "Guncel adres: $adres"
        }

        # 3) Adres var ama uc 502 veriyorsa (lhr eslesmeyi dusurdu, ssh zombi): ssh'i oldur -
        #    bir sonraki turda yeniden kurulur ve yeni adres alinir.
        # Yeni adrese 30 sn HOSGORU (lhr eslesmesi yayilirken ilk saniyelerde 502 normal) ve
        # tek 502 ile oldurme YOK - saglikli tunel aceleci nobetci yuzunden dusurulmesin
        # (6 Eki 2026: ilk surum tek 502'de yenileyip adresi bosa degistiriyordu).
        if ($adres -and ((Get-Date) - $adresZamani).TotalSeconds -gt 30) {
            try {
                $y = Invoke-WebRequest -UseBasicParsing -Uri "$adres/health" -TimeoutSec 10
                $pespese502 = 0
            } catch {
                $durum = 0
                if ($_.Exception.Response) { $durum = [int]$_.Exception.Response.StatusCode }
                if ($durum -eq 502 -or $durum -eq 503) {
                    $pespese502++
                    if ($pespese502 -ge 3) {
                        Yaz "Tunel ucu pes pese $pespese502 kontrolde $durum verdi - ssh yenileniyor."
                        $pespese502 = 0
                        $sshPid = 0
                        if ([int]::TryParse((Get-Content $sshPidDosya -TotalCount 1), [ref]$sshPid) -and $sshPid -gt 0) {
                            Stop-Process -Id $sshPid -Force -ErrorAction SilentlyContinue
                        }
                    }
                }
            }
        }
    } catch {
        Yaz ("Dongu hatasi: " + $_.Exception.Message)
    }
    Start-Sleep -Seconds $KontrolSaniye
}
