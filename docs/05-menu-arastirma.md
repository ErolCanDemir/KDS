# 05 · Sol Menü Araştırması ve Önerisi

> Amaç: piyasadaki KDS/BI ürünlerinin navigasyonunu inceleyip LST-KDS'nin sol menüsünü
> gerekçeli şekilde şekillendirmek. Menü, [00-analiz](00-analiz.md) ürün kapsamı ve
> [roadmap](roadmap.md) dilimleriyle hizalıdır. Kabuk tasarımı (gradyan ray + akordeon)
> zaten uygulandı; bu doküman **menüde ne olacağını** belirler.

## 1. Piyasa taraması (navigasyon + özellik kümeleri)

**Power BI Service** — sol gezinme: Home · Create · Browse · OneLake/Data hub (veri kataloğu)
· Apps · Workspaces · **Metrics** (merkezi KPI'lar) · Monitor · Learn. İçerik "workspace"
konteynerlerinde; "app"ler paketlenmiş pano/rapor koleksiyonları.

**Tableau Cloud** — sol gezinme çok net bir "içerik keşfi" modeli: **Home · Explore ·
Favorites · Recents · Collections · Shared with Me**. Home; son favoriler/son görüntülenenler/
koleksiyonlar/paylaşılanları toplar.

**Metabase** — sol kenar: Home · **Collections** (soru/pano/model klasörleri) · **Browse →
Data** (veritabanı/tablo kataloğu) · **Models** · **Metrics**. Ayrıca yönetici için
**Usage analytics** (hangi pano az kullanılıyor vb.).

**Apache Superset** — üst menü: **Dashboards · Charts · Datasets · SQL Lab**. Dataset
düzeyinde tanımlı, tekrar kullanılan metrikler; SQL Lab ad-hoc sorgu.

**Self-servis BI ortak özellikleri** — Raporlar & Panolar (sürükle-bırak) · Veri haritaları
(merkezi metrik/ilişki) · **Zamanlama** (otomatik yenileme/gönderim) · **Uyarılar** (eşik
aşımında e-posta/Slack) · Paylaşım/işbirliği · **Kullanım izleme**.

**Yönetim/governance** — Kullanıcılar · Roller & Yetkiler (Admin/Member/Contributor/Viewer
deseni) · **Veri bağlantıları** (gateway/connection yönetimi, "Test Et") · **Denetim kayıtları**
· Kullanım/performans izleme · workspace-düzeyi + içerik-düzeyi yetki.

**Embedded / çok-kiracılı SaaS** — tenant başına veri yönlendirme + satır-düzeyi güvenlik ·
**zamanlanmış abonelikler** (PDF/Excel e-posta) · **eşik uyarıları** · çok sayıda bağlayıcı.

## 2. Ortak desenler (çıkarım)

Ürünlerin hepsinde menü kabaca **beş kümeye** ayrılıyor:

1. **Keşif/Çalışma alanı** — Home + içerik (Panolar, Raporlar) + organizasyon (Klasör/
   Koleksiyon, Favoriler, Son görüntülenenler, Paylaşılanlar).
2. **Oluşturma/Analiz** — sürükle-bırak sorgu, SQL, veri haritaları.
3. **Veri** — bağlantılar/kaynaklar, veri kataloğu/tarayıcı, (bizde) içe alım.
4. **Otomasyon** — zamanlanmış gönderim/abonelik, uyarılar, metrik merkezi.
5. **Yönetim** — kullanıcı, rol/yetki, tenant ayarları, denetim, kullanım.

İki güçlü, sık tekrarlanan öğe bizde henüz yok ve **eklenmeli**: **Favoriler + Son
görüntülenenler + Paylaşılanlar** (Tableau modeli) ve **Metrik merkezi** (Power BI/Metabase).
Bir de **Ana Sayfa (Home)** — panodan ayrı, kişisel açılış.

## 3. LST-KDS için önerilen menü

Rol kısaltmaları: **M**usteri (görüntüleyici) · **G**eliştirici · **A**dmin. Gruplar rayda
bölüm başlıklarıyla ayrılır. "Dilim" o öğeyi gerçek yapacak roadmap işidir.

### Küme 1 — Çalışma alanı (herkes görür)
| Öğe | Ne | Rol | Dilim |
|---|---|---|---|
| **Ana Sayfa** | Kişisel açılış: son görüntülenenler + favoriler + hızlı oluştur | M/G/A | sonra |
| **Soru Sor** | Doğal dille sorgu (NLQ / AI), metriklere grounded | M/G/A | v1.x (Kademe 2) |
| **Panolar** | Dashboard galerisi/listesi | M/G/A | S11 |
| **Raporlar** (akordeon) | Tüm Raporlar · Klasörler · Favorilerim · Benimle Paylaşılanlar | M/G/A | S10 |
| **Metrikler** | Merkezi tanımlı KPI/ölçüt kataloğu (rapor/panolarda tekrar kullanılır) | M/G/A | sonra |

### Küme 2 — Oluştur & Analiz (G + A)
| Öğe | Ne | Rol | Dilim |
|---|---|---|---|
| **Sorgu Tasarımcısı** | Sürükle-bırak sorgu (React Flow) | G/A | S7–S8 |
| **SQL Editörü** | Monaco + SELECT-only bekçisi | G/A | S9 |
| **Veri Haritaları** | Ham tabloları dostça ad/ilişki/boyut-ölçü ile kullanıma hazır harita | G/A | S6 |

### Küme 3 — Veri (G + A)
| Öğe | Ne | Rol | Dilim |
|---|---|---|---|
| **Bağlantılar** | Kaynak DB profilleri (şifreli + "Test Et" + varsayılan mod) | G/A* | S4 |
| **İçe Alım İşleri** | Zamanlanmış senkron (Import modu) + son durum/süre/satır | G/A* | S12 |
| **Veri Tarayıcı** | Şema/tablo kataloğu (isteğe bağlı; tasarımcı içinde de var) | G/A | sonra |

\* Bağlantı/içe-alım yalnız **A**'ya da kısıtlanabilir (governance kararı — §4).

### Küme 4 — Otomasyon (v1'e alındı — 2026-08-05, rakip analizi)
| Öğe | Ne | Rol | Dilim |
|---|---|---|---|
| **Zamanlanmış Raporlar** | Abonelik: periyodik e-posta (PDF/Excel), alıcı-bazlı filtre | M/G/A | v1 (Kademe 1) |
| **Uyarılar** | Eşik/anomali aşımında bildirim (data alerts) | M/G/A | v1 (Kademe 1) |

### Küme 5 — Yönetim (yalnız A, akordeon)
| Alt öğe | Ne | Dilim |
|---|---|---|
| **Kullanıcılar** | Tenant kullanıcıları (JIT liste — hazır) + davet | S3 ✅ / sonra |
| **Roller & Yetkiler** | Rapor/klasör-bazlı ince yetki (nesne izni) | S10 |
| **Satır Güvenliği (RLS)** | Kullanıcı/kiracı özniteliğine göre veri satırı kısıtı | v1 önerisi (Kademe 1) |
| **Kuruluş Ayarları** | Tenant adı/plan/limit/marka | sonra |
| **Gömme & Marka** | Embed token, izinli alan adları, beyaz-etiket | v2 (Kademe 3) |
| **Ajanlar** | Müşteri ağına kurulan KDS Ajanı: içe alım işlerini kendi tarafında koşturur, veriyi depoya iter; anahtar üret/iptal + çevrimiçi izleme | ✅ S45 |
| **Denetim Kayıtları** | Kim, hangi sorgu, ne zaman, kaç satır/ms | S5 |
| **Kullanım** | Az kullanılan pano/rapor, performans | sonra |

### Alt bölge (kullanıcı bloğu — hazır)
Avatar + ad + rol + **Çıkış**. Eklenebilir: **Profil/Ayarlar**, **Bildirimler**.

## 4. v1'de menü ne olsun (uygulama önerisi)

Şimdi kurulacak iskelet (içi dilimlerle dolacak), gruplu ve rol-görünür:

```
ÇALIŞMA ALANI
  Ana Sayfa            (M/G/A)   ← kişisel içerik: kayıtlı panolar/grafikler + hızlı sorgu sonuçları
  Soru Sor             (M/G/A)   ← doğal dille sorgu (NLQ / AI) — v1.x
  Panolar              (M/G/A)   ← pano listesi/galerisi
  Raporlar ▸           (M/G/A)   → Tüm Raporlar · Klasörler · Favorilerim · Paylaşılanlar
  Metrikler            (M/G/A)   ← ölçüt kataloğu (ekran S6 sonrası dolar)
OLUŞTUR
  Sorgu Tasarımcısı    (G/A)
  SQL Editörü          (G/A)
  Veri Haritaları      (G/A)
VERİ
  Bağlantılar          (G/A)
  İçe Alım İşleri      (G/A)
OTOMASYON
  Zamanlanmış Raporlar (M/G/A)   ← abonelik/teslim (PDF/Excel e-posta) — v1
  Uyarılar             (M/G/A)   ← eşik/anomali uyarıları — v1
YÖNETİM ▸               (A)       → Kullanıcılar · Roller & Yetkiler · Satır Güvenliği · Kuruluş Ayarları · Gömme & Marka · Denetim · Kullanım
```

**2026-08-05 güncellemesi (rakip analizi sonrası):** [06-rakip-analizi.md](06-rakip-analizi.md)
boşluk analizinde çıkan, piyasada standart olup bizde eksik olan özellikler menüye eklendi:
**Soru Sor (NLQ)**, yeni **OTOMASYON** grubu (**Zamanlanmış Raporlar** + **Uyarılar**),
**Satır Güvenliği (RLS)** ve **Gömme & Marka**. (Mobil = ayrı menü öğesi değil, kesişen bir
UX niteliği → responsive/PWA; PDF export = dışa-aktarma seçeneği, menü öğesi değil.)
Sonraya bırakılan tek öğe: **Veri Tarayıcı** (tasarımcı içinde de var).

## 5. Kararlar (kullanıcı, 2026-08-05)

**Kesinleşenler:**
- **Ana Sayfa = kişisel içerik.** Genel KPI özeti değil; kullanıcının **kendi kayıtlı
  panoları/grafikleri + hızlı sorgu sonuçları**. Panolar ayrı bir öğe olarak kalır (pano
  listesi/galerisi).
  **S43'te (27 Ağu 2026) gerçekleşme biçimi netleşti:** ana sayfanın kendi tasarım motoru YOK —
  kullanıcı panosunu Panolar'da tasarlar, birini **"ana sayfam"** işaretler, ana sayfa onu çizer.
  Seçim yoksa karşılama görünümü çıkar (görebildiği panolar listelenir, tek tıkla seçilir).
  Temsilî KPI/grafik blokları kaldırıldı.
- **Bağlantılar + İçe Alım = geliştirici + admin** (G/A). Kimlik bilgisi şifreli + salt-okunur
  DB kullanıcısı → risk sınırlı.
- **Metrikler = v1.** "Çalışma Alanı" kümesinde; ekran **S6 (Veri Haritaları) sonrası** gerçek
  ölçütlerle dolar. Metrik merkezi = merkezi tanımlı KPI kataloğu (ad/formül/biçim/model/kullanım),
  tek doğruluk kaynağı.

**Kesinleşen (2026-08-05):** Menü dizilimi **A (grup başlıklı)** · **Metrikler eklendi**.
Menü gerçek uygulamaya uygulandı: gruplu gradyan ray (ÇALIŞMA ALANI / OLUŞTUR / VERİ /
YÖNETİM) + Ana Sayfa (kişisel içerik) + Metrikler + akordeon Raporlar + rol-görünürlük
(boş grup gizlenir). Canlı doğrulandı — admin 4 grup, gelistirici 3 grup (Yönetim gizli),
musteri yalnız Çalışma Alanı.

**Kesinleşen (2026-08-05, rakip analizi sonrası):** Piyasa boşluk analizi
([06-rakip-analizi.md](06-rakip-analizi.md)) ile eksik özellikler menüye eklendi:
**Soru Sor (NLQ)** → Çalışma Alanı; yeni **OTOMASYON** grubu = **Zamanlanmış Raporlar** +
**Uyarılar** (M/G/A); **Satır Güvenliği (RLS)** ve **Gömme & Marka** → Yönetim (A). Grup
görünürlüğü: admin 5, gelistirici 4, musteri 2 (Çalışma Alanı + Otomasyon). Öncelik
kademeleri rakip-analizi §9'da.

## 6. Menü öğeleri — detaylı görev tanımı (tek tek karar)

Roller: **M**usteri (görüntüleyici) · **G**eliştirici · **A**dmin.

### ÇALIŞMA ALANI
| Öğe | Ne iş yapar | Roller | Dilim |
|---|---|---|---|
| **Ana Sayfa** | Kişisel açılış: kullanıcı **"ana sayfam" işaretlediği panoyu** burada görür (seçim yoksa panolarından birini seçtiren karşılama). Seçim kullanıcıya özeldir; aynı panoyu kullanan başkasının ana sayfası değişmez. | M/G/A | ✅ S43 |
| **Soru Sor** | **Doğal dille sorgu (NLQ)**: kullanıcı yazıyla soru sorar, AI **tanımlı metriklere grounded** yanıt + grafik üretir; "SQL'i Göster" ile şeffaf. Sürükle-bırak/SQL bilmeyen için en düşük giriş engeli. | M/G/A | v1.x (rakip analizi Kademe 2) |
| **Panolar** | Tenant/kullanıcı **dashboard'larının listesi/galerisi**. Aç, oluştur, paylaş, favorile. Kart/tablo görünümü. | görüntüle M/G/A · oluştur G/A | S11 |
| **Raporlar** ▸ | Kayıtlı raporlara erişim + organizasyon: **Tüm Raporlar** (tablo), **Klasörlerim** (klasör ağacı), **Favorilerim**, **Benimle Paylaşılanlar**. | görüntüle M/G/A · oluştur G/A | S10 |
| **Metrikler** | **Merkezi ölçüt/KPI kataloğu**. "Net Satış", "Tahsilat Oranı" gibi ölçütler bir kez (veri haritası üstünde formül) tanımlanır, her rapor/panoda aynı kullanılır — tek doğruluk kaynağı. | görüntüle M/G/A · tanımla G/A | S6 sonrası |

### OLUŞTUR
| Öğe | Ne iş yapar | Roller | Dilim |
|---|---|---|---|
| **Sorgu Tasarımcısı** | **Sürükle-bırak sorgu** (React Flow). SQL bilmeyen kullanıcı veri haritasından varlık sürükler, ilişki/filtre/gruplama kurar, sonucu grafiğe bağlar. | G/A | S7–S8 |
| **SQL Editörü** | İleri kullanıcı için **ham SQL** (Monaco) — SELECT-only bekçisi + satır/süre limiti. | G/A | S9 |
| **Veri Haritaları** | Hangi tablolar, dostça adlarla, hangi ilişkilerle (join), boyut/ölçü işaretiyle kullanılacak. Son kullanıcı ham tabloya değil **haritaya** bakar. | G/A | S6 |

### VERİ
| Öğe | Ne iş yapar | Roller | Dilim |
|---|---|---|---|
| **Bağlantılar** | **Kaynak DB profilleri**: sunucu/DB/salt-okunur kimlik (şifreli), "Test Et", varsayılan çalıştırma modu (Canlı/İçe Alım). | G/A | S4 |
| **İçe Alım İşleri** | **Import modu**: zamanlanmış senkron işleri (cron), hangi tablolar tenant deposuna çekiliyor, son durum/süre/satır. | G/A | S12 |

### OTOMASYON
| Öğe | Ne iş yapar | Roller | Dilim |
|---|---|---|---|
| **Zamanlanmış Raporlar** | **Abonelik/teslim**: rapor veya panoyu periyodik (günlük/haftalık/aylık) e-posta ile gönder (PDF/Excel), alıcı-bazlı filtre. Hangfire altyapısı üstünde (içe-alımla aynı motor). | M/G/A | v1 (rakip analizi Kademe 1) |
| **Uyarılar** | **Eşik ve anomali uyarıları (data alerts)**: bir ölçüt/rapor değeri belirlenen sınırı aşınca bildirim. Piyasada standart; bizde eksikti. | M/G/A | v1 (rakip analizi Kademe 1) |

### YÖNETİM (yalnız A)
| Öğe | Ne iş yapar | Roller | Dilim |
|---|---|---|---|
| **Kullanıcılar** | Tenant kullanıcıları (JIT ile giriş yapanlar) + davet + rol atama. | A | liste ✅ (S3) / davet sonra |
| **Roller & Yetkiler** | Rapor/klasör/pano-bazlı **ince yetki** (kim görür/düzenler). Kaba roller Keycloak'ta; ince yetki burada. **Nesne** izni (RLS'den ayrı). | A | S10 |
| **Satır Güvenliği** | **Satır-düzeyi güvenlik (RLS)**: kullanıcı/kiracı özniteliğine göre üretilen sorguya WHERE enjekte edilir (Superset deseni), ScriptDom bekçisiyle enjeksiyona kapalı. Roller & Yetkiler = nesne izni; bu = **veri satırı** izni. | A | v1 önerisi (rakip analizi Kademe 1) |
| **Kuruluş Ayarları** | Tenant adı, plan/lisans, **limitler** (satır/eşzamanlı sorgu), marka. | A | sonra |
| **Gömme & Marka** | **Gömülü analitik (embedding) + beyaz-etiket**: embed token üretimi, izinli alan adları, marka/tema. Çok-kiracılı SaaS'ta ikinci dağıtım kanalı. | A | v2 (rakip analizi Kademe 3) |
| **Denetim** | Denetim kayıtları: kim, hangi sorgu (JSON/SQL), hangi bağlantı, ne zaman, kaç satır/ms. | A | S5 |
| **Kullanım** | Kullanım/performans: az kullanılan pano/rapor, yavaş sorgular. | A | sonra |

