# 06 · Rakip / Piyasa Analizi — KDS

> LST-KDS'in konumlanması için piyasadaki KDS/BI ürünlerinin tam taraması: kim ne
> yapıyor, 2026'da neyin "olmazsa-olmaz" (table-stakes) neyin farklılaştırıcı olduğu,
> ve **bizde ne var / ne eksik / neyle ayrışırız**. Kararların dayanağı
> [00-analiz.md](00-analiz.md) (K1–K11, §11 v1 kapsamı); iş sırası [roadmap.md](roadmap.md).
>
> **Araştırma tarihi:** 2026-08-05 · **Yöntem:** 15 ürün + trend/fiyat/Türkiye kolu web
> taraması (resmî dokümanlar + üçüncü-taraf analizler). **Fiyat rakamları** neredeyse
> hiçbir kurumsal üründe resmî yayınlanmıyor; buradaki tüm parasal değerler üçüncü-taraf
> tahmindir ve **yön** olarak okunmalıdır, kesin gerçek değil.

---

## 1. Yönetici özeti — en kritik 6 bulgu

1. **Bizim çekirdek konumumuz (sürükle-bırak + ham SQL + veri haritası + iki mod +
   çok-kiracılık) sağlam ve piyasada karşılığı var.** Doğrudan rakiplerimiz iki kümede:
   *self-servis açık kaynak* (Metabase, Superset) ve *gömülü çok-kiracılı SaaS*
   (Bold BI, Qrvey, GoodData, Luzmo, Holistics). Kurumsal devler (Power BI, Tableau,
   Qlik, Looker, ThoughtSpot) farklı lig — ama trendleri onlar belirliyor.

2. **2026'da üç şey artık "giriş bileti" oldu, farklılaştırıcı değil:** (a) **doğal
   dille sorgu / AI (NLQ)**, (b) **satır-düzeyi güvenlik (RLS)**, (c) **zamanlanmış
   rapor + eşik uyarıları**. Bunların **üçü de [00-analiz.md](00-analiz.md) §11'de v1
   DIŞI** ya da hiç yok. Piyasa hızlı hareket etti; bu kararların yeniden tartışılması
   gerekiyor (bkz. §8–§9).

3. **Veri haritası (sektörde "semantik katman") en değerli varlığımız — çünkü hem self-servisin hem AI'nin
   temeli.** GoodData (MAQL), Looker (LookML), Holistics (AML) bunun üstüne kuruldu.
   "Metrikler" ekranımız doğru içgüdü; ama 2025-2026'da **OSI / Apache Ossie**
   (satıcıdan bağımsız semantik değişim standardı) doğdu — kapıyı açık tutmalıyız.

4. **AI, semantik katman olmadan halüsinasyon üretir.** Sektör dersi net (Looker
   "%⅔ hata azalttı" iddiası, ThoughtSpot deterministik search-token, Qlik Answers):
   NLQ'yu **tanımlı metriklere grounded** kur, serbest LLM'e bırakma. Bu bizim için
   sıralama demek: **önce metrik katmanı olgunlaşmalı, sonra AI**.

5. **Fiyatta saf "kullanıcı başına" (per-seat) çok-kiracılı SaaS için tuzak.** İzleyici
   sayısı büyüdükçe müşteriyi cezalandırır. Sektör yönü **hibrit**: üretici (creator)
   için koltuk + izleyici için kapasite/kullanım veya **tenant/workspace paketi**
   (GoodData per-workspace, Qrvey/Bold BI flat "no-per-user" modeli bunun örneği).

6. **Türkiye'de bağımsız, yerli, self-servis BI motoru boşluğu gerçek.** Yerli oyuncular
   (OBASE, Bimser, İnnova…) çoğunlukla küresel motorların entegratörü/bayisi. Asıl rakip
   **Power BI** (agresif ~$14/kullanıcı fiyatı çıpa). Farklılaşma ekseni: **Türkçe NLQ +
   KVKK/veri-egemenliği + yerel fiyat/destek**.

---

## 2. Pazar haritası ve LST-KDS'in yeri

Piyasa dört segmente ayrışıyor. Bizim doğal segmentimiz **çok-kiracılı self-servis SaaS** —
yani 2. ve 3. segmentin kesişimi.

