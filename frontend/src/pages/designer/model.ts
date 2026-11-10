import type { DataMapDetail } from '../../api/dataMaps';
import type {
  QueryModel, FilterCondition, FilterNode, JoinSelection, GroupingItem, JoinSetting, JoinType, LogicConnector,
  ReportParameter, SortItem, TableInstance,
} from '../../api/queries';
import {
  pairKey, isValueless, aggLabel,
  type EditField, type EditFilter, type EditParametre, type EditSort, type EditSummary, type EditComposite,
  type EditConditional, type EditCaseAlan, type EditOzetKosul, type EditHesap, type EditJoinSetting,
  type EditIfadeKosul, type EditPencere, type EditIlkN, type EditAraKume,
  type EditCapraz, type EditAraToplam, type EditVarlik,
} from './shared';

// ---- Sorgu Tasarımcısı SAF çekirdeği (Y4 refactor: bölüm 2) ----
// React'a bağımlı DEĞİL: birim test edilebilir (vitest hedefi). UI yalnız sarmalar.

export type TreeEdge = {
  ebeveyn: string; tur: JoinType; etiket: string; ozel: boolean; ayarli: boolean; araMi?: boolean;
};

/** Etkin join ağacı — backend JoinPlani'nin istemci yansıması (birebir aynı kural, test turu
 * M4/M13/M15). Eski kör BFS haritanın TÜMÜNDE ilişki-dizisi sırasıyla koşuyordu: kanvastaki tablo,
 * kanvasta OLMAYAN bir ara tablo üzerinden erken keşfedilebiliyor (kenar çizilmiyordu) ya da
 * kestirme FK'ya bağlanıyordu. Yeni kural:
 * (a) ayarlı tablo, ebeveyni yerleşince yerleşir;
 * (b) kanvastaki tablo MÜMKÜNSE kanvastaki bir tabloya DOĞRUDAN bağlanır; birden çok aday varsa
 *     EN DERİN yerleşik ebeveyn kazanır (snowflake sezgisi), eşitlikte ilişki sırası;
 * (c) doğrudan bağ kalmadıysa yerleşik kümeden çok-kaynaklı BFS ile en yakın kanvas tablosuna
 *     ARA-zincir kurulur (ara üyeler kanvasta çizilmez — araMi). */
