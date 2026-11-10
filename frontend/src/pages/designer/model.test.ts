import { describe, expect, it } from 'vitest';
import type { DataMapDetail } from '../../api/dataMaps';
import type { FilterCondition } from '../../api/queries';
import { buildTree, buildModel, joinSirasiDegistir, filtreAgaciKur, filtreDuzlestir, type ModelInputs } from './model';
import type { EditField } from './shared';

// ---- yardımcı kurulum ----

const T = { kok: 'kok', a: 'tabloA', b: 'tabloB', ara: 'tabloAra' };

const harita: DataMapDetail = {
  id: 'h1', ad: 'H', baglantiId: 'b1', mod: 'Canli',
  tablolar: [
    { id: T.kok, sema: 'dbo', hamAd: 'Kok', dostcaAd: 'Kok', kolonlar: [] },
    { id: T.a, sema: 'dbo', hamAd: 'A', dostcaAd: 'A', kolonlar: [] },
    { id: T.b, sema: 'dbo', hamAd: 'B', dostcaAd: 'B', kolonlar: [] },
    { id: T.ara, sema: 'dbo', hamAd: 'Ara', dostcaAd: 'Ara', kolonlar: [] },
  ],
  iliskiler: [
    { kaynakTabloId: T.a, kaynakKolon: 'KokId', hedefTabloId: T.kok, hedefKolon: 'Id', fktenMi: true },
    { kaynakTabloId: T.ara, kaynakKolon: 'KokId', hedefTabloId: T.kok, hedefKolon: 'Id', fktenMi: true },
    { kaynakTabloId: T.b, kaynakKolon: 'AraId', hedefTabloId: T.ara, hedefKolon: 'Id', fktenMi: true },
  ],
};

const alan = (tabloId: string, kolon: string, ek?: Partial<EditField>): EditField => ({
  anahtar: `${tabloId}.${kolon}`, tabloId, kolon, etiket: kolon, rol: 'Boyut', toplama: 'Yok', takmaAd: '', ...ek,
});

const girdiler = (ek?: Partial<ModelInputs>): ModelInputs => ({
  haritaId: 'h1',
  alanlar: [alan(T.kok, 'Id')],
  filtreler: [], siralar: [], limit: '1000',
  joinSecim: {}, gruplamalar: null, ozetler: [], bilesikler: [], kosullular: [], caseAlanlar: [], ozetKosullar: [], hesaplananlar: [], metrikler: [], ifadeKosullari: [], pencereler: [], parametreler: [],
  ilkN: { etkin: false, adet: '3', bolum: [], sira: [], esitleriDeAl: false },
  araKume: { etkin: false, gruplar: [], ozetler: [] },
  capraz: { etkin: false, baslikKolonu: '', degerKolonu: '', basliklar: '', toplama: 'Topla' },
  araToplam: { etkin: false, tur: 'GenelToplam', etiket: '(Tümü)', etiketle: true },
  varliklar: [], tekille: false, atla: '', tabloOrnekleri: [], birlesimModelleri: [], birlesimTekille: false,
  elleKosul: '', elleBaglac: 'Ve', kokTabloId: '', joinAyarlari: {}, sadeceJoinTablolar: [], tabloSirasi: [],
  kopukTablolar: [], tableName: (id) => id,
  ...ek,
});

// ---- buildTree ----

describe('buildTree', () => {
  it('otomatik ebeveyni BFS ile bulur', () => {
    const m = buildTree(harita, T.kok, [T.kok, T.a], {}, {});
    expect(m.get(T.a)?.ebeveyn).toBe(T.kok);
    expect(m.get(T.a)?.ayarli).toBe(false);
  });

  it('ayar ebeveyni otomatik-baglanan tablo olabilir (JoinPlani duzeltmesiyle ayni)', () => {
    // B'nin ebeveyni elle A secildi; A otomatik (BFS) baglanir — cozulmeli.
    const m = buildTree(harita, T.kok, [T.kok, T.a, T.b],
      { [T.b]: { ebeveynTabloId: T.a, tur: 'Left', kosullar: [] } }, {});
    expect(m.get(T.b)).toMatchObject({ ebeveyn: T.a, tur: 'Left', ayarli: true });
    expect(m.get(T.a)?.ebeveyn).toBe(T.kok);
  });

  it('ara tablo uzerinden gecen yol bulunur ve araMi isaretlenir (O4)', () => {
    // B yalniz Ara uzerinden koke baglanir; Ara kanvasta yok.
    const m = buildTree(harita, T.kok, [T.kok, T.b], {}, {});
    expect(m.get(T.b)?.ebeveyn).toBe(T.ara);
    expect(m.get(T.b)?.araMi).toBe(true);
  });
});

