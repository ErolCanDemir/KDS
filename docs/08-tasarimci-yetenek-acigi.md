# 08 — Sorgu Tasarımcısı: yetenek açığı ve genişleme maddeleri

**Kaynak:** [infra/ornek-raporlar.sql](../infra/ornek-raporlar.sql) (15 örnek rapor) ·
**Tarih:** 17 Ağustos 2026 · **İstek (kullanıcı):** "Bu raporları tasarlayabilmemiz lazım, çok
özellikli olmamız lazım — eksik olan tüm özellikleri maddele."

**Yöntem:** her sorgu, sorgu modelinin sözleşmesi ([QueryModel.cs](../src/Kds.Contracts/QueryModel/QueryModel.cs))
ve SQL üreticisinin ([MssqlQueryGenerator.cs](../src/Kds.Execution/MssqlQueryGenerator.cs)) gerçek
davranışıyla karşılaştırıldı. Koddan doğrulandı: modelde **HAVING yok**, **DISTINCT yok**,
**pencere fonksiyonu yok**; **serbest WHERE kutusu (ElleKosul) var**.
**Sınır:** 15 rapor ekranda tek tek tıklanarak kurulmadı — bu bir sözleşme/kod analizidir.

---

## 1. Durum tablosu

| # | Rapor | Durum | Eksik |
|---|---|---|---|
| 1 | Etimesgut'ta oturan Audi sahipleri | ✅ kurulabilir | — |
| 2 | Marka bazında servis cirosu | ✅ | — |
| 3 | İl bazında kişi/araç/talep yoğunluğu | ✅ | — |
| 4 | En çok iş yapan 20 servis | ✅ | — |
| 5 | Getirme sebebi × durum matrisi | ✅ | — |
| 6 | Marka ciro payı + ülke içi sıralama | ❌ | Y6 pencere · Y1 ifade kolonu |
| 7 | Son 12 ay ciro trendi + değişim % | ❌ | Y4 tarih kovası · Y6 (LAG) · Y1 · Y5 |
| 8 | Top-10 servisin en pahalı 3 talebi | ❌ | Y8 ara küme · Y7 grup içi TOP-N |
| 9 | Sigortasız yüksek hasarlı araçlar | 🟡 nüanslı | Y11 (EXISTS görsel; elle SQL ile mümkün) · Y5 |
| 10 | Bölge × yıl ciro matrisi | ❌ | Y9 PIVOT · Y4 (yıl) |
| 11 | 3+ markaya yetkili servisler | ❌ | **yalnız Y3 HAVING** |
| 12 | Garantili + orijinal parçalı satış detayı | ✅ | (yalnız Y5: "son 6 ay" sabit tarihle) |
| 13 | Geri alınmış km sayacı şüphesi | ❌ | Y6 (LAG) · Y1 · Y2 |
| 14 | Randevu kanalı dönüşüm oranı | ❌ | Y10 GROUPING SETS · Y1 (oran) · Y2 (ORDER BY ifade) |
| 15 | Ödeme / fatura mutabakatı | ❌ | Y1 (fark) · Y2 (farka göre filtre) |

**Özet:** 6 tam kurulabilir · 1 nüanslı · 8 kurulamıyor.

---

## 2. Eksik yetenekler (maddeler)

Sıra, "kaç raporu açıyor" ve "bağımlılık" gözetilerek verildi; **hiçbiri uygulanmadı**.

### Y1 — Hesaplanan (ifade) kolon 🔴 *6 rapor: 6, 7, 10, 13, 14, 15*
SELECT'e formül yazabilmek: kolon/ölçü/sabit arasında **aritmetik** (`a - b`, `a * 100.0 / b`),
`CAST`, `ISNULL/COALESCE`, `ABS`, `ROUND`, sıfıra bölünme koruması (`NULLIF`).
- Bugün yalnız **birleşik kolon** (CONCAT) ve **CASE kolonu** var; sayısal ifade yok.
- İki ayrı düzey: (a) **satır düzeyi** ifade (kolonlar arası), (b) **özet sonrası** ifade
  (iki ölçünün oranı — Rapor 14'ün dönüşüm yüzdesi, Rapor 15'in farkı).
- Tasarım soruları: hangi işleç/fonksiyon kümesi serbest, tip çıkarımı (sonuç kolonunun tipi),
  ifade doğrulama (haritadaki kolonlar), üretilen SQL'in bekçiden geçmesi (son SQL zaten denetleniyor).