export function buildTree(
  detay: DataMapDetail | null,
  etkinKok: string,
  kullanilan: string[],
  joinAyarlari: Record<string, EditJoinSetting>,
  joinSecim: Record<string, JoinSelection>,
  /** M36: OTOMATİK satırlar için yalnız join TÜRÜ ezmesi (INNER→LEFT). Satır otomatik kalır —
   * ebeveyn her değişiklikte yeniden hesaplanır; kullanıcı kolonlara dokunursa "ayarlı" olur. */
  joinTurleri: Record<string, JoinType> = {},
): Map<string, TreeEdge> {
  const m = new Map<string, TreeEdge>();
  if (!detay || !etkinKok) return m;
  const ad = (tid: string) => { const t = detay.tablolar.find((x) => x.id === tid); return t ? (t.dostcaAd || t.hamAd) : '?'; };
  const ciftEtiket = (tid: string, ebeveyn: string) => {
    const k = pairKey(tid, ebeveyn);
    const il = joinSecim[k]
      ?? detay.iliskiler.find((i) => pairKey(i.kaynakTabloId, i.hedefTabloId) === k);
    if (!il) return '?';
    // M14 (yalnız görsel): eşitliğin SOL tarafı hep SATIRIN tablosu — ilişki haritada ters
    // yönlü kayıtlıysa taraflar çevrilerek yazılır (join mantığı değişmez).
    const duz = il.kaynakTabloId === tid;
    const solT = duz ? il.kaynakTabloId : il.hedefTabloId;
    const solK = duz ? il.kaynakKolon : il.hedefKolon;
    const sagT = duz ? il.hedefTabloId : il.kaynakTabloId;
    const sagK = duz ? il.hedefKolon : il.kaynakKolon;
    return `${ad(solT)}.${solK} = ${ad(sagT)}.${sagK}`;
  };

  const derinlik = new Map<string, number>([[etkinKok, 0]]);
  const secilen = new Map<string, string>(); // tablo → seçilen ebeveyn (oto)
  const yerlesikSira: string[] = [etkinKok];
  const yerlesti = new Set<string>([etkinKok]);
  const bekleyen = kullanilan.filter((t) => t !== etkinKok);
  const yerlestir = (t: string, ebeveyn: string, oto: boolean) => {
    yerlesti.add(t);
    yerlesikSira.push(t);
    derinlik.set(t, (derinlik.get(ebeveyn) ?? 0) + 1);
    if (oto) secilen.set(t, ebeveyn);
  };

  let ilerleme = true;
  while (ilerleme && bekleyen.some((t) => !yerlesti.has(t))) {
    ilerleme = false;
    // (a) ayarlılar: kullanıcı ebeveyni sabitledi — EBEVEYN ve koşullarının başvurduğu TÜM tablolar
    //     yerleşince yerleşir (M5: koşul tabloları mantıksal ebeveyndir; erken yerleşirse otomatik
    //     tablolar ona bağlanıp döngü doğurabiliyordu).
    for (const t of bekleyen) {
      if (yerlesti.has(t)) continue;
      const ayar = joinAyarlari[t];
      if (!ayar?.ebeveynTabloId) continue;
      const dayanaklar = [ayar.ebeveynTabloId, ...ayar.kosullar.map((k) => k.ebeveynTabloId ?? ayar.ebeveynTabloId)];
      if (dayanaklar.every((x) => yerlesti.has(x))) {
        yerlestir(t, ayar.ebeveynTabloId, false);
        // derinlik = en derin dayanak + 1 (en-derin-ebeveyn seçimi tutarlı kalsın).
        derinlik.set(t, Math.max(...dayanaklar.map((x) => derinlik.get(x) ?? 0)) + 1);
        ilerleme = true;
      }
    }
    if (ilerleme) continue;

    // (b) doğrudan bağ: ekleme sırasıyla ilk uygun tablo; en derin yerleşik komşu kazanır.
    for (const t of bekleyen) {
      if (yerlesti.has(t) || joinAyarlari[t]) continue;
      let aday: string | null = null;
      for (const il of detay.iliskiler) {
        const p = il.kaynakTabloId === t && yerlesti.has(il.hedefTabloId) ? il.hedefTabloId
          : il.hedefTabloId === t && yerlesti.has(il.kaynakTabloId) ? il.kaynakTabloId : null;
        if (p && (aday === null || (derinlik.get(p) ?? 0) > (derinlik.get(aday) ?? 0))) aday = p;
      }
      if (aday) { yerlestir(t, aday, true); ilerleme = true; break; }
    }
    if (ilerleme) continue;

    // (c) ara-zincir: yerleşik kümeden dışarı BFS; ulaşılan İLK bekleyen kanvas tablosu,
    //     aradaki (kanvas dışı) tablolarla birlikte zincir hâlinde yerleşir.
    const bfsParent = new Map<string, string>();
    const gorulen = new Set(yerlesti);
    const kuyruk = [...yerlesikSira];
    let hedef: string | null = null;
    while (kuyruk.length > 0 && hedef === null) {
      const u = kuyruk.shift()!;
      for (const il of detay.iliskiler) {
        const v = il.kaynakTabloId === u ? il.hedefTabloId : il.hedefTabloId === u ? il.kaynakTabloId : null;
        if (!v || gorulen.has(v)) continue;
        gorulen.add(v);
        bfsParent.set(v, u);
        if (bekleyen.includes(v) && !joinAyarlari[v]) { hedef = v; break; }
        kuyruk.push(v);
      }
    }
    if (hedef !== null) {
      const zincir: string[] = [];
      for (let c: string = hedef; !yerlesti.has(c); c = bfsParent.get(c)!) zincir.unshift(c);
      for (const c of zincir) yerlestir(c, bfsParent.get(c)!, true);
      ilerleme = true;
    }
  }

  for (const tid of kullanilan) {
    if (tid === etkinKok) continue;
    const ayar = joinAyarlari[tid];
    const ebeveyn = ayar?.ebeveynTabloId ?? secilen.get(tid);
    if (!ebeveyn) continue; // yol gerçekten yok → kenar çizilmez (Adım 2 uyarır)
    const araMi = !kullanilan.includes(ebeveyn); // ebeveyn kanvasta değil (ara tablo)
    if (ayar) {
      const etiket = ayar.kosullar.length > 0
        // M5: farklı tabloya başvuran koşulda tablo adı da görünsün (ss.ParcaId = TalepParcalari.ParcaId).
        ? ayar.kosullar.map((ks) => {
            const hedefTablo = ks.ebeveynTabloId && ks.ebeveynTabloId !== ebeveyn ? `${ad(ks.ebeveynTabloId)}.` : '';
            return `${ks.tabloKolon} = ${hedefTablo}${ks.ebeveynKolon}`;
          }).join(' VE ')
        : ciftEtiket(tid, ebeveyn);
      m.set(tid, { ebeveyn, tur: ayar.tur, etiket, ozel: true, ayarli: true, araMi });
    } else {
      m.set(tid, {
        ebeveyn, tur: joinTurleri[tid] ?? 'Inner', etiket: ciftEtiket(tid, ebeveyn),
        ozel: !!joinSecim[pairKey(tid, ebeveyn)], ayarli: false, araMi,
      });
    }
  }
  return m;
}