// ---- test turu M4/M13/M15: kullanılan-öncelikli join ağacı ----

describe('buildTree — kullanılan-öncelikli ebeveyn (M4/M13/M15)', () => {
  // Kullanıcının servis haritasının küçültülmüşü: Kisiler—KisiAdresleri—Adresler—Iller zinciri +
  // Kisiler.DogumYeriIlId→Iller KESTİRMESİ + Adresler—Ilceler—Iller alternatif ara yolu.
  const U = { kisiler: 'kisiler', kadres: 'kisiAdresleri', adres: 'adresler', iller: 'iller', ilceler: 'ilceler' };
  const h: DataMapDetail = {
    id: 'h2', ad: 'H2', baglantiId: 'b1', mod: 'Canli',
    tablolar: [
      { id: U.kisiler, sema: 'kisi', hamAd: 'Kisiler', dostcaAd: 'Kisiler', kolonlar: [] },
      { id: U.kadres, sema: 'kisi', hamAd: 'KisiAdresleri', dostcaAd: 'KisiAdresleri', kolonlar: [] },
      { id: U.adres, sema: 'adres', hamAd: 'Adresler', dostcaAd: 'Adresler', kolonlar: [] },
      { id: U.iller, sema: 'ortak', hamAd: 'Iller', dostcaAd: 'Iller', kolonlar: [] },
      { id: U.ilceler, sema: 'ortak', hamAd: 'Ilceler', dostcaAd: 'Ilceler', kolonlar: [] },
    ],
    iliskiler: [
      // BİLEREK yanıltıcı sıra: kestirme + ara yollar önce — eski kör BFS bu sırayla yanılıyordu.
      { kaynakTabloId: U.kisiler, kaynakKolon: 'DogumYeriIlId', hedefTabloId: U.iller, hedefKolon: 'Id', fktenMi: true },
      { kaynakTabloId: U.ilceler, kaynakKolon: 'IlId', hedefTabloId: U.iller, hedefKolon: 'Id', fktenMi: true },
      { kaynakTabloId: U.adres, kaynakKolon: 'IlceId', hedefTabloId: U.ilceler, hedefKolon: 'Id', fktenMi: true },
      { kaynakTabloId: U.kadres, kaynakKolon: 'KisiId', hedefTabloId: U.kisiler, hedefKolon: 'Id', fktenMi: true },
      { kaynakTabloId: U.kadres, kaynakKolon: 'AdresId', hedefTabloId: U.adres, hedefKolon: 'Id', fktenMi: true },
      { kaynakTabloId: U.adres, kaynakKolon: 'IlId', hedefTabloId: U.iller, hedefKolon: 'Id', fktenMi: true },
    ],
  };

  it('M13: kanvastaki tablo, ara tablo yerine kanvastaki komşusuna bağlanır', () => {
    // Kanvas: Kisiler → KisiAdresleri → Adresler. Eski BFS, Adresler'i ilişki sırası yüzünden
    // kanvasta OLMAYAN Iller üzerinden buluyor → kenar hiç çizilmiyordu.
    const m = buildTree(h, U.kisiler, [U.kisiler, U.kadres, U.adres], {}, {});
    expect(m.get(U.kadres)?.ebeveyn).toBe(U.kisiler);
    expect(m.get(U.adres)?.ebeveyn).toBe(U.kadres);
    expect(m.get(U.adres)?.araMi).toBe(false);
  });

  it('M15: birden çok kanvas-adayında EN DERİN ebeveyn kazanır (Iller→Adresler, kestirme değil)', () => {
    const m = buildTree(h, U.kisiler, [U.kisiler, U.kadres, U.adres, U.iller], {}, {});
    expect(m.get(U.iller)?.ebeveyn).toBe(U.adres); // Kisiler.DogumYeriIlId kestirmesi DEĞİL
    expect(m.get(U.iller)?.araMi).toBe(false);
    // M14 (görsel): etikette SOL taraf hep satırın tablosu — ilişki haritada Adresler.IlId→Iller.Id
    // (ters yön) kayıtlı olsa da satır Iller olduğundan taraflar çevrilir.
    expect(m.get(U.iller)?.etiket).toBe('Iller.Id = Adresler.IlId');
  });

  it('M4: zincirin tamamı kanvas içinden kurulur — hiçbir kenar ara tabloya düşmez', () => {
    const m = buildTree(h, U.kisiler, [U.kisiler, U.kadres, U.adres, U.ilceler, U.iller], {}, {});
    for (const tid of [U.kadres, U.adres, U.ilceler, U.iller])
      expect(m.get(tid)?.araMi, `${tid} ara tabloya bağlanmamalı`).toBe(false);
    expect(m.get(U.ilceler)?.ebeveyn).toBe(U.adres);
  });
});

