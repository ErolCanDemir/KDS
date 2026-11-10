import { apiSend } from './client';

// Backend Kds.Contracts/QueryModel ile birebir. Enum'lar string (JsonStringEnumConverter).
// Filtre polimorfiktir: 'tur' ayrımcısı ('grup'|'kosul') ve İLK alan olmalı (System.Text.Json).

export type AggregationType = 'Yok' | 'Topla' | 'Ortalama' | 'EnAz' | 'EnCok' | 'Say' | 'BenzersizSay';
export type LogicConnector = 'Ve' | 'Veya';
export type ComparisonOp =
  | 'Esit' | 'EsitDegil' | 'Buyuk' | 'BuyukEsit' | 'Kucuk' | 'KucukEsit'
  | 'Icinde' | 'Arasinda' | 'Icerir' | 'EmptyState' | 'DoluDegil';

// Y4 — tarih kırılımı (kova). Dönem BAŞLANGICI ailesi (Yil…Gun) o dönemin ilk gününü döner
// (kronolojik sıralanır); PARÇA ailesi (YilNo…SaatNo) yıldan bağımsız sayıdır (mevsimsellik).
export type TarihKovasi = 'Yil' | 'Ceyrek' | 'Ay' | 'Hafta' | 'Gun' | 'YilNo' | 'CeyrekNo' | 'AyNo' | 'SaatNo';
export type SelectedField = { tabloId: string; kolon: string; toplama: AggregationType; takmaAd?: string | null; kova?: TarihKovasi | null };
export type FilterCondition = { tur: 'kosul'; tabloId: string; kolon: string; op: ComparisonOp; degerler: unknown[] };
export type FilterGroup = { tur: 'grup'; baglac: LogicConnector; cocuklar: FilterNode[] };
export type FilterNode = FilterGroup | FilterCondition;
// Y2: cikisAdi dolu ise sıralama bir ÇIKIŞ KOLONUNU işaret eder (hesaplanan/özet/ölçü kolonu
// dahil) ve tablo/kolon yok sayılır.
export type SortItem = { tabloId: string; kolon: string; toplama: AggregationType; azalan: boolean; cikisAdi?: string | null; kova?: TarihKovasi | null };
export type JoinSelection = { kaynakTabloId: string; kaynakKolon: string; hedefTabloId: string; hedefKolon: string };
// Y4: kova dolu ise gruplama kolonun kendisine değil tarih kırılımına yapılır (SELECT ile aynı).
export type GroupingItem = { tabloId: string; kolon: string; kova?: TarihKovasi | null };
// İlişkili özet alan: farklı granülaritede, kendi filtreli özet kolonu (ör. servis başına aktif
// çalışan). Üretici fan-trap'siz türetilmiş-tablo LEFT JOIN'e çevirir; filtre yalnız özet tablosunun
// kolonlarını kullanabilir.
export type SummaryField = {
  tabloId: string; kolon: string; toplama: AggregationType; bagTabloId: string;
  takmaAd?: string | null; filtre?: FilterNode | null;
};
// Birleşik kolon: kolonlar + sabit metin ayraçları tek çıktı kolonunda (CONCAT; sabitler parametre).
export type CompositePart = { tabloId?: string | null; kolon?: string | null; sabit?: string | null };
export type CompositeField = { parcalar: CompositePart[]; takmaAd?: string | null };
// Koşullu ölçü: AGG(CASE WHEN koşul THEN değer …) — ör. garanti kapsamındaki talep sayısı.
// kolon null = sabit 1 (adet); Topla ELSE 0, diğerleri ELSE'siz (eşleşmeyen hesaba katılmaz).
export type ConditionalMeasure = {
  toplama: AggregationType; filtre: FilterNode;
  tabloId?: string | null; kolon?: string | null; takmaAd?: string | null;
};
// M29: satır-düzeyi CASE kolonu — CASE WHEN … THEN … [ELSE …] END (aggregate YOK). Dal sonucu
// ya kolon (tabloId+kolon) ya sabit; varsayılan (ELSE) kolon/sabit/boş (NULL).
export type CaseBranch = { filtre: FilterNode; tabloId?: string | null; kolon?: string | null; sabit?: string | null };
export type CaseColumn = {
  dallar: CaseBranch[]; takmaAd?: string | null;
  varsayilanTabloId?: string | null; varsayilanKolon?: string | null; varsayilanSabit?: string | null;
};
// Join Yolları adımı: tablo başına ebeveyn + tür + (ops.) bileşik koşullar; kök tablo seçimi.
export type JoinType = 'Inner' | 'Left';
// M5: ebeveynTabloId dolu ise koşul SORGUDAKİ başka bir tabloya başvurur (null = ayarın ana ebeveyni).
export type JoinCondition = { tabloKolon: string; ebeveynKolon: string; ebeveynTabloId?: string | null };
// Y13: ekKosullar JOIN'in ON'una eklenir — LEFT join'de ana satırı ELEMEZ (WHERE'den farkı budur).
export type JoinSetting = {
  tabloId: string; ebeveynTabloId: string; tur: JoinType;
  kosullar?: JoinCondition[] | null; ekKosullar?: FilterCondition[] | null;
};