### Y2 — Hesaplanan kolona göre filtre ve sıralama 🔴 *3 rapor: 13, 14, 15* · *bağımlı: Y1, Y3*
Türetilmiş bir kolona (fark/oran/özet) göre **süzme** ve **sıralama**.
- Bugün sıralama yalnız **seçili bir alan** üzerinde yapılabiliyor; ifadeye göre sıralama yok.
- Özet (bağlı tablo) kolonuna filtre de yok: elle SQL koşulu kutusu bu kolonu **göremiyor**
  (o kutu yalnız harita tablolarının kolonlarını çözüyor) → Rapor 15'in `ABS(fark) > 25.000`'i.

### Y3 — HAVING (özet sonucuna filtre) 🟢 *1 rapor: 11 — tek başına açar*
`HAVING COUNT(DISTINCT …) >= 3` gibi, **gruplama sonrası** filtre.
- WHERE'in eşi: modelde ikinci bir filtre ağacı + üreticide `HAVING` bloğu.
- **En düşük maliyetli madde**; Rapor 11'in başka eksiği yok.

### Y4 — Tarih kovalama (yıl / ay / hafta / çeyrek) 🟠 *2 rapor: 7, 10*
Tarih kolonunu **yıl** ya da **ay başı** gibi kovalara indirip onunla gruplayabilmek.
- Bugün tarih kolonuyla gruplayınca her gün/saat ayrı satır oluyor → trend raporu kurulamıyor.
- Rapor 10'un yıl kırılımı ve Rapor 7'nin aylık trendi bunun üstünde duruyor.

### Y5 — Dinamik tarih ifadesi (bugün − N) 🟠 *4 rapor: 7, 9, 12, 13*
"Bugünden 6 ay önce", "son 2 yıl", "bu ayın başı" gibi **kayan** zaman aralıkları.
- Bugün yalnız `$bugun` (tam gün) var; aritmetiği yok → sabit tarih yazmak gerekiyor,
  kaydedilen rapor zamanla **kaymıyor** (bir hafta sonra yanlış pencereyi gösteriyor).
- Filtre değeri ve parametre varsayılanı olarak iki yerde gerekiyor.

### Y6 — Pencere fonksiyonları 🔴 *3 rapor: 6, 7, 13* · *bağımlı: Y1*
`ROW_NUMBER / RANK / DENSE_RANK`, `LAG / LEAD`, `SUM() OVER (…)`, `PARTITION BY`, `ORDER BY`.
- Kullanım karşılıkları: **sıralama numarası** (genel + kırılım içi), **toplam içindeki pay**,
  **önceki satırla karşılaştırma** (trend, sayaç denetimi), **yürüyen toplam**.

### Y7 — Grup içi TOP-N 🟠 *1 rapor: 8* · *bağımlı: Y6 ya da Y8*
"Her servis için en pahalı 3 talep" — bugünkü TOP sorgunun **tamamına** uygulanıyor.
- `ROW_NUMBER() OVER (PARTITION BY …)` + filtre ya da `CROSS APPLY` ile çözülür.