/** Filtre satırı → API değer listesi. paramAnahtar dolu ise sabit değer yerine parametre-referansı
 * ({ $param }) üretilir — TarihAralik iki uca (BETWEEN), diğerleri tek değere bağlanır. */
const conditionValues = (
  f: { op: EditFilter['op']; d1: string; d2: string; paramAnahtar?: string },
  parametreler: EditParametre[] = [],
): unknown[] => {
  if (isValueless(f.op)) return [];
  if (f.paramAnahtar) {
    const p = parametreler.find((x) => x.anahtar === f.paramAnahtar);
    return p?.tip === 'TarihAralik'
      ? [{ $param: f.paramAnahtar, parca: 'baslangic' }, { $param: f.paramAnahtar, parca: 'bitis' }]
      : [{ $param: f.paramAnahtar }];
  }
  return f.op === 'Arasinda' ? [f.d1, f.d2]
    : f.op === 'Icinde' ? f.d1.split(',').map((x) => x.trim()).filter((x) => x.length > 0)
      : [f.d1];
};

export type ModelInputs = {
  haritaId: string;
  alanlar: EditField[];
  filtreler: EditFilter[];
  siralar: EditSort[];
  limit: string;
  joinSecim: Record<string, JoinSelection>;
  gruplamalar: GroupingItem[] | null;
  ozetler: EditSummary[];
  bilesikler: EditComposite[];
  kosullular: EditConditional[];
  /** M29: satır-düzeyi CASE kolonları. */
  caseAlanlar: EditCaseAlan[];
  /** Y1: hesaplanan (formül) kolonlar. */
  hesaplananlar: EditHesap[];
  // S22: katalog metrik referanslari — modele formulun kopyasi degil KIMLIGI girer.
  metrikler: { metrikId: string; ad: string }[];
  /** Y3: gruplama SONRASI koşullar (HAVING). */
  ozetKosullar: EditOzetKosul[];
  /** Y2: formül koşulları (backend WHERE/HAVING'i kendi seçer). */
  ifadeKosullari: EditIfadeKosul[];
  /** Y6: pencere kolonları (OVER). */
  pencereler: EditPencere[];
  /** Y7: her bölümden ilk N satır (kapalıysa modele gitmez). */
  ilkN: EditIlkN;
  /** Y8: ara küme (ikinci kat) — sonucun üstünde yeniden kırılım/özet. */
  araKume: EditAraKume;
  /** Y9: çapraz tablo (kapalıysa modele gitmez). */
  capraz: EditCapraz;
  /** Y10: ara toplam satırları (kapalıysa modele gitmez). */
  araToplam: EditAraToplam;
  /** Y11: kaydı var/yok koşulları. */
  varliklar: EditVarlik[];
  /** Y12: yinelenen satırları ele (DISTINCT). */
  tekille: boolean;
  /** Y14: tablo örnekleri (self-join). */
  tabloOrnekleri: TableInstance[];
  /** Y15: bu sorgunun ALTINA eklenecek sorgu modelleri (UNION) + yinelenenler elensin mi. */
  birlesimModelleri: QueryModel[];
  birlesimTekille: boolean;
  /** Y16: sayfalamada atlanacak satır sayısı (boş = sayfalama yok). */
  atla: string;
  parametreler: EditParametre[];
  elleKosul: string;
  elleBaglac: LogicConnector;
  kokTabloId: string;
  joinAyarlari: Record<string, EditJoinSetting>;
  /** M17: alansız eklenen tablolar — SELECT'e girmez, yalnız JOIN'e katılır. */
  sadeceJoinTablolar: string[];
  /** M18: Join Yolları'ndaki kullanıcı sırası (kullanılan tabloların sıralı hâli). */
  tabloSirasi: string[];
  kopukTablolar: string[];
  tableName: (id: string) => string;
};