| Segment | Örnekler | Konum | Bize uzaklık |
|---|---|---|---|
| **1. Kurumsal BI liderleri** | Power BI, Tableau, Qlik Sense, Looker, ThoughtSpot | Devasa ekosistem, ağır governance, yüksek fiyat/karmaşıklık | Uzak (farklı lig) ama **trend belirleyici** |
| **2. Açık kaynak / self-servis** | Metabase, Superset, Redash, Grafana | Düşük/sıfır lisans, geliştirici/analist dostu, self-host | **Yakın** — sorgu deneyimimizin referansı |
| **3. Gömülü / çok-kiracılı SaaS** | Bold BI, Qrvey, GoodData, Luzmo, Holistics, Sisense, Domo, Explo, Reveal | SaaS ürüne gömme, tenant izolasyonu, white-label | **En yakın** — mimarimizin birebir karşılığı |
| **4. Türkiye / bölgesel** | OBASE, Bimser, İnnova, Uyumsoft… | Çoğunlukla entegratör/bayi ya da ERP-içi raporlama | **Boşluk** — yerli bağımsız motor zayıf |

**Okuma:** Bir açık kaynak ürünün (Metabase) no-code deneyimini, bir gömülü SaaS ürünün
(GoodData/Qrvey) çok-kiracılık + iki-mod olgunluğuyla birleştirmeye çalışıyoruz.
En tehlikeli rakip **Qrvey** (canlı vs import + çok-kiracılık + zamanlama/alert tek üründe)
ve **Bold BI** (site-başına DB izolasyonu + canlı/extract mod ikilisi bizimkiyle birebir).

---

## 3. Ürün profilleri (özet)

> Her ürün için: konum · en güçlü yanı · en zayıf yanı · **bizim için ders**.

### 3.1 Kurumsal BI liderleri

**Microsoft Power BI** — Bulut-öncelikli, Microsoft Fabric ile birleşmiş; 200+ konnektör,
Import/DirectQuery/Direct Lake modları, semantic model + DAX, RLS/OLS, Copilot (F2+
kapasiteden itibaren), Teams/Excel entegrasyonu. **En güçlü:** ekosistem + ~$14/kullanıcı
giriş fiyatı (pazarın çıpası). **Zayıf:** lisans karmaşası (Free/Pro/PPU/Fabric F SKU),
gerçek self-service/embed/Copilot için pahalı kapasite; on-prem (Report Server) çok geride.
**Ders:** iki-mod ayrımımızın (canlı/import) birebir kaynağı Power BI'ın DirectQuery vs
Import'u — [00-analiz.md](00-analiz.md) §3 zaten buna dayanıyor.

**Salesforce Tableau** — Görselleştirme derinliğinde lider (VizQL motoru), 100+ konnektör,
Live/Extract(Hyper), Tableau Pulse (otomatik KPI/anomali) + Tableau Agent (AI). **Zayıf:**
rol-bazlı lisans + kredi tüketimi TCO'yu öngörülemez kılar; en yeni AI/semantik Cloud/Tableau
Next'e bağlı. **Ders:** görsel zenginlik satın alınır bir fark; ECharts ile makul bir set v1,
ama "grafik kütüphanesi genişliği" kalıcı bir yatırım alanı.

**Qlik Sense** — Tek teknik farklılaştırıcısı **Associative Engine**: seçim yapınca ilgisiz
veriyi de ("gri") gösterir; sorgu-tabanlı değil. Section Access ile satır+sütun güvenliği,
mobilde çevrimdışı in-memory analiz, Qlik Answers (agentic AI). 2025'te **kapasite-bazlı
fiyata** geçti (per-user'ı yeni müşteriye kapattı). **Ders:** fiyat modeli göçü (per-user →
kapasite) sektörel bir sinyal; biz baştan hibrit tasarlamalıyız.

**Google Looker** — **LookML** (kod-tabanlı, Git-versiyonlu semantik model) + in-database
(extract yok, sorgu ambara push). Kurumsal-sınıf embedding/white-label + access_filter (RLS).
Gemini in Looker semantik modele grounded (halüsinasyonu ~⅔ azalttığı iddiası). **Zayıf:**
görsel esneklik Tableau'nun gerisinde; LookML dik öğrenme eğrisi; fiyat "Call sales" (Standard
~$60K/yıl tahmini). **Ders:** "governed semantik katman = AI'nin doğruluğu" tezinin en net
kanıtı; bizim metrik katmanı → AI sıralamamızı doğruluyor.