// Parametreli rapor: çalıştırma-anı parametreleri. Filtre değeri sabit yerine { $param: anahtar }
// (TarihAralik'ta { $param, parca: 'baslangic'|'bitis' }) ile buna bağlanır.
export type ParametreTipi = 'Metin' | 'Sayi' | 'Tarih' | 'TarihAralik' | 'EvetHayir' | 'Liste';
export type ParametreSecenek = { deger: unknown; etiket: string };
export type ReportParameter = {
  anahtar: string;
  etiket: string;
  tip: ParametreTipi;
  zorunlu?: boolean;
  varsayilan?: unknown;
  secenekler?: ParametreSecenek[] | null;
};
export type ParametreDegerleri = Record<string, unknown>;

export type QueryModel = {
  haritaId: string;
  alanlar: SelectedField[];
  filtre: FilterNode | null;
  siralama: SortItem[];
  satirLimiti: number | null;
  joinler?: JoinSelection[] | null;         // aynı çift için hangi ilişki (null = otomatik ilk)
  gruplamalar?: GroupingItem[] | null;  // elle GROUP BY (null = otomatik)
  ozetAlanlar?: SummaryField[] | null;       // ilişkili özet alanlar (null = yok)
  bilesikAlanlar?: CompositeField[] | null; // birleşik kolonlar (null = yok)
  // Elle SQL koşulu: {Tablo.Kolon} yer tutucuları alias'a çevrilir, filtrelerle AND'lenir.
  elleKosul?: string | null;
  kokTabloId?: string | null;            // FROM kökü (null = ilk alanın tablosu)
  joinAyarlari?: JoinSetting[] | null;     // tablo başına join tanımı (null = otomatik)
  ekTablolar?: string[] | null;            // M17: alansız tablolar — SELECT'e girmez, yalnız JOIN'e katılır
  tabloSirasi?: string[] | null;           // M18: Join Yolları tablo sırası (otomatik ebeveyn seçim önceliği)
  elleKosulBaglaci?: LogicConnector;      // elle koşul filtrelerle nasıl birleşir (varsayılan Ve)
  kosulluOlculer?: ConditionalMeasure[] | null; // koşullu ölçüler (CASE WHEN; null = yok)
  parametreler?: ReportParameter[] | null;      // çalıştırma-anı parametreleri (null = yok)
  caseAlanlar?: CaseColumn[] | null;            // M29: satır-düzeyi CASE kolonları (null = yok)
  ozetKosullar?: SummaryCondition[] | null;     // Y3: gruplama SONRASI koşullar → HAVING
  hesaplananAlanlar?: ComputedColumn[] | null; // Y1: formül kolonları
  metrikAlanlar?: MetricFieldRef[] | null; // S22: katalog metrik referansları (formül koşumda çözülür)
  ifadeKosullari?: ExpressionCondition[] | null; // Y2: formül koşulları → WHERE ya da HAVING
  pencereKolonlari?: WindowColumn[] | null;      // Y6: OVER(...) kolonları
  grupIciIlkN?: TopNPerGroup | null;             // Y7: her bölümden ilk N satır
  araKumeGruplari?: string[] | null;             // Y8: dış katman kırılımı (çıkış kolonu adları)
  araKumeOzeti?: OuterAggregate[] | null;        // Y8: dış katman toplamaları ("özetin özeti")
  capraz?: PivotSpec | null;                     // Y9: çapraz tablo (PIVOT)
  araToplam?: SubtotalSpec | null;               // Y10: ara/genel toplam satırları
  varlikKosullari?: ExistenceCondition[] | null; // Y11: kaydı var/yok (EXISTS / NOT EXISTS)
  tekille?: boolean;                             // Y12: SELECT DISTINCT
  atla?: number | null;                          // Y16: sayfalama (OFFSET) — limit sayfa boyudur
  tabloOrnekleri?: TableInstance[] | null;       // Y14: aynı tablonun ikinci örneği (self-join)
  birlesim?: QueryModel[] | null;                // Y15: alt alta eklenecek sorgular (UNION)
  birlesimTekille?: boolean;                     // Y15: UNION (true) / UNION ALL (false)
};