// ---- test turu M5: çok-tablolu join koşulu ----

describe('M5 — koşul-başına ebeveyn tablo', () => {
  it('buildModel: farklı tabloya başvuran koşul ebeveynTabloId ile gider; ana ebeveyn null olur', () => {
    const s = buildModel(girdiler({
      kokTabloId: T.kok,
      joinAyarlari: {
        [T.b]: {
          ebeveynTabloId: T.kok, tur: 'Left', kosullar: [
            { anahtar: '1', tabloKolon: 'ServisId', ebeveynKolon: 'ServisId' },
            { anahtar: '2', tabloKolon: 'ParcaId', ebeveynKolon: 'ParcaId', ebeveynTabloId: T.a },
          ],
        },
      },
    }));
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.joinAyarlari![0].kosullar).toEqual([
      { tabloKolon: 'ServisId', ebeveynKolon: 'ServisId', ebeveynTabloId: null },
      { tabloKolon: 'ParcaId', ebeveynKolon: 'ParcaId', ebeveynTabloId: T.a },
    ]);
  });

  it('ayarlı tablonun koşul tablosu, ayarlıya değil kendi yoluna bağlanır (döngü çıkmaz)', () => {
    // Marka deseni (E2E'nin yakaladığı gerileme): Araclar ayarlı (Markalar'a, koşulu Modeller'e
    // başvuruyor); Modeller'in Markalar'a bağımsız yolu var. Ayarlı tablo erken yerleşirse Modeller
    // "en derin" diye Araclar'a bağlanıp döngü doğuruyordu.
    const V = { mk: 'markalar', md: 'modeller', ar: 'araclar' };
    const hv: DataMapDetail = {
      id: 'h3', ad: 'H3', baglantiId: 'b1', mod: 'Canli',
      tablolar: [
        { id: V.mk, sema: 'ortak', hamAd: 'Markalar', dostcaAd: 'Markalar', kolonlar: [] },
        { id: V.md, sema: 'ortak', hamAd: 'Modeller', dostcaAd: 'Modeller', kolonlar: [] },
        { id: V.ar, sema: 'arac', hamAd: 'Araclar', dostcaAd: 'Araclar', kolonlar: [] },
      ],
      iliskiler: [
        { kaynakTabloId: V.md, kaynakKolon: 'MarkaId', hedefTabloId: V.mk, hedefKolon: 'Id', fktenMi: true },
        { kaynakTabloId: V.ar, kaynakKolon: 'MarkaId', hedefTabloId: V.mk, hedefKolon: 'Id', fktenMi: true },
        { kaynakTabloId: V.ar, kaynakKolon: 'ModelId', hedefTabloId: V.md, hedefKolon: 'Id', fktenMi: true },
      ],
    };
    const m = buildTree(hv, V.mk, [V.mk, V.md, V.ar], {
      [V.ar]: {
        ebeveynTabloId: V.mk, tur: 'Left', kosullar: [
          { anahtar: '1', tabloKolon: 'MarkaId', ebeveynKolon: 'Id' },
          { anahtar: '2', tabloKolon: 'ModelId', ebeveynKolon: 'Id', ebeveynTabloId: V.md },
        ],
      },
    }, {});
    expect(m.get(V.md)?.ebeveyn).toBe(V.mk); // Araclar'a DEĞİL — döngü doğmaz
    expect(m.get(V.ar)).toMatchObject({ ebeveyn: V.mk, tur: 'Left', ayarli: true });
  });

  it('buildTree: farklı tabloya başvuran koşulun etiketinde tablo adı görünür', () => {
    const m = buildTree(harita, T.kok, [T.kok, T.a, T.b], {
      [T.b]: {
        ebeveynTabloId: T.kok, tur: 'Left', kosullar: [
          { anahtar: '1', tabloKolon: 'X', ebeveynKolon: 'Y' },
          { anahtar: '2', tabloKolon: 'P', ebeveynKolon: 'Q', ebeveynTabloId: T.a },
        ],
      },
    }, {});
    expect(m.get(T.b)?.etiket).toBe('X = Y VE P = A.Q');
  });
});

// ---- buildModel ----