**ThoughtSpot** — Arama/AI-öncelikli; sorgu oluşturmanın birincil yolu doğal dil (Spotter AI
analist). Patentli **search-token → deterministik SQL** (LLM text-to-SQL yerine). Orgs =
çok-kiracılık, RLS/CLS AI cevaplarına taşınır. **Zayıf:** kurumsal kullanım yıllık $100K+;
tüketim/sorgu-bazlı fiyat öngörülemez. **Ders:** NLQ'da "deterministik/açıklanabilir" yaklaşım
serbest-LLM'den güvenli — bizim AI tasarımımızın hedefi bu olmalı.

### 3.2 Açık kaynak / self-servis (bize yakın)

**Metabase** — No-code **notebook sorgu tasarımcısı**nın piyasadaki en olgun örneği +
yerel SQL editörü (no-code ↔ SQL convert). **Data Studio**: Models + **Metrics** (bir kez
tanımla, her yerde kullan) + lineage. Dashboard subscriptions (**alıcı grubuna göre farklı
filtre**), Metabot AI. **Zayıf:** RLS/SSO/audit/embedding **paywall arkasında**; per-seat
ölçekte artar; **AGPL** gömen SaaS'a kaynak-paylaşım yükü. **Ders:** no-code tasarımcı
UX'imizin altın standardı Metabase notebook; ayrıca "sürükle-bırak ↔ SQL round-trip"
hedefimiz ([00-analiz.md](00-analiz.md) §5 JSON→SQL "SQL'i Göster") doğru yolda.

**Apache Superset** — Sıfır lisansla kurumsal genişlik: Explore (drag-drop) + **SQL Lab**
(tam IDE, Jinja), 40+ viz, dataset-düzeyi metrik, **gerçek RLS** (Jinja/user-attribute ile
WHERE enjeksiyonu), guest-token embedding. **Zayıf:** ağır kurulum (web+Celery+Redis+…),
native AI yok (yalnız Preset), mobil zayıf. ⚠ RLS-bypass CVE'si (2025) → SQL enjeksiyon
savunmasını baştan sağlam kur. **Ders:** RLS'i Superset gibi **sorguya WHERE enjekte**
modeliyle, ama bizim **ScriptDom SELECT-only bekçisi** ([00-analiz.md](00-analiz.md) §7) ile
enjeksiyona kapalı kur.

**Redash** — SQL-first; **no-code tasarımcı yok**. 48 konnektör, güçlü editör (snippet,
parametre, history), eşik alert. **Bakım modunda** (Databricks aldı, gönüllüyle yürüyor).
**Ders:** SQL editörümüzün (Monaco) snippet+parametre+geçmiş çıtası buradan alınmalı; ama
tek başına SQL yetmez (Redash'in düşüşünün dersi).

**Grafana** — Observability/gerçek-zamanlı lider, **sınıfının en iyi Alerting**'i (çok-kanal,
kural, silence). Ama **semantik katman yok, RLS yok**, iş-kullanıcısı self-servisi zayıf —
farklı segment. **Ders:** uyarı (alerting) mimarisinin referansı; iş BI'ı için model değil.

*(Bonus: **Lightdash** = dbt-native metrics-as-code; **Evidence** = "BI as code" SQL+Markdown.
İkisi de code-first governance nişi — bizim "metrics-as-code" ileri hedefimize ilham.)*

### 3.3 Gömülü / çok-kiracılı SaaS (en yakın segment)