/** Tasarım durumundan QueryModel kurar — saf; hata durumunda kullanıcıya gösterilecek metni döner. */
export function buildModel(g: ModelInputs): { model: QueryModel } | { hata: string } {
  if (!g.haritaId || g.alanlar.length === 0)
    return { hata: 'En az bir alan seçin (1. adımda soldan sürükle ya da tıkla).' };
  if (g.kopukTablolar.length > 0)
    return {
      hata: `Şu tablolar için haritada join yolu yok: ${g.kopukTablolar.map(g.tableName).join(', ')}. ` +
        'Veri Haritası ekranından ilişki ekleyin ya da bu tabloların alanlarını çıkarın.',
    };

  const kosullar: FilterCondition[] = g.filtreler.map((f) => ({
    tur: 'kosul', tabloId: f.tabloId, kolon: f.kolon, op: f.op, degerler: conditionValues(f, g.parametreler),
  }));
  // M36: satırlar artık VE ya da VEYA ile bağlanabilir (bağlaç satırın BİR ÖNCEKİYLE ilişkisidir).
  const filtreAgacı = filtreAgaciKur(kosullar, g.filtreler.map((f) => f.baglac ?? 'Ve'));

  const parametreler: ReportParameter[] | null = g.parametreler.length > 0
    ? g.parametreler.map((p): ReportParameter => ({
        anahtar: p.anahtar,
        etiket: p.etiket,
        tip: p.tip,
        zorunlu: p.zorunlu,
        varsayilan: p.tip === 'TarihAralik' ? null : (p.varsayilan.trim() || null),
        secenekler: p.tip === 'Liste'
          ? p.secenekler.split(',').map((s) => s.trim()).filter(Boolean).map((s) => ({ deger: s, etiket: s }))
          : null,
      }))
    : null;

  // Aynı adlı kolonlar iki tablodan seçilince sonuç başlıkları karışmasın: takma ad
  // verilmemiş ÇAKIŞAN kolonlara otomatik "Tablo Kolon" takma adı üret.
  const cikisSayaci = new Map<string, number>();
  for (const a of g.alanlar) {
    if (a.takmaAd.trim()) continue;
    const k = `${a.toplama}|${a.kolon}`;
    cikisSayaci.set(k, (cikisSayaci.get(k) ?? 0) + 1);
  }
  // M7: otomatik takma ad BOŞLUKSUZ üretilir (kullanıcı: "AS'lerde boşluk olmasın" —
  // "Odemeler Tutar" → "Odemeler_Tutar"). Kullanıcının ELLE verdiği takma ada dokunulmaz.
  const adSadelestir = (s: string) => s.replace(/[()]/g, '').trim().replace(/\s+/g, '_');
  const otoTakma = (a: EditField) =>
    !a.takmaAd.trim() && (cikisSayaci.get(`${a.toplama}|${a.kolon}`) ?? 0) > 1
      ? adSadelestir(`${g.tableName(a.tabloId)}_${a.etiket}${a.toplama !== 'Yok' ? `_${aggLabel[a.toplama]}` : ''}`)
      : (a.takmaAd.trim() || null);

  return {
    model: {
      haritaId: g.haritaId,
      // Y4: kova (tarih kırılımı) alanla birlikte gider; backend SELECT ve GROUP BY'da aynı
      // indirgenmiş ifadeyi kullanır.
      alanlar: g.alanlar.map((a) => ({
        tabloId: a.tabloId, kolon: a.kolon, toplama: a.toplama, takmaAd: otoTakma(a), kova: a.kova ?? null,
      })),
      filtre: filtreAgacı,
      // Y1: hesaplanan (formül) kolonlar — ifade backend'de doğrulanır ({Tablo.Kolon} çözülür).
      hesaplananAlanlar: g.hesaplananlar.some((h) => h.ifade.trim())
        ? g.hesaplananlar
            .filter((h) => h.ifade.trim())
            .map((h) => ({ ifade: h.ifade.trim(), takmaAd: h.takmaAd.trim() || null }))
        : null,
      // S22: metrik referanslari — formul kosum aninda katalogdan cozulur (tek dogruluk kaynagi).
      metrikAlanlar: g.metrikler.length > 0 ? g.metrikler.map((m) => ({ metrikId: m.metrikId, takmaAd: null })) : null,
      // Y3: özet koşulları (HAVING) — filtreden AYRI liste; değerler filtre satırlarıyla aynı
      // biçimde çözülür (parametre bağlanmaz: HAVING değeri sabit sayıdır).
      ozetKosullar: g.ozetKosullar.length > 0
        ? g.ozetKosullar.map((k) => ({
            toplama: k.toplama, tabloId: k.tabloId, kolon: k.kolon, op: k.op,
            degerler: conditionValues(k), baglac: k.baglac ?? 'Ve',
          }))
        : null,
      // Y6: pencere kolonları — boş ifadeler atılır; sayı alanı boşsa backend varsayılanı kullanır.
      pencereKolonlari: g.pencereler.length > 0
        ? g.pencereler.map((p) => ({
            islev: p.islev,
            deger: p.deger.trim() || null,
            bolum: p.bolum.map((b) => b.trim()).filter(Boolean),
            sira: p.sira.filter((s) => s.ifade.trim()).map((s) => ({ ifade: s.ifade.trim(), azalan: s.azalan })),
            sayi: p.sayi.trim() ? Number(p.sayi) : null,
            takmaAd: p.takmaAd.trim() || null,
          }))
        : null,
      // Y9: çapraz tablo — açık, kolonlar seçili ve en az bir başlık girilmişse gönderilir.
      capraz: g.capraz.etkin && g.capraz.baslikKolonu && g.capraz.degerKolonu && g.capraz.basliklar.trim()
        ? {
            baslikKolonu: g.capraz.baslikKolonu, degerKolonu: g.capraz.degerKolonu,
            basliklar: g.capraz.basliklar.split(',').map((x) => x.trim()).filter(Boolean),
            toplama: g.capraz.toplama,
          }
        : null,
      // Y10: ara toplam satırları.
      araToplam: g.araToplam.etkin
        ? { tur: g.araToplam.tur, etiket: g.araToplam.etiket.trim() || '(Tümü)', etiketle: g.araToplam.etiketle }
        : null,
      // Y11: kaydı var/yok koşulları — iç filtre satırları VE ile birleşir (özet filtresiyle aynı desen).
      varlikKosullari: g.varliklar.filter((v) => v.tabloId).length > 0
        ? g.varliklar.filter((v) => v.tabloId).map((v) => ({
            tabloId: v.tabloId, yok: v.yok, baglac: v.baglac ?? 'Ve',
            filtre: v.filtreler.filter((f) => f.kolon).length > 0
              ? {
                  tur: 'grup' as const, baglac: 'Ve' as const,
                  cocuklar: v.filtreler.filter((f) => f.kolon).map((f): FilterCondition => ({
                    tur: 'kosul', tabloId: v.tabloId, kolon: f.kolon, op: f.op, degerler: conditionValues(f),
                  })),
                }
              : null,
          }))
        : null,
      // Y8: ara küme — yalnız açıkken ve en az bir kırılım/özet varken gönderilir.
      araKumeGruplari: g.araKume.etkin && g.araKume.gruplar.some((x) => x.trim())
        ? g.araKume.gruplar.map((x) => x.trim()).filter(Boolean) : null,
      araKumeOzeti: g.araKume.etkin && g.araKume.ozetler.some((o) => o.cikisAdi.trim())
        ? g.araKume.ozetler.filter((o) => o.cikisAdi.trim())
            .map((o) => ({ toplama: o.toplama, cikisAdi: o.cikisAdi.trim(), takmaAd: o.takmaAd.trim() || null }))
        : null,
      // Y7: grup içi ilk N — yalnız açıkken ve sıra verilmişken gönderilir.
      grupIciIlkN: g.ilkN.etkin && g.ilkN.sira.some((s) => s.ifade.trim())
        ? {
            adet: g.ilkN.adet.trim() ? Number(g.ilkN.adet) : 3,
            bolum: g.ilkN.bolum.map((b) => b.trim()).filter(Boolean),
            sira: g.ilkN.sira.filter((s) => s.ifade.trim()).map((s) => ({ ifade: s.ifade.trim(), azalan: s.azalan })),
            esitleriDeAl: g.ilkN.esitleriDeAl,
          }
        : null,
      // Y2: formül koşulları — değerler filtre satırlarıyla aynı biçimde çözülür.
      ifadeKosullari: g.ifadeKosullari.some((k) => k.ifade.trim())
        ? g.ifadeKosullari
            .filter((k) => k.ifade.trim())
            .map((k) => ({
              ifade: k.ifade.trim(), op: k.op,
              degerler: conditionValues(k, g.parametreler), baglac: k.baglac ?? 'Ve',
            }))
        : null,
      // Y2: sıra ya bir ÇIKIŞ kolonunu (cikisAdi) ya da seçili bir alanı işaret eder. Çıkış adı
      // dolu olduğunda tablo/kolon backend'de yok sayılır — boş Guid gönderilir.
      siralama: g.siralar.flatMap((s): SortItem[] => {
        if (s.cikisAdi?.trim())
          return [{
            tabloId: '00000000-0000-0000-0000-000000000000', kolon: '', toplama: 'Yok',
            azalan: s.azalan, cikisAdi: s.cikisAdi.trim(),
          }];
        const a = g.alanlar.find((x) => x.anahtar === s.alanAnahtar);
        // Y4: kova da eşleşmeye girer — aynı kolonun yıl/ay kırılımları AYRI çıkış kolonlarıdır.
        return a ? [{ tabloId: a.tabloId, kolon: a.kolon, toplama: a.toplama, azalan: s.azalan, kova: a.kova ?? null }] : [];
      }),
      satirLimiti: g.limit.trim() ? Number(g.limit) : null,
      // Y12: yinelenen satırları ele. Y16: sayfalama — limit "sayfa boyu"na dönüşür.
      tekille: g.tekille,
      // Y14: tablo örnekleri — backend haritayı aynı şekilde genişletir.
      tabloOrnekleri: g.tabloOrnekleri.length > 0 ? g.tabloOrnekleri : null,
      // Y15: birleşim — dalların kendi sıralaması/limiti temizlenir (birleşimde bütüne uygulanır).
      birlesim: g.birlesimModelleri.length > 0
        ? g.birlesimModelleri.map((m) => ({ ...m, siralama: [], satirLimiti: null, atla: null }))
        : null,
      birlesimTekille: g.birlesimTekille,
      atla: g.atla.trim() ? Number(g.atla) : null,
      // Yalnız kullanıcının ELLE değiştirdiği join kolonları gönderilir (diğerlerini backend
      // haritanın ilk ilişkisinden otomatik türetir). Kaydedilen raporda da bu override saklanır.
      joinler: Object.values(g.joinSecim),
      // Elle GROUP BY (kullanıcı düzenlediyse); yoksa null → backend otomatik türetir.
      gruplamalar: g.gruplamalar,
      // İlişkili özet alanlar: filtre satırları VE ile bağlanır; değer eşleme ana filtrelerle aynı.
      ozetAlanlar: g.ozetler.length > 0 ? g.ozetler.map((o) => ({
        tabloId: o.tabloId, kolon: o.kolon, toplama: o.toplama, bagTabloId: o.bagTabloId,
        takmaAd: o.takmaAd.trim() || null,
        filtre: o.filtreler.length > 0 ? {
          tur: 'grup' as const, baglac: 'Ve' as const,
          cocuklar: o.filtreler.map((f): FilterCondition => ({
            tur: 'kosul', tabloId: o.tabloId, kolon: f.kolon, op: f.op, degerler: conditionValues(f),
          })),
        } : null,
      })) : null,
      // Birleşik kolonlar: parçalar sırayla; sabit metinler backend'de parametre olur.
      bilesikAlanlar: g.bilesikler.length > 0 ? g.bilesikler.map((b) => ({
        takmaAd: b.takmaAd.trim() || null,
        parcalar: b.parcalar.map((p) => p.tur === 'sabit'
          ? { sabit: p.sabit }
          : { tabloId: p.tabloId, kolon: p.kolon }),
      })) : null,
      // Elle SQL koşulu: {Tablo.Kolon} yer tutucuları backend'de alias'a çevrilir.
      elleKosul: g.elleKosul.trim() || null,
      elleKosulBaglaci: g.elleBaglac,
      // Koşullu ölçüler (CASE WHEN): koşul satırları VE ile bağlanır; değer eşleme filtrelerle aynı.
      kosulluOlculer: g.kosullular.length > 0 ? g.kosullular.map((ko) => ({
        toplama: ko.toplama,
        tabloId: ko.kolon ? ko.tabloId : null,
        kolon: ko.kolon || null,
        takmaAd: ko.takmaAd.trim() || null,
        filtre: {
          tur: 'grup' as const, baglac: 'Ve' as const,
          cocuklar: ko.kosullar.map((f): FilterCondition => ({
            tur: 'kosul', tabloId: f.tabloId, kolon: f.kolon, op: f.op, degerler: conditionValues(f),
          })),
        },
      })) : null,
      // M29: satır-düzeyi CASE kolonları — dal koşulları VE ile bağlanır; sonuç sabit ya da kolon.
      caseAlanlar: g.caseAlanlar.length > 0 ? g.caseAlanlar.map((c) => ({
        takmaAd: c.takmaAd.trim() || null,
        dallar: c.dallar.map((d) => ({
          filtre: {
            tur: 'grup' as const, baglac: 'Ve' as const,
            cocuklar: d.kosullar.map((f): FilterCondition => ({
              tur: 'kosul', tabloId: f.tabloId, kolon: f.kolon, op: f.op, degerler: conditionValues(f),
            })),
          },
          ...(d.sonucTur === 'sabit' ? { sabit: d.sabit } : { tabloId: d.tabloId, kolon: d.kolon }),
        })),
        ...(c.varsayilanTur === 'sabit' ? { varsayilanSabit: c.varsayilanSabit }
          : c.varsayilanTur === 'kolon' ? { varsayilanTabloId: c.varsayilanTabloId, varsayilanKolon: c.varsayilanKolon }
          : {}),
      })) : null,
      // Çalıştırma-anı parametreleri (filtreler { $param } ile bunlara bağlanır).
      parametreler,
      // M17: alansız tablolar — backend bunları da join planına katar.
      ekTablolar: g.sadeceJoinTablolar.length > 0 ? g.sadeceJoinTablolar : null,
      // M18: kullanıcı sırası — backend otomatik ebeveyn seçiminde aynı önceliği kullanır.
      tabloSirasi: g.tabloSirasi.length > 0 ? g.tabloSirasi : null,
      // Join Yolları: kök + tablo başına ayar (yalnız kullanıcının özelleştirdikleri gönderilir).
      kokTabloId: g.kokTabloId || null,
      joinAyarlari: Object.keys(g.joinAyarlari).length > 0
        ? Object.entries(g.joinAyarlari).map(([tabloId, a]): JoinSetting => ({
            tabloId, ebeveynTabloId: a.ebeveynTabloId, tur: a.tur,
            kosullar: a.kosullar.length > 0
              // M5: koşul-başına tablo (undefined/ana-ebeveyn → null: backend ana ebeveyni kullanır).
              ? a.kosullar.map((k) => ({
                  tabloKolon: k.tabloKolon, ebeveynKolon: k.ebeveynKolon,
                  ebeveynTabloId: k.ebeveynTabloId && k.ebeveynTabloId !== a.ebeveynTabloId ? k.ebeveynTabloId : null,
                }))
              : null,
            // Y13: ON'a eklenen sabit koşullar — LEFT join'de ana satırı elemeden eşleşmeyi daraltır.
            ekKosullar: (a.ekKosullar ?? []).filter((f) => f.kolon).length > 0
              ? (a.ekKosullar ?? []).filter((f) => f.kolon).map((f): FilterCondition => ({
                  tur: 'kosul', tabloId: f.tabloId, kolon: f.kolon, op: f.op, degerler: conditionValues(f),
                }))
              : null,
          }))
        : null,
    },
  };
}