describe('buildModel', () => {
  it('alan yoksa anlasilir hata doner', () => {
    const s = buildModel(girdiler({ alanlar: [] }));
    expect('hata' in s && s.hata).toContain('En az bir alan');
  });

  it('kopuk tablo varsa tablo adlariyla hata doner', () => {
    const s = buildModel(girdiler({ kopukTablolar: [T.b], tableName: () => 'B Tablosu' }));
    expect('hata' in s && s.hata).toContain('B Tablosu');
  });

  it('ayni adli cakisan kolonlara otomatik BOSLUKSUZ "Tablo_Kolon" takma adi uretir (M7)', () => {
    const s = buildModel(girdiler({
      alanlar: [alan(T.a, 'Ad'), alan(T.b, 'Ad')],
      tableName: (id) => (id === T.a ? 'A' : 'B'),
    }));
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.alanlar[0].takmaAd).toBe('A_Ad');
    expect(s.model.alanlar[1].takmaAd).toBe('B_Ad');
  });

  it('filtre deger esleme: Icinde virgulle bolunur, Arasinda iki deger, EmptyState isValueless', () => {
    const s = buildModel(girdiler({
      filtreler: [
        { anahtar: '1', tabloId: T.kok, kolon: 'X', op: 'Icinde', d1: 'a, b ,c', d2: '' },
        { anahtar: '2', tabloId: T.kok, kolon: 'Y', op: 'Arasinda', d1: '1', d2: '9' },
        { anahtar: '3', tabloId: T.kok, kolon: 'Z', op: 'EmptyState', d1: 'yok', d2: '' },
      ],
    }));
    if ('hata' in s) throw new Error(s.hata);
    const grup = s.model.filtre!;
    if (grup.tur !== 'grup') throw new Error('grup bekleniyordu');
    expect((grup.cocuklar[0] as { degerler: unknown[] }).degerler).toEqual(['a', 'b', 'c']);
    expect((grup.cocuklar[1] as { degerler: unknown[] }).degerler).toEqual(['1', '9']);
    expect((grup.cocuklar[2] as { degerler: unknown[] }).degerler).toEqual([]);
  });

  it('join ayarlarini yalniz varsa gonderir; bos kosullar null olur', () => {
    const s = buildModel(girdiler({
      kokTabloId: T.kok,
      joinAyarlari: { [T.a]: { ebeveynTabloId: T.kok, tur: 'Left', kosullar: [] } },
    }));
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.kokTabloId).toBe(T.kok);
    expect(s.model.joinAyarlari).toEqual([
      { tabloId: T.a, ebeveynTabloId: T.kok, tur: 'Left', kosullar: null, ekKosullar: null },
    ]);
  });

  it('bos listeler modelde null olarak gider (ozet/bilesik/kosullu/elle)', () => {
    const s = buildModel(girdiler());
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.ozetAlanlar).toBeNull();
    expect(s.model.bilesikAlanlar).toBeNull();
    expect(s.model.kosulluOlculer).toBeNull();
    expect(s.model.elleKosul).toBeNull();
    expect(s.model.joinAyarlari).toBeNull();
    expect(s.model.kokTabloId).toBeNull();
    expect(s.model.parametreler).toBeNull();
  });
});

describe('parametreler', () => {
  const cocuk = (s: { model: import('../../api/queries').QueryModel }) =>
    ((s.model.filtre as { cocuklar: { degerler: unknown[] }[] }).cocuklar[0]);

  it('parametreye bağlı filtre $param üretir + parametreler modele girer', () => {
    const s = buildModel(girdiler({
      filtreler: [{ anahtar: 'f1', tabloId: T.kok, kolon: 'Id', op: 'BuyukEsit', d1: '', d2: '', paramAnahtar: 'alt' }],
      parametreler: [{ anahtar: 'alt', etiket: 'Alt', tip: 'Sayi', zorunlu: true, varsayilan: '', secenekler: '' }],
    }));
    if ('hata' in s) throw new Error(s.hata);
    expect(cocuk(s).degerler).toEqual([{ $param: 'alt' }]);
    expect(s.model.parametreler).toEqual([
      { anahtar: 'alt', etiket: 'Alt', tip: 'Sayi', zorunlu: true, varsayilan: null, secenekler: null },
    ]);
  });

  it('TarihAralik parametresi Arasinda filtresini iki uca bağlar', () => {
    const s = buildModel(girdiler({
      filtreler: [{ anahtar: 'f1', tabloId: T.kok, kolon: 'Id', op: 'Arasinda', d1: '', d2: '', paramAnahtar: 'don' }],
      parametreler: [{ anahtar: 'don', etiket: 'Dönem', tip: 'TarihAralik', zorunlu: false, varsayilan: '', secenekler: '' }],
    }));
    if ('hata' in s) throw new Error(s.hata);
    expect(cocuk(s).degerler).toEqual([{ $param: 'don', parca: 'baslangic' }, { $param: 'don', parca: 'bitis' }]);
  });

  it('Liste parametresi seçenekleri deger/etiket olarak çözer', () => {
    const s = buildModel(girdiler({
      parametreler: [{ anahtar: 'sehir', etiket: 'Şehir', tip: 'Liste', zorunlu: false, varsayilan: '', secenekler: 'Ankara, İzmir' }],
    }));
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.parametreler?.[0].secenekler).toEqual([
      { deger: 'Ankara', etiket: 'Ankara' }, { deger: 'İzmir', etiket: 'İzmir' },
    ]);
  });
});