/** Y14 — tablo örneği: haritadaki bir tablonun sorgu içindeki İKİNCİ kimliği ("getiren kişi" ile
 * "danışman" aynı tablodan gelir ama ayrı satırlardır). ornekId modelin geri kalanında normal bir
 * tablo kimliği gibi kullanılır. */
export type TableInstance = { ornekId: string; kaynakTabloId: string; etiket?: string | null };

/** Y9 — çapraz tablo: bir kolonun DEĞERLERİ sütun başlığı olur. Sütun listesi SABİTtir. */
export type PivotSpec = {
  baslikKolonu: string; degerKolonu: string; basliklar: string[]; toplama: AggregationType;
};

/** Y10 — ara toplam satırları. Kademeli = her kırılım düzeyi (ROLLUP). */
export type AraToplamTuru = 'GenelToplam' | 'Kademeli';
export type SubtotalSpec = { tur: AraToplamTuru; etiket: string; etiketle: boolean };

/** Y11 — "eşleşen kaydı olanlar/olmayanlar" (EXISTS / NOT EXISTS): satır çoğaltmaz, kolon eklemez. */
export type ExistenceLink = { kolon: string; anaTabloId: string; anaKolon: string };
export type ExistenceCondition = {
  tabloId: string; yok?: boolean; baglar?: ExistenceLink[] | null;
  filtre?: FilterNode | null; baglac?: LogicConnector;
};

/** Y8 — ara küme özeti: dış katmanda, iç sorgunun ÇIKIŞ KOLONU üzerinde toplama. */
export type OuterAggregate = { toplama: AggregationType; cikisAdi: string; takmaAd?: string | null };

/** Y7 — grup içi ilk N: "her marka için en çok satan 3 ürün". Satır limiti sonucun tamamını
 * kırpar; bu HER BÖLÜMDEN N satır bırakır. esitleriDeAl: N.'deki beraberlikte hepsi kalsın mı. */
export type TopNPerGroup = {
  sira: WindowSort[]; adet: number; bolum?: string[] | null; esitleriDeAl?: boolean;
};

/** Y6 — pencere işlevi (satırı komşularına göre değerlendirir). */
export type PencereIslevi =
  | 'SiraNo' | 'Sira' | 'YogunSira' | 'Dilim' | 'Onceki' | 'Sonraki'
  | 'KosanToplam' | 'BolumToplami' | 'HareketliOrtalama';
export type WindowSort = { ifade: string; azalan?: boolean };
/** deger/bolum/sira Y1-Y2 formül dilindedir: {Tablo.Kolon} ya da [Çıkış Kolonu]. */
export type WindowColumn = {
  islev: PencereIslevi; deger?: string | null; bolum?: string[] | null;
  sira?: WindowSort[] | null; sayi?: number | null; takmaAd?: string | null;
};

/** Y2 — formül koşulu: sol taraf bir HESAP ({T.K} * 1.18) ya da [Çıkış Kolonu] atfı; sağ taraf
 * her filtre gibi operatör + değer. Toplama içeriyorsa backend HAVING'e, yoksa WHERE'e koyar. */
export type ExpressionCondition = {
  ifade: string; op: ComparisonOp; degerler: unknown[]; baglac?: LogicConnector;
};