/** M18/M35 — Join Yolları'nda satırı bir sıra yukarı/aşağı taşır ve yeni ÖNCELİK listesini döner.
 * Taşıma GÖRÜNEN liste (FROM kökü hariç) üzerinde yapılır: kök satır olarak listelenmediği için
 * sıralamaya da katılmamalıdır — eski hâli tam liste üzerinde çalışıyor, komşu kök olduğunda
 * takası sessizce iptal ediyordu (buton basılıyor, satır kıpırdamıyordu). Kök, önceliğin BAŞINA
 * yazılır: FROM tabanı zaten ilk işlenendir. Taşınamıyorsa liste değişmeden döner. */
export function joinSirasiDegistir(
  kullanilan: string[],
  etkinKok: string,
  tabloId: string,
  yon: -1 | 1,
): string[] {
  const gorunen = kullanilan.filter((t) => t !== etkinKok);
  const i = gorunen.indexOf(tabloId);
  const j = i + yon;
  if (i < 0 || j < 0 || j >= gorunen.length) return kullanilan;
  [gorunen[i], gorunen[j]] = [gorunen[j], gorunen[i]];
  return etkinKok ? [etkinKok, ...gorunen] : gorunen;
}

/** M36 — filtre satırlarından WHERE ağacı. Bağlaç, satırın BİR ÖNCEKİ satırla ilişkisidir
 * (ilk satırınki yok sayılır) ve soldan sağa uygulanır: A VE B VEYA C → ((A VE B) VEYA C).
 * Hepsi VE ise TEK düz grup üretilir — eski davranış (ve üretilen SQL) birebir korunur. */