**Bold BI (Syncfusion)** — **Dişe-diş rakip.** "Site" = tenant, **her site ayrı DB**
izolasyonu; **canlı vs extract mod ikilisi bizimkiyle birebir**. Olgun schedule/export
(hourly→yearly, PDF/Excel/CSV/image, **FTP/SFTP/webhook**, harici alıcı), eşik alert
(5 koşul tipi, e-posta/webhook), audit trail (immutable, SIEM'e forward), AI Copilot.
**No-per-user lisans** (custom quote; ~$495/ay iddiası + community lisans). **Zayıf:**
büyük veride performans şikâyeti; gömme "significant dev effort"; merkezî metric layer
belirsiz. **Ders:** schedule/export ve audit olgunluğu birebir örnek alınmalı.

**Qrvey** — **En tehlikeli rakip.** Gömülü analitikte **dahili veri gölü (Elasticsearch)**
olan tek yığın: **Live-Connect + Managed(ingest) + Dataset Views** = bizim "canlı vs import"
modelimizin bir üstü. iframe-free bileşen gömme, qvToken RLS, workflow/automation (scheduled
export + alert), **LLM-agnostik** AI (OpenAI/Claude/Bedrock). **Flat-rate, sınırsız
tenant/user** (per-user yok). **Zayıf:** kendi cloud'unuza ES kümesi = operasyon yükü;
şeffaf fiyat yok. **Ders:** iki-mod + çok-kiracılık + zamanlama/alert'i **tek üründe**
sunmak pazarın beklentisi; bizim farkımız daha hafif/managed + şeffaf fiyat olabilir.

**GoodData** — **Headless/API-öncelikli** çok-kiracılık şampiyonu: **parent-child workspace
hiyerarşisi** (model/metrik tek merkezden binlerce kiracıya miras), WDF (tenant veri
yönlendirme) + UDF (RLS), **LCM ile binlerce workspace declarative provizyon**. **MAQL**
açık/headless metrik store (en büyük farkı). FlexQuery (canlı pushdown + FlexCache + bellek/
disk materialize). **Per-workspace fiyat** (~$1.500/ay+). AI Assistant (RAG) + **self-hosted
AI**. **Zayıf:** son-kullanıcı ham SQL zayıf; audit/usage yalnız Enterprise; MAQL öğrenme
eğrisi. **Ders:** çok-kiracılık için **parent-child + declarative provizyon** deseni ve
**per-tenant fiyat** modeli bizim için altın referans.

**Luzmo (eski Cumul.io)** — Üç çok-kiracılık deseni (tek-dataset **parametre override** +
kiracı-başına-DB routing + broker plugin), live/import/query-cache/**Warp(tam senkron)** dört
veri modu, **Luzmo IQ** (chat widget + **MCP/Agent API** — Claude/OpenAI/LangChain + RAG).
**MAU-bazlı fiyat** (~$995–3.100/ay). **Zayıf:** son-kullanıcıya ham SQL merkezî değil.
**Ders:** AI'nin gömme yolları (hazır chat widget + MCP/agent) ve **embed-token'da parametre
override** RLS deseni birebir örnek.

**Holistics** — **AML (analytics-as-code, Git)** semantik katmanı en olgun; ama **salt-canlı**
(import/materialization yok) ve **native SDK yok** (sadece iframe). Güçlü zamanlı gönderim
(PDF/Excel/CSV/PNG, şifreli, Slack), **şeffaf fiyat** ($800–2000/ay). **Ders:** import modumuz
Holistics'e karşı net farklılaşma; as-code semantik katman ise onların gücü, ileri hedefimiz.

**Sisense · Domo · Explo · Reveal** (kısa):
- **Sisense:** olgun in-memory ElastiCube + 3 çok-kiracılık deseni + ML anomali (Pulse);
  ama headless metrik yok, live kısıtlı, pahalı.
- **Domo:** tam-yığın (1000+ konnektör + ETL + Adrenaline depo + en olgun AI: DomoGPT +
  **Claude modelleri** + agentic MCP) + **çok-kanallı alert** (e-posta/SMS/push/telefon);
  ama consumption-credit fiyatı öngörülemez, çok-kiracılık "purpose-built" değil.
- **Explo:** hızlı gömme + son-kullanıcı Report Builder; ama **Ekim 2025'te Omni satın aldı,
  ~12 ayda sunset** (uzun vadeli rakip değil).
- **Reveal (Infragistics):** native SDK gömme (iframe yok) uç referansı; ama **zamanlı rapor
  yok, alert yok, as-code semantik yok**, çok-kiracılık koda bırakılmış.

---

## 4. Özellik matrisi

Legend: **✓** var/güçlü · **~** kısmi/ek ücretli/orta · **✗** yok/zayıf · **○** LST-KDS'te
*planlı ama henüz yok*. LST-KDS sütunu [00-analiz.md](00-analiz.md) §11 + menü koduna göre.

| Yetenek | PowerBI | Metabase | Superset | GoodData | BoldBI | Qrvey | Luzmo | **LST-KDS (plan)** |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| Sürükle-bırak sorgu tasarımcısı | ✓ | ✓ | ✓ | ~ | ✓ | ✓ | ✓ | **○ (S-planlı, çekirdek)** |
| Ham SQL editörü | ~ | ✓ | ✓ | ~ | ✓ | ~ | ✗ | **○ (Monaco, SELECT-only)** |
| Semantik model / metrik katmanı | ✓ | ✓ | ~ | ✓✓ | ~ | ~ | ~ | **○ (Veri Haritaları + Metrikler)** |
| Canlı (DirectQuery) modu | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | **○ (Mod A)** |
| İçe alım (Import) modu | ✓ | ~ | ✗ | ~ | ✓ | ✓ | ✓ | **○ (Mod B, Hangfire)** |
| Dashboard + görselleştirme | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | **○ (ECharts + grid)** |
| Çok-kiracılık / tenant izolasyonu | ✓ | ~ | ~ | ✓✓ | ✓ | ✓ | ✓ | **○ (TenantId + tenant DB)** |
| Satır-düzeyi güvenlik (RLS) | ✓ | ~ | ✓ | ✓ | ✓ | ✓ | ✓ | **✗ (rapor/klasör yetkisi var, veri-RLS yok)** |
| RBAC roller + audit | ✓ | ~ | ✓ | ✓ | ✓ | ✓ | ~ | **○ (roller + Denetim ekranı)** |
| Kullanım analizi (usage) | ✓ | ~ | ~ | ~ | ✓ | ~ | ~ | **○ (Kullanım ekranı)** |
| Zamanlanmış rapor / abonelik | ✓ | ✓ | ✓ | ✓ | ✓✓ | ✓ | ✓ | **✗ (v1 DIŞI — §11)** |
| Eşik uyarıları (data alerts) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | **✗ (yok)** |
| Doğal dille sorgu / AI (NLQ) | ✓ | ✓ | ~ | ✓ | ✓ | ✓ | ✓✓ | **✗ (v1 DIŞI — §11)** |
| Gömme (embedding) / white-label | ✓ | ~ | ✓ | ✓ | ✓ | ✓ | ✓ | **✗ (v1 DIŞI — §11)** |
| Mobil (native/responsive) | ✓ | ~ | ~ | ~ | ✓ | ~ | ~ | **✗ (v1 DIŞI — §11)** |
| Excel/CSV export | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | **○ (ClosedXML)** |
| PDF export | ✓ | ✓ | ✓ | ✓ | ✓ | ~ | ✓ | **✗ (v1 DIŞI — §11, karar bekliyor)** |

**Matrisin söylediği:** Çekirdek (üst blok) rakiplerle **eşit** planlıyoruz — burada
geri değiliz. Fark **alt blokta**: RLS, zamanlanmış rapor, eşik uyarıları, NLQ, embedding,
mobil — bunların çoğu piyasada **standart**, bizde **yok/ertelenmiş**.

---

## 5. 2026 trendleri (piyasa nereye gidiyor)

| Trend | Ne | Kim | LST-KDS için önemi |
|---|---|---|---|
| **NLQ / conversational** | Doğal dil + takip sorusu; sürükle-bırak yerine yaz/ara | ThoughtSpot, Power BI Copilot, Tableau Pulse, Luzmo IQ, Qlik Answers | **Giriş bileti** (Gartner: 2026'da sorguların %50+'si NL/arama). **Semantik katman ön koşulu** |
| **Agentic / otomatik içgörü** | "Dashboard'a bak" → "içgörü sana gelsin"; anomali tespiti + öneri | Tableau Pulse, Domo, ThoughtSpot, Sisense Pulse | Orta vade; anomali+push yol haritasına |
| **Semantik katman standardı (OSI)** | Metrik tanımının satıcıdan bağımsız değişimi ("USB-C") | Snowflake/dbt/Cube öncülüğünde **OSI → Apache Ossie** (2025-2026) | Metrik katmanımıza **import/export** kapısı; "kapalı ada" riskinden kaçın |
| **Veri yönetişimi + gözlemlenebilirlik** | Kalite/soy/maskeleme; "governance as code" | Purview, Alation, Acceldata | RBAC/RLS/audit + KVKK = kurumsal satışın kapısı |
| **Self-servisin yaygınlaşması** | İş kullanıcısı IT'siz rapor üretir | ~%80 benimseme (2026) | Tam bizim tezimiz; fark artık **tüketici-sınıfı UX** |
| **Gömülü analitik büyümesi** | SaaS içine gömme, API-first, white-label | Qrvey/Luzmo/GoodData/Bold BI | Çok-kiracılı SaaS olarak **ikinci gelir/dağıtım kanalı** |
| **Gerçek-zamanlı analitik** | Sub-saniye canlı/akış | Power BI Direct Lake, ClickHouse | Sektöre göre farklılaştırıcı; depolama/motor kararı erken |

---

## 6. Fiyatlandırma modelleri

**Altı ana kalıp:** per-seat · kapasite · kullanım/kredi (MAU) · flat-rate · tenant/workspace ·
hibrit. **2026'nın baskın deseni hibrit** (öngörülebilir taban + kullanımla büyüyen bileşen);
neredeyse tüm ürünler **üretici (creator, pahalı) ↔ izleyici (viewer, ucuz)** ayrımı yapıyor.

| Ürün | Model | Yaklaşık (üçüncü-taraf) |
|---|---|---|
| Power BI Pro / PPU | per-seat | $14 / $24 kullanıcı/ay |
| Power BI Fabric | kapasite | F2 ~$263/ay … F64 ~$8.400/ay |
| Tableau Creator/Viewer | rol-bazlı seat | ~$75 / ~$15 kullanıcı/ay |
| Qlik | **kapasite** (2025'te per-user kapandı) | ~$2.500–5.000/ay+ |
| Looker | per-seat + platform (Call sales) | Standard ~$60K/yıl tahmini |
| ThoughtSpot | seat / **tüketim** ($0,10/sorgu) | kurumsal $100K–500K/yıl |
| Metabase | flat + per-external-user | $575/ay + $12,50/dış kullanıcı |
| GoodData | **per-workspace** | ~$1.500/ay+ |
| Bold BI | **no-per-user** (custom) | ~$495/ay + community lisans |
| Qrvey | **flat, sınırsız tenant** | ~$1.500–5.000/ay (tahmin) |
| Luzmo | **MAU-bazlı** | ~$995–3.100/ay |
| Holistics | **şeffaf** per-plan | $800–2.000/ay + seat |
| Domo | **consumption-credit** | küçük $50K, kurumsal $200K–500K+/yıl |

**LST-KDS için okuma:** Çok-kiracılı + izleyici-ağırlıklı bir üründe **saf per-seat riskli**.
Önerilen yön: **üretici (gelistirici/admin) için koltuk + izleyici (musteri) için tenant
paketi / kapasite**. Power BI'ın $14 çıpasına karşı **düşük tabanlı + kullanımla büyüyen**
model, Türkiye'nin fiyat-hassas pazarında daha kolay kabul görür. *(Not: fiyatlandırma
[00-analiz.md](00-analiz.md)'de hiç ele alınmadı — bu bir açık; iş modeli kararı gerekiyor.)*

---

## 7. Türkiye / bölgesel pazar

- **Fiilen kullanılan:** Top 3 = **Power BI, Tableau, Qlik** (Power BI bulut fiyatıyla domine).
- **Yerli oyuncular** çoğunlukla **entegratör/bayi** ya da **ERP-içi raporlama modülü**:
  OBASE (MicroStrategy iş ortağı, en köklü BI), Bimser (Ensemble/eBA — BI paket parçası),
  İnnova (Türk Telekom grubu entegratör), Uyumsoft/Doğa Yazılım (ERP+BI raporlama),
  innobi/Datinova (DWH/veri altyapısı, görselleştirme değil).
- **Bağımsız, yerli, self-servis BI görselleştirme motoru kamuya açık kaynaklarda belirgin
  biçimde yok** → **boşluk gerçek.** (Uyarı: kapalı/bayi-kanallı yerli ürünler bu taramada
  görünmemiş olabilir.)
- **Talep bağlamı:** TÜİK'e göre işletmelerin yalnız ~%7,5'i AI kullanıyor (250+ çalışanda
  ~%24) → fiyat-hassas, Türkçe + veri-egemenliği talebi güçlü bir pazar.

**Farklılaşma ekseni:** Türkçe NLQ · KVKK/veri-ikameti (yerli bulut/erişim) · yerel
fiyat/destek · sektörel şablonlar. Asıl rakip küresel (Power BI); yerli rakip zayıf.

---

## 8. LST-KDS boşluk analizi

[00-analiz.md](00-analiz.md) §11 planı ile piyasa gerçeğini yan yana koyunca üç kova çıkıyor.

### ✓ Doğru konumlanmış (çekirdekte rakiplerle eşit)
Sürükle-bırak + ham SQL ikilisi · veri haritası + metrik katmanı · iki çalıştırma modu
(canlı/import) · çok-kiracılık (TenantId + tenant DB) · RBAC + audit + kullanım · dashboard +
Excel/CSV · SELECT-only bekçisi (ScriptDom). **Bunlar piyasa standardıyla uyumlu; devam.**

### ✗ Piyasa standardı ama bizde yok/ertelenmiş (kritik boşluklar)
| Boşluk | Durum | Piyasa | Risk |
|---|---|---|---|
| **Satır-düzeyi güvenlik (RLS)** | Yok (rapor/klasör yetkisi var, veri-RLS yok) | **Herkeste** (Superset/GoodData/Bold BI/Qrvey…) | Çok-kiracılı SaaS'ta veri-RLS olmadan kurumsal satış zor; tek tenant içinde kullanıcı-bazlı kısıt yapılamaz |
| **Zamanlanmış rapor / abonelik** | v1 DIŞI (§11) | **Herkeste**, olgun (Bold BI FTP/webhook, Metabase alıcı-bazlı filtre) | "İçe alım" veri senkronu var ama **rapor teslimi** yok — temel bir beklenti eksik |
| **Eşik uyarıları (data alerts)** | Yok (analizde hiç geçmiyor) | **Herkeste** (eşik + anomali; Domo çok-kanal) | Proaktif değer (push) rakiplerde standart |
| **Doğal dille sorgu / AI (NLQ)** | v1 DIŞI (§11) | **2026 giriş bileti** (Gartner %50+) | En hızlı büyüyen beklenti; veri haritamız hazır temeli |
| **Gömme (embedding) / white-label** | v1 DIŞI (§11) | En yakın segmentin **merkezi** | Çok-kiracılı SaaS'ız; ikinci gelir kanalını kapatıyoruz |
| **Mobil / responsive** | v1 DIŞI (§11) | Çoğunda native app | En azından **responsive** olmadan modern kabul görmez |

### ★ Farklılaştırma fırsatları (bizi ayıracak yerler)
- **Türkçe-yerel + KVKK/veri-egemenliği** — küresel rakiplerde yok, yerli rakip zayıf.
- **İki-mod + çok-kiracılık + zamanlama/alert'i tek, hafif, şeffaf-fiyatlı üründe** —
  Qrvey'in gücü ama ES-kümesi operasyon yüküyle; biz managed + basit sunabiliriz.
- **Governance temellerini paywall'suz ver** — Metabase/Grafana RLS/SSO/audit'i ücretli
  katmana koyuyor; bizde çekirdek olması güven farkı yaratır.
- **Şeffaf, hibrit fiyat** — Power BI'ın $14 çıpasına karşı düşük-taban + tenant paketi.
- **Sürükle-bırak ↔ SQL round-trip + "SQL'i Göster"** — [00-analiz.md](00-analiz.md) §5 zaten
  bu yolda; Metabase notebook↔SQL kalitesine çekmek net bir ürün-kalitesi hedefi.

---

## 9. Öncelikli öneri listesi

> Sıralama ilkesi: (1) çekirdeği bitir, (2) piyasa-standardı boşlukları kapat, (3)
> farklılaştırıcıya yatır. Her madde bir **roadmap kararı** gerektirir — bu doküman öneri,
> karar kullanıcının.

**Kademe 0 — v1 çekirdeği (zaten planlı, teyit):** sorgu tasarımcısı · ham SQL · veri
modeli + metrikler · iki mod · dashboard · çok-kiracılık · RBAC/audit/kullanım · Excel/CSV.
→ *Değişiklik yok; matrisin üst bloğu rakiplerle eşit.*

**Kademe 1 — v1'e ALINMASI önerilenler (piyasa "olmazsa-olmaz"ı, düşük-orta maliyet):**
1. **Satır-düzeyi güvenlik (RLS).** Superset modeli: üretilen sorguya user/tenant-attribute'lu
   WHERE, ama ScriptDom bekçisiyle enjeksiyona kapalı. Çok-kiracılı SaaS'ta bu neredeyse
   zorunlu; ertelemenin bedeli kurumsal satışı bloklamak. *(§11'de yok — eklenmeli.)*
2. **Zamanlanmış rapor + eşik uyarıları.** Hangfire zaten var (içe-alım için); aynı altyapı
   rapor teslimi + alert değerlendirmesini de taşır. Başlangıç: e-posta + Excel/CSV,
   alıcı-bazlı filtre. Düşük ek maliyet, yüksek beklenti karşılığı. *(§11 "zamanlanmış rapor
   e-postası"nı DIŞI bırakmış — yeniden değerlendir.)*

**Kademe 2 — v1.x (farklılaştırıcı, orta maliyet):**
3. **Veri haritasına grounded NLQ (AI).** ÖNCE metrik katmanı olgunlaşsın, SONRA AI. Model:
   ThoughtSpot/Looker gibi **tanımlı metriklere bağlı, "SQL'i Göster" ile açıklanabilir**,
   LLM-agnostik (Domo/Qrvey gibi Claude dahil takılabilir). Türkçe NLQ = yerel farklılaşma.
4. **Responsive + PWA** (native app değil). Mevcut PWA/çevrimdışı deneyimi (QMS'te var) ilham.

**Kademe 3 — v2 (stratejik, yüksek maliyet):**
5. **Gömme (embedding) + white-label** — çok-kiracılık + RLS hazırsa ikinci gelir kanalı.
   Referans: Luzmo embed-token parametre override, GoodData signed embed.
6. ~~**OSI / Apache Ossie uyumu** — metrik katmanına import/export; standart olgunlaşınca değer.~~ → **✅ S46'da yapıldı (2 Eyl 2026):** spec v0.1 biçiminde dışa/içe aktarım (roadmap S46).
7. **Anomali tespiti + push (agentic yön)** — "içgörü sana gelsin".

**Kademe 4 — iş modeli kararı (koddan bağımsız, acil değil ama gerekli):**
8. **Fiyatlandırma modeli.** Analizde hiç yok. Öneri: **hibrit** (gelistirici/admin seat +
   musteri için tenant paketi/kapasite), düşük-taban. Bu bir ürün-yönetimi kararı.

---

## 10. Kaynaklar (segment bazında ana referanslar)

**Kurumsal:** microsoft.com/power-platform/power-bi · learn.microsoft.com (Power BI/Fabric) ·
tableau.com + help.tableau.com · help.qlik.com + qlik.dev · cloud.google.com/looker ·
thoughtspot.com + docs.thoughtspot.com

**Açık kaynak:** metabase.com (pricing, semantic-layer, metabot, RLS) · superset.apache.org
(RLS docs) · redash.io · grafana.com · lightdash / evidence (modern-datatools.com)

**Gömülü/çok-kiracılı:** help.boldbi.com + boldbi.com · qrvey.com + qrvey-docs · gooddata.ai/docs
(multitenancy, MAQL, FlexQuery) · luzmo.com + academy.luzmo.com · holistics.io + docs.holistics.io
· docs.sisense.com · knowledge.domo.com + domo.com · docs.explo.co (Omni satın alma:
omni.co/blog) · revealbi.io

**Trend/fiyat/Türkiye:** gartner.com (2026 tahminleri, agentic analytics) · snowflake.com
(OSI/Apache Ossie) · cube.dev · toucantoco.com + luzmo.com + holistics.io (fiyat karşılaştırma) ·
obase.com · powerbi.istanbul · sheltron.com.tr (TÜİK/Türkiye AI)

> Tam URL listesi araştırma ajanı çıktılarında; kritik iddialar (OSI kronolojisi, Explo
> satın alma, MediatR v13 ticari, fiyat modelleri) çapraz doğrulandı. Fiyatlar üçüncü-taraf.