// ---- joinSirasiDegistir (M18/M35) ----
describe('joinSirasiDegistir', () => {
  // Kullanıcı senaryosu: kök Talepler, görünen satırlar [Markalar, Modeller, Odemeler].
  // Odemeler'i yukarı taşımak, tam listede komşusu KÖK olduğu için eskiden hiç çalışmıyordu.
  const kullanilan = ['markalar', 'talepler', 'odemeler', 'modeller'];

  it('kök komşu olsa bile satırı yukarı taşır (eski hâlinde takas iptal oluyordu)', () => {
    const yeni = joinSirasiDegistir(kullanilan, 'talepler', 'odemeler', -1);
    // Kök başta; görünen sıra: odemeler markalar'ın önüne geçti.
    expect(yeni).toEqual(['talepler', 'odemeler', 'markalar', 'modeller']);
  });

  it('aşağı taşır ve kökü sıraya karıştırmaz', () => {
    const yeni = joinSirasiDegistir(kullanilan, 'talepler', 'markalar', 1);
    expect(yeni).toEqual(['talepler', 'odemeler', 'markalar', 'modeller']);
    expect(yeni.indexOf('talepler')).toBe(0);
  });

  it('görünen listenin sınırlarında liste değişmez', () => {
    expect(joinSirasiDegistir(kullanilan, 'talepler', 'markalar', -1)).toEqual(kullanilan);
    expect(joinSirasiDegistir(kullanilan, 'talepler', 'modeller', 1)).toEqual(kullanilan);
  });

  it('kök yoksa (henüz seçilmemiş) yalnız görünen sırayı döndürür', () => {
    expect(joinSirasiDegistir(['a', 'b'], '', 'b', -1)).toEqual(['b', 'a']);
  });
});

// ---- filtre bağlaçları (M36) ----
describe('filtreAgaciKur / filtreDuzlestir', () => {
  const k = (kolon: string): FilterCondition => ({ tur: 'kosul', tabloId: 't', kolon, op: 'Esit', degerler: ['x'] });

  it('hepsi VE ise TEK düz grup üretir (eski SQL biçimi korunur)', () => {
    const a = filtreAgaciKur([k('a'), k('b'), k('c')], ['Ve', 'Ve', 'Ve']);
    expect(a).toEqual({ tur: 'grup', baglac: 'Ve', cocuklar: [k('a'), k('b'), k('c')] });
  });

  it('VEYA varsa soldan sağa yuvalanır: A VE B VEYA C → ((A VE B) VEYA C)', () => {
    const a = filtreAgaciKur([k('a'), k('b'), k('c')], ['Ve', 'Ve', 'Veya']);
    expect(a).toEqual({
      tur: 'grup', baglac: 'Veya',
      cocuklar: [{ tur: 'grup', baglac: 'Ve', cocuklar: [k('a'), k('b')] }, k('c')],
    });
  });

  it('düzleştirme kurmanın tersidir (bağlaçlar geri gelir)', () => {
    const baglaclar = ['Ve', 'Veya', 'Ve'] as const;
    const satirlar = filtreDuzlestir(filtreAgaciKur([k('a'), k('b'), k('c')], [...baglaclar]));
    expect(satirlar.map((s) => s.kosul.kolon)).toEqual(['a', 'b', 'c']);
    expect(satirlar.map((s) => s.baglac)).toEqual(['Ve', 'Veya', 'Ve']);
  });

  it('eski kayıtlar (tek düz VE grubu) sorunsuz açılır', () => {
    const eski = { tur: 'grup' as const, baglac: 'Ve' as const, cocuklar: [k('a'), k('b')] };
    expect(filtreDuzlestir(eski).map((s) => s.baglac)).toEqual(['Ve', 'Ve']);
  });

  it('filtre yoksa null döner', () => {
    expect(filtreAgaciKur([], [])).toBeNull();
    expect(filtreDuzlestir(null)).toEqual([]);
  });
});