## Kaynaklar
- Power BI: [Navigate the service](https://learn.microsoft.com/en-us/power-bi/explore-reports/end-user-experience) · [Glossary](https://learn.microsoft.com/en-us/power-bi/explore-reports/end-user-glossary)
- Tableau Cloud: [Tour your site](https://help.tableau.com/current/pro/desktop/en-us/navigating.htm) · [New home page](https://www.tableau.com/blog/new-homepage-tableau-server-and-tableau-online-makes-browsing-personal-105237)
- Metabase: [Organization overview](https://www.metabase.com/docs/latest/exploration-and-organization/start) · [Collections](https://www.metabase.com/docs/latest/exploration-and-organization/collections) · [Usage analytics](https://www.metabase.com/docs/latest/usage-and-performance-tools/usage-analytics)
- Superset: [Creating your first dashboard](https://superset.apache.org/user-docs/using-superset/creating-your-first-dashboard/)
- Self-servis BI: [Holistics](https://www.holistics.io/blog/self-service-analytics/) · [Domo](https://www.domo.com/glossary/what-is-self-service-bi)
- Governance/roller: [Power BI roles & permissions](https://moldstud.com/articles/p-power-bi-user-roles-and-permissions-comprehensive-beginners-guide)
- Embedded/çok-kiracılı: [Holistics embedded for SaaS](https://www.holistics.io/blog/embedded-analytics-for-saas/) · [Bold BI features](https://www.boldbi.com/resources/features/)