export function filtreAgaciKur(
  kosullar: FilterCondition[],
  baglaclar: LogicConnector[],
): FilterNode | null {
  if (kosullar.length === 0) return null;
  const veya = baglaclar.slice(1, kosullar.length).some((b) => b === 'Veya');
  if (!veya) return { tur: 'grup', baglac: 'Ve', cocuklar: kosullar };

  let agac: FilterNode = kosullar[0];
  for (let i = 1; i < kosullar.length; i++)
    agac = { tur: 'grup', baglac: baglaclar[i] ?? 'Ve', cocuklar: [agac, kosullar[i]] };
  return agac;
}

/** M36 — ağacı satır listesine geri açar (rapor/oturum yüklerken). Soldan-sağa kurulmuş ikili
 * ağaçta bağlaç sağdaki satıra yazılır; eski düz gruplar (tek bağlaçlı N çocuk) da desteklenir. */
export function filtreDuzlestir(
  dugum: FilterNode | null,
): { kosul: FilterCondition; baglac: LogicConnector }[] {
  if (!dugum) return [];
  if (dugum.tur === 'kosul') return [{ kosul: dugum, baglac: 'Ve' }];

  const cocuklar = dugum.cocuklar ?? [];
  // İkili sol-yuvalı biçim: [ağaç, koşul] — sağdaki satır bu grubun bağlacıyla bağlanır.
  if (cocuklar.length === 2 && cocuklar[0].tur === 'grup' && cocuklar[1].tur === 'kosul')
    return [...filtreDuzlestir(cocuklar[0]), { kosul: cocuklar[1], baglac: dugum.baglac }];

  return cocuklar.flatMap((c, i) =>
    filtreDuzlestir(c).map((x, j) => ({ kosul: x.kosul, baglac: i > 0 && j === 0 ? dugum.baglac : x.baglac })));
}