// ---- Y3: özet koşulları (HAVING) ----
describe('buildModel — özet koşulu (HAVING)', () => {
  it('özet koşulları modele AYRI liste olarak gider (filtreye karışmaz)', () => {
    const s = buildModel(girdiler({
      filtreler: [{ anahtar: 'f1', tabloId: T.kok, kolon: 'Tutar', op: 'Buyuk', d1: '100', d2: '' }],
      ozetKosullar: [
        { anahtar: 'o1', toplama: 'Say', tabloId: T.a, kolon: 'Id', op: 'BuyukEsit', d1: '3', d2: '' },
        { anahtar: 'o2', toplama: 'Topla', tabloId: T.a, kolon: 'Tutar', op: 'Buyuk', d1: '1000', d2: '', baglac: 'Veya' },
      ],
    }));
    if ('hata' in s) throw new Error(s.hata);

    // WHERE tarafı yalnız satır filtresi
    expect(s.model.filtre).toEqual({
      tur: 'grup', baglac: 'Ve',
      cocuklar: [{ tur: 'kosul', tabloId: T.kok, kolon: 'Tutar', op: 'Buyuk', degerler: ['100'] }],
    });
    // HAVING tarafı ayrı liste, bağlaçlarıyla
    expect(s.model.ozetKosullar).toEqual([
      { toplama: 'Say', tabloId: T.a, kolon: 'Id', op: 'BuyukEsit', degerler: ['3'], baglac: 'Ve' },
      { toplama: 'Topla', tabloId: T.a, kolon: 'Tutar', op: 'Buyuk', degerler: ['1000'], baglac: 'Veya' },
    ]);
  });

  it('özet koşulu yoksa null gider', () => {
    const s = buildModel(girdiler());
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.ozetKosullar).toBeNull();
  });
});

