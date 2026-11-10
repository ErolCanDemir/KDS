# KDS Yayın Yöntemleri — karşılaştırma ve karar kaydı

> **Durum (1 Eki 2026):** Deneme yayını için **mevcut geliştirme makinesi + Cloudflare Tunnel**
> seçildi (kullanıcı kararı — "bu PC ile ilerleyelim"). Diğer yöntemler İLERİDE yeniden
> konuşulmak üzere burada saklanıyor; fiyatlar 1 Eki 2026 araştırmasıdır (kur: 1 € ≈ 55,7 ₺,
> 1 $ ≈ 49 ₺ — güncelliğini kullanırken yeniden kontrol et).

## Seçilen yol — bu makine + Cloudflare Tunnel (0 ₺/ay)

- Makineye ücretsiz `cloudflared` tüneli kurulur; internete makinenin IP'si hiç açılmadan,
  YALNIZ KDS'nin tek yerel portu yayınlanır (makinenin geneline erişim vermez).
- Hızlı demo: hesapsız **quick tunnel** (trycloudflare.com — her açılışta adres değişir).
  Kalıcı adres: ücretsiz DuckDNS alt alanı ya da satın alınmış domain + named tunnel.
- **Bedeller:** makine açıkken yayında (7/24 değil); şirket bilgisayarı olduğu için kalıcı
  yayında IT politikası gözetilmeli; elektrik masaüstünde 7/24 ~100-200 ₺/ay (yalnız denerken
  açılırsa ~0).
- **Ön koşul (güvenlik):** internete açmadan önce Keycloak geliştirme kullanıcıları
  (admin/admin vb.) sıkılaştırılır; tek origin paketi (S48) kurulur.

## Saklanan alternatifler (ileride konuşulacak)

| Yöntem | Fiyat (1 Eki 2026) | Artı | Eksi |
|---|---|---|---|
| **Contabo Cloud VPS** (4 GB) | ~$5,56/ay ≈ 275 ₺ | Piyasanın en ucuz RAM'i; 8 GB'lısı ~$6,99 (~345 ₺) Ollama'lı tam pakete yeter | Disk/ağ performansı ve destek şöhreti Hetzner'in altında |
| **Hetzner CX23** (2 vCPU/4 GB/40 GB) | €5,49 + IPv4 €0,50 ≈ 335 ₺/ay | Güvenilir, hızlı NVMe, 20 TB trafik | NLQ (Ollama) için RAM yetmez — kapalı başlar |
| **Hetzner CX33** (4 vCPU/8 GB/80 GB) — *tam paket önerisi* | €8,49 + €0,50 ≈ 500 ₺/ay | Tüm bileşenler + CPU'da Ollama; tek tıkla büyütme | — |
| **Hetzner "yalnız denerken açık"** | kullanım başına ~30-80 ₺/ay | Saatlik faturalama (CX33 ≈ 0,8 ₺/saat); snapshot al → **sil** → gerekince geri yükle | "Kapatmak" yetmez, silinmeyen sunucu faturalanır; aç-kapa zahmeti |
| **Oracle Cloud Always Free** | 0 ₺ kalıcı | 2 OCPU/12 GB ücretsiz (2026'da yarıya indirildi) | **ARM — MSSQL çalışmaz; KDS bugün kurulamaz.** Çoklu motor (PostgreSQL) yapılırsa açılır — o işe ilk somut gerekçe. Kapasite bulmak zor |

**Uyarı:** Hetzner **CAX (ARM)** planları ucuz görünür ama alınmaz — MSSQL Linux ARM'da yok;
x86 (CX) şart.

## Domain seçenekleri

| Seçenek | Fiyat | Not |
|---|---|---|
| Hiç domain almadan | 0 ₺ | Tünelde trycloudflare/DuckDNS; VPS'te `1.2.3.4.sslip.io` (Let's Encrypt çalışır) |
| **.com — Cloudflare Registrar** | $10,46/yıl ≈ 515 ₺ | Kâr marjı yok, yenileme zammı yok — en ucuz kalıcı yol |
| .com — Porkbun | $11,08/yıl | Yakın alternatif |
| .com — Namecheap | ilk yıl ~$10,98, **yenileme ~$18,48** | Yenileme zammı tuzağı — önerilmez |

## Ortak notlar (yöntemden bağımsız)

- **MSSQL lisansı:** yayında **Express** (ücretsiz, 10 GB veri sınırı). Developer sürümü
  ücretsizdir ama lisansı üretimde kullanımı yasaklar.
- Yazılım yığınının kalanı ücretsiz: Caddy (HTTPS otomatik) · Docker · Keycloak · MongoDB ·
  Ollama · .NET · React · Cloudflare DNS/Tunnel.
- Hazırlanan paket (S48: tek origin + üretim realm + reçete) her yöntemde AYNI — yöntem
  değiştirmek taşınma değil, reçetenin son sayfasını değiştirmektir.
- NLQ için GPU: ayrı ve sonraya bırakılmış karar (kural: dış LLM API'si yok; ücretli GPU'lu
  sunucu kabul — `kds-kendi-modeli-kalir`).

Kaynaklar: Hetzner resmî fiyat ayarlaması (docs.hetzner.com) · Hetzner IPv4 ücreti ·
sparecores.com CX özellikleri · bestusavps.com/reviews/contabo · terminalbytes.com Oracle
Free Tier 2026 · elvisonunwa.com registrar karşılaştırması · kur.doviz.com.