### Y8 — Ara küme / FROM'da alt sorgu 🟠 *1 rapor: 8*
"Önce şu özeti hesapla, sonra ona bağlan" (Rapor 8'in top-10 servisi).
- Bugün yalnız **bağlı tablo özeti** var (dar biçim: tek bağ kolonu + kendi filtresi).
- Genel çözüm: tasarımcıda **adlandırılmış ara adım** (kaydedilmiş bir sorguyu kaynak olarak
  kullanmak da bu maddenin bir biçimi olabilir).

### Y9 — PIVOT (çapraz tablo) 🟠 *1 rapor: 10* · *bağımlı: Y4*
Satır kırılımı × kolon kırılımı (bölge × yıl).
- Üretici tarafı + **görüntüleyici** tarafı birlikte gerekir (bugün rapor görüntüleyicide de yok).
- Kolon değerlerinin sabit liste mi, veriden mi türetileceği bir tasarım kararı.

### Y10 — GROUPING SETS / ROLLUP (ara toplam + genel toplam satırı) 🟠 *1 rapor: 14*
Kırılım satırlarının yanına "TOPLAM" satırı — tek taramada.
- Toplam satırının **sıralamada en sona** sabitlenmesi de gerekiyor (Y2 ile ilişkili).

### Y11 — EXISTS / NOT EXISTS görsel karşılığı 🟢 *1 rapor: 9*
"İlişkili kaydı olan / olmayan" koşulu (Rapor 9: aktif poliçesi olmayan araçlar).
- **Bugün mümkün** ama yalnız *elle SQL koşulu* kutusundan:
  `NOT EXISTS (SELECT 1 FROM … WHERE p.AracId = {Araclar.Id} AND p.AktifMi = 1)`
- Eksik olan: SQL bilmeyenin kurabileceği görsel karşılık ("şu tabloda kaydı **yok**").

---

## 3. Bu 15 raporda görünmeyen sınırlar — **hepsi kapatıldı (17 Ağu 2026)**

Bu maddeler "öncelik düşük, hiçbiri bu raporlar için gerekmiyor" diye kayda geçmişti; kullanıcı
kararıyla ("sırasıyla bu maddeleri de bitirelim") tamamı yapıldı:

- **Y12 — SELECT DISTINCT** ✅ `QueryModel.Tekille` → `SELECT DISTINCT`. Sarmalama olsa bile İÇ
  katmandadır (tekilleştirme kaynak satırlarda yapılır). Ekran: **Satır Limiti & Sayfalama**
  bölümünde *"Yinelenen satırları ele (DISTINCT) — join yüzünden çoğalan listelerde işe yarar"*.
- **Y13 — LEFT join'de ON'a sabit koşul** ✅ `JoinSetting.EkKosullar` → koşullar JOIN'in ON'una
  eklenir. Ekran: **Join Yolları**'nda satırın içinde *"Bu bağlantıya sabit şart"* — LEFT
  seçiliyken uyarı da değişir: *"yalnız EŞLEŞMEYİ daraltır — ana satır yine gelir, kolonlar boş
  kalır; filtreye yazsaydınız satır tamamen elenirdi"*.
- **Y14 — Aynı tabloyu iki kez kullanma (self-join)** ✅ `TableInstance` + `TabloOrnekleri`. Örnek,
  haritaya **sanal** olarak eklenir (kaynak tablonun tüm ilişkileri kopyalanır) → palet, kanvas,
  join yolları ve kolon listeleri onu normal bir tablo gibi görür, SQL'de ayrı takma ad alır.
  Ekran: sol paletteki tablo başlığında **kopyala** düğmesi — *"Bu tabloyu ikinci kez ekle — aynı
  tablodan İKİ FARKLI satır lazımsa (ör. 'getiren kişi' ve 'danışman')"*.
- **Y15 — UNION / birleştirme** ✅ `Birlesim` + `BirlesimTekille`. Dallar ayrı üretilir (parametre
  adları önekle ayrılır), türetilmiş tabloya sarılır; **sıralama/limit/sayfalama SQL gereği yalnız
  bütüne** uygulanır ve ana sorgudan gelir (dalın kendi sıralaması varsa açık hata). Kolon sayısı
  uyuşmazlığı erken ve sayıyla söylenir. Ekran: **"Birleşim (başka sorguyu ALT ALTA ekle)"** —
  kayıtlı rapor seçilir; *"join yan yana getirir, bu ALT ALTA"*.
- **Y16 — OFFSET/FETCH (sayfalı sorgu)** ✅ `QueryModel.Atla`. TOP ile birlikte kullanılamaz →
  "atla" doldurulunca limit **sayfa boyu** olur; sıralama zorunludur (yoksa açık hata: *"kaçıncı
  sayfa" sorusunun sabit bir cevabı olmaz*). Sarmalayıcı/çapraz katmanlarda da doğru yere yazılır.

**Kanıt:** 12 yeni altın-örnek test + 4 model testi → **274 backend + 55 önyüz testi yeşil**;
beş biçim de gerçek KdsDemo'da çalıştırıldı (Y13'te LEFT join ana satırları elenmedi — `NULL`
gelenler listede kaldı).

---

## 4. Önerilen sıra (karar kullanıcıya ait — uygulanmadı)

| Faz | Maddeler | Neden bu sırada |
|---|---|---|
| **F1** | **Y3 (HAVING)** · **Y5 (dinamik tarih)** | En küçük iş, en hızlı kazanç: Y3 tek başına Rapor 11'i açar; Y5 dört raporun "kayan zaman" sorununu çözer ve kaydedilen raporları doğru kılar |
| **F2** | **Y1 (ifade kolonu)** + **Y2 (ifadeye filtre/sıralama)** | Tek başına 6 raporun ortak eksiği; Y2 onsuz anlamsız. En çok tasarım gerektiren madde (işleç kümesi, tip çıkarımı) |
| **F3** | **Y4 (tarih kovası)** → **Y6 (pencere)** → **Y7 (grup içi TOP-N)** | Trend/sıralama/pay ailesi; Y6 ifade kolonundan sonra doğal olarak gelir |
| **F4** | **Y8 (ara küme)** · **Y9 (PIVOT)** · **Y10 (GROUPING SETS)** · **Y11 (EXISTS görsel)** | Her biri tek rapor; en özel desenler |

**Not:** F1 bittiğinde 15 raporun **7'si** (11 + nüanslı 9), F2 bittiğinde **9'u**, F3 bittiğinde
**12'si**, F4 bittiğinde **15'i** tasarımcıyla kurulabilir olur.