// ---- Y2: formül koşulu + çıkış kolonuna göre sıralama ----
describe('buildModel — Y2 formül koşulu ve çıkış sıralaması', () => {
  it('formül koşulları ayrı liste olarak gider; boş formüller atılır', () => {
    const s = buildModel(girdiler({
      ifadeKosullari: [
        { anahtar: 'i1', ifade: ' {Kok.Tutar} * 1.18 ', op: 'Buyuk', d1: '5000', d2: '' },
        { anahtar: 'i2', ifade: '   ', op: 'Buyuk', d1: '1', d2: '' }, // boş → gitmez
        { anahtar: 'i3', ifade: '[Kar Marji]', op: 'BuyukEsit', d1: '20', d2: '', baglac: 'Veya' },
      ],
    }));
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.ifadeKosullari).toEqual([
      { ifade: '{Kok.Tutar} * 1.18', op: 'Buyuk', degerler: ['5000'], baglac: 'Ve' },
      { ifade: '[Kar Marji]', op: 'BuyukEsit', degerler: ['20'], baglac: 'Veya' },
    ]);
  });

  it('formül koşulunun değeri parametreye bağlanabilir', () => {
    const s = buildModel(girdiler({
      parametreler: [{ anahtar: 'esik', etiket: 'Eşik', tip: 'Sayi', zorunlu: false, varsayilan: '', secenekler: '' }],
      ifadeKosullari: [{ anahtar: 'i1', ifade: '{Kok.Tutar} * 2', op: 'Buyuk', d1: '', d2: '', paramAnahtar: 'esik' }],
    }));
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.ifadeKosullari![0].degerler).toEqual([{ $param: 'esik' }]);
  });

  it('formül koşulu yoksa null gider', () => {
    const s = buildModel(girdiler());
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.ifadeKosullari).toBeNull();
  });

  it('çıkış adına göre sıra tablo/kolon aramaz — cikisAdi ile gider', () => {
    const s = buildModel(girdiler({
      siralar: [
        { anahtar: 's1', alanAnahtar: '', azalan: true, cikisAdi: 'Kar Marji' },
        { anahtar: 's2', alanAnahtar: `${T.kok}.Id`, azalan: false },
      ],
    }));
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.siralama).toEqual([
      { tabloId: '00000000-0000-0000-0000-000000000000', kolon: '', toplama: 'Yok', azalan: true, cikisAdi: 'Kar Marji' },
      { tabloId: T.kok, kolon: 'Id', toplama: 'Yok', azalan: false, kova: null },
    ]);
  });

  it('Y4: tarih kırılımı alanla ve sırayla birlikte modele gider', () => {
    const s = buildModel(girdiler({
      alanlar: [alan(T.kok, 'Tarih', { kova: 'Ay' }), alan(T.kok, 'Tutar', { toplama: 'Topla' })],
      siralar: [{ anahtar: 's1', alanAnahtar: `${T.kok}.Tarih`, azalan: false }],
    }));
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.alanlar[0]).toMatchObject({ kolon: 'Tarih', kova: 'Ay' });
    expect(s.model.alanlar[1]).toMatchObject({ kolon: 'Tutar', kova: null });
    expect(s.model.siralama[0]).toMatchObject({ kolon: 'Tarih', kova: 'Ay' });
  });

  it('Y6: pencere kolonu modele gider; boş bölüm/sıra satırları temizlenir', () => {
    const s = buildModel(girdiler({
      pencereler: [{
        anahtar: 'p1', islev: 'Onceki', deger: ' [Ciro] ', bolum: ['[Marka]', '  '],
        sira: [{ anahtar: 's1', ifade: '[Ay]', azalan: false }, { anahtar: 's2', ifade: ' ', azalan: true }],
        sayi: '1', takmaAd: 'Gecen Ay',
      }],
    }));
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.pencereKolonlari).toEqual([{
      islev: 'Onceki', deger: '[Ciro]', bolum: ['[Marka]'],
      sira: [{ ifade: '[Ay]', azalan: false }], sayi: 1, takmaAd: 'Gecen Ay',
    }]);
  });

  it('Y7: grup içi ilk N yalnız açıkken ve sıra verilmişken modele gider', () => {
    const kapali = buildModel(girdiler({
      ilkN: { etkin: false, adet: '3', bolum: ['[Marka]'], sira: [{ anahtar: 's', ifade: '[Ciro]', azalan: true }], esitleriDeAl: false },
    }));
    if ('hata' in kapali) throw new Error(kapali.hata);
    expect(kapali.model.grupIciIlkN).toBeNull();

    const sirasiz = buildModel(girdiler({
      ilkN: { etkin: true, adet: '3', bolum: [], sira: [{ anahtar: 's', ifade: '  ', azalan: true }], esitleriDeAl: false },
    }));
    if ('hata' in sirasiz) throw new Error(sirasiz.hata);
    expect(sirasiz.model.grupIciIlkN).toBeNull(); // sıra yoksa gönderilmez (backend zaten reddederdi)

    const acik = buildModel(girdiler({
      ilkN: { etkin: true, adet: '5', bolum: ['[Marka]', ' '], sira: [{ anahtar: 's', ifade: '[Ciro]', azalan: true }], esitleriDeAl: true },
    }));
    if ('hata' in acik) throw new Error(acik.hata);
    expect(acik.model.grupIciIlkN).toEqual({
      adet: 5, bolum: ['[Marka]'], sira: [{ ifade: '[Ciro]', azalan: true }], esitleriDeAl: true,
    });
  });

  it('Y8: ara küme yalnız açıkken gider; boş kırılım/özet temizlenir', () => {
    const kapali = buildModel(girdiler({
      araKume: { etkin: false, gruplar: ['Sehir'], ozetler: [{ anahtar: 'o', toplama: 'Ortalama', cikisAdi: 'Ciro', takmaAd: '' }] },
    }));
    if ('hata' in kapali) throw new Error(kapali.hata);
    expect(kapali.model.araKumeGruplari).toBeNull();
    expect(kapali.model.araKumeOzeti).toBeNull();

    const acik = buildModel(girdiler({
      araKume: {
        etkin: true, gruplar: ['Sehir', '  '],
        ozetler: [{ anahtar: 'o', toplama: 'Ortalama', cikisAdi: ' Ciro ', takmaAd: 'Ortalama Ciro' },
                  { anahtar: 'o2', toplama: 'Say', cikisAdi: '', takmaAd: '' }],
      },
    }));
    if ('hata' in acik) throw new Error(acik.hata);
    expect(acik.model.araKumeGruplari).toEqual(['Sehir']);
    expect(acik.model.araKumeOzeti).toEqual([{ toplama: 'Ortalama', cikisAdi: 'Ciro', takmaAd: 'Ortalama Ciro' }]);
  });

  it('Y9/Y10/Y11: çapraz, ara toplam ve var/yok koşulları modele doğru gider', () => {
    const s = buildModel(girdiler({
      capraz: { etkin: true, baslikKolonu: 'Ay', degerKolonu: 'Ciro', basliklar: ' Ocak , Şubat ,, ', toplama: 'Topla' },
      araToplam: { etkin: true, tur: 'Kademeli', etiket: '  ', etiketle: true },
      varliklar: [
        { anahtar: 'v1', tabloId: T.a, yok: true, baglac: 'Ve',
          filtreler: [{ anahtar: 'f', kolon: 'Durum', op: 'Esit', d1: 'Iptal', d2: '' }] },
        { anahtar: 'v2', tabloId: '', yok: false, baglac: 'Ve', filtreler: [] }, // tablosuz → gitmez
      ],
    }));
    if ('hata' in s) throw new Error(s.hata);

    expect(s.model.capraz).toEqual({ baslikKolonu: 'Ay', degerKolonu: 'Ciro', basliklar: ['Ocak', 'Şubat'], toplama: 'Topla' });
    expect(s.model.araToplam).toEqual({ tur: 'Kademeli', etiket: '(Tümü)', etiketle: true }); // boş etiket → varsayılan
    expect(s.model.varlikKosullari).toEqual([{
      tabloId: T.a, yok: true, baglac: 'Ve',
      filtre: { tur: 'grup', baglac: 'Ve', cocuklar: [{ tur: 'kosul', tabloId: T.a, kolon: 'Durum', op: 'Esit', degerler: ['Iptal'] }] },
    }]);
  });

  it('Y9/Y10: kapalıyken ya da eksik seçimde modele gitmez', () => {
    const s = buildModel(girdiler({
      capraz: { etkin: true, baslikKolonu: 'Ay', degerKolonu: '', basliklar: 'Ocak', toplama: 'Topla' }, // değer kolonu yok
      araToplam: { etkin: false, tur: 'GenelToplam', etiket: '(Tümü)', etiketle: true },
    }));
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.capraz).toBeNull();
    expect(s.model.araToplam).toBeNull();
    expect(s.model.varlikKosullari).toBeNull();
  });

  it('Y12/Y16: tekilleştirme ve sayfalama modele gider', () => {
    const s = buildModel(girdiler({ tekille: true, atla: '100', limit: '50' }));
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.tekille).toBe(true);
    expect(s.model.atla).toBe(100);
    expect(s.model.satirLimiti).toBe(50);

    const bos = buildModel(girdiler({ atla: '   ' }));
    if ('hata' in bos) throw new Error(bos.hata);
    expect(bos.model.atla).toBeNull();
  });

  it('Y13: join ayarındaki sabit şartlar ekKosullar olarak gider; kolonsuz satır atılır', () => {
    const s = buildModel(girdiler({
      alanlar: [alan(T.kok, 'Id'), alan(T.a, 'x')],
      joinAyarlari: {
        [T.a]: {
          ebeveynTabloId: T.kok, tur: 'Left', kosullar: [],
          ekKosullar: [
            { anahtar: 'f1', tabloId: T.a, kolon: 'AktifMi', op: 'Esit', d1: '1', d2: '' },
            { anahtar: 'f2', tabloId: T.a, kolon: '', op: 'Esit', d1: 'x', d2: '' }, // kolonsuz → gitmez
          ],
        },
      },
    }));
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.joinAyarlari![0].ekKosullar).toEqual([
      { tur: 'kosul', tabloId: T.a, kolon: 'AktifMi', op: 'Esit', degerler: ['1'] },
    ]);
  });

  it('Y14/Y15: tablo örnekleri ve birleşim modele gider; dalların sıralaması temizlenir', () => {
    const dal = buildModel(girdiler({ alanlar: [alan(T.a, 'x')], limit: '5' }));
    if ('hata' in dal) throw new Error(dal.hata);

    const s = buildModel(girdiler({
      tabloOrnekleri: [{ ornekId: 'o1', kaynakTabloId: T.a, etiket: 'A (2)' }],
      birlesimModelleri: [{ ...dal.model, siralama: [{ tabloId: T.a, kolon: 'x', toplama: 'Yok', azalan: true }] }],
      birlesimTekille: true,
    }));
    if ('hata' in s) throw new Error(s.hata);

    expect(s.model.tabloOrnekleri).toEqual([{ ornekId: 'o1', kaynakTabloId: T.a, etiket: 'A (2)' }]);
    expect(s.model.birlesimTekille).toBe(true);
    // Dalın kendi sıralaması/limiti temizlenir: birleşimde bunlar BÜTÜNE uygulanır.
    expect(s.model.birlesim![0].siralama).toEqual([]);
    expect(s.model.birlesim![0].satirLimiti).toBeNull();
  });

  it('Y6: pencere kolonu yoksa null gider', () => {
    const s = buildModel(girdiler());
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.pencereKolonlari).toBeNull();
  });

  it('silinmiş alanı işaret eden sıra sessizce düşer (çökmez)', () => {
    const s = buildModel(girdiler({ siralar: [{ anahtar: 's1', alanAnahtar: 'olmayan', azalan: true }] }));
    if ('hata' in s) throw new Error(s.hata);
    expect(s.model.siralama).toEqual([]);
  });
});