/** Y3 — özet koşulu (HAVING): AGG(tablo.kolon) OP değer. Filtre (WHERE) satır bazındadır ve
 * gruplamadan ÖNCE eler; bu ise grubun ÖZETİNE bakar. baglac bir öncekiyle ilişkidir. */
export type SummaryCondition = {
  toplama: AggregationType; tabloId: string; kolon: string;
  op: ComparisonOp; degerler: unknown[]; baglac?: LogicConnector;
};

// M33 "Sorun Ne?": çalıştırmadan tahmini plandan çıkarılan tanı bulguları.
export type DiagnoseFinding = {
  baslik: string; aciklama: string; siddet: 'yuksek' | 'orta' | 'bilgi';
  oneri?: string | null; kopyalanabilirSql?: string | null;
};
export type QueryDiagnosis = { ozet: string; bulgular: DiagnoseFinding[] };

export type GeneratedQuery = { sql: string; parametreler: Record<string, unknown> };
export type QueryColumnDto = { ad: string; veriTipi: string };
export type QueryRunResult = {
  kolonlar: QueryColumnDto[];
  satirlar: unknown[][];
  satirSiniriUygulandi: boolean;
  sureMs: number;
};

/** Modeli parametreli SQL'e çevirir (çalıştırmadan) — "SQL'i Göster". Parametre değerleri
 * verilirse önizleme o değerlerle (yoksa parametre varsayılanlarıyla) üretilir. */
export const generateQuerySql = (model: QueryModel, parametreDegerleri?: ParametreDegerleri, signal?: AbortSignal) =>
  apiSend<GeneratedQuery>('/api/sorgu/sql', 'POST', { model, parametreDegerleri: parametreDegerleri ?? null }, undefined, signal);

/** Modeli SQL'e çevirip haritanın bağlantısında çalıştırır — "Çalıştır". */
export const runQuery = (model: QueryModel, parametreDegerleri?: ParametreDegerleri, signal?: AbortSignal) =>
  apiSend<QueryRunResult>('/api/sorgu/calistir', 'POST', { model, parametreDegerleri: parametreDegerleri ?? null }, undefined, signal);

/** Ham SQL ekranı (S9): elle yazılan SELECT'i seçilen bağlantıda çalıştırır (aynı tek kapı).
 * M11: SQL'deki @parametreler için değerler `parametreler` sözlüğüyle gider — metin yerine
 * GERÇEK SqlParameter olarak bağlanır (enjeksiyon-güvenli "değeri yerine koyma"). */
/** M33 "Sorun Ne?" — tasarımcı: modeli tanılar (çalıştırmaz). */
export const diagnoseQueryModel = (model: QueryModel, parametreDegerleri?: ParametreDegerleri, signal?: AbortSignal) =>
  apiSend<QueryDiagnosis>('/api/sorgu/tanila-model', 'POST', { model, parametreDegerleri: parametreDegerleri ?? null }, undefined, signal);

/** M33 "Sorun Ne?" — SQL editörü: ham SELECT'i tanılar (çalıştırmaz). */
export const diagnoseRawSql = (baglantiId: string, sql: string, parametreler?: Record<string, unknown> | null, signal?: AbortSignal) =>
  apiSend<QueryDiagnosis>('/api/sorgu/tanila', 'POST', { baglantiId, sql, parametreler: parametreler ?? null }, undefined, signal);

export const runRawSql = (
  baglantiId: string, sql: string, satirSiniri?: number | null,
  parametreler?: Record<string, unknown> | null, signal?: AbortSignal,
) =>
  apiSend<QueryRunResult>('/api/sorgu/ham', 'POST',
    { baglantiId, sql, satirSiniri: satirSiniri ?? null, parametreler: parametreler ?? null }, undefined, signal);

/** Y1 — hesaplanan (formül) kolon: SELECT'e serbest HESAP. Kolonlar {Tablo.Kolon} yer tutucusuyla
 * yazılır; toplama içeren ifade grup düzeyi, içermeyen satır düzeyidir (backend karar verir). */
export type ComputedColumn = { ifade: string; takmaAd?: string | null };

/** S22 — katalog metriği referansı: modele formülün kopyası değil kimliği girer. */
export type MetricFieldRef = { metrikId: string; takmaAd: string | null };
