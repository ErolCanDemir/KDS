import { describe, expect, it } from 'vitest';
import { SABLONLAR, alanEkle, sablonYerlestir } from './metrikFormul';

/* S47 — formül yardımcısının imleç/yer-tutucu kuralları: "çip → alan → alan" akışı klavyesiz
 * formül kurabilmeli; kurallar UI'dan bağımsız burada kilitli. */

describe('sablonYerlestir', () => {
  it('boş alana şablonu koyar ve İLK yer tutucuyu seçili bırakır', () => {
    const s = sablonYerlestir('', 0, 0, 'SUM(…)');
    expect(s.metin).toBe('SUM(…)');
    expect(s.metin.slice(s.secimBas, s.secimSon)).toBe('…');
  });

  it('seçili metnin yerine geçer', () => {
    const s = sablonYerlestir('ESKI', 0, 4, 'AVG(…)');
    expect(s.metin).toBe('AVG(…)');
  });

  it('yer tutucusuz şablonda imleç sona gelir', () => {
    const s = sablonYerlestir('', 0, 0, 'GETDATE()');
    expect(s.secimBas).toBe('GETDATE()'.length);
    expect(s.secimBas).toBe(s.secimSon);
  });
});

describe('alanEkle', () => {
  it('seçim yoksa İLK yer tutucuyu doldurur, sıradakini seçer (zincirleme doldurma)', () => {
    const oran = 'SUM(…) * 100.0 / SUM(…)';
    const bir = alanEkle(oran, 0, 0, '{Odemeler.Tutar}');
    expect(bir.metin).toBe('SUM({Odemeler.Tutar}) * 100.0 / SUM(…)');
    expect(bir.metin.slice(bir.secimBas, bir.secimSon)).toBe('…');

    const iki = alanEkle(bir.metin, bir.secimBas, bir.secimSon, '{Iadeler.Tutar}');
    expect(iki.metin).toBe('SUM({Odemeler.Tutar}) * 100.0 / SUM({Iadeler.Tutar})');
    expect(iki.secimBas).toBe(iki.secimSon);   // yer tutucu kalmadı → imleç
  });

  it('seçim varsa yer tutucuya bakmadan seçimi değiştirir', () => {
    const s = alanEkle('SUM(…) + X', 9, 10, '{A.B}');
    expect(s.metin).toBe('SUM(…) + {A.B}');   // '…' duruyor; X değişti
  });

  it('yer tutucu ve seçim yoksa imlece ekler', () => {
    const s = alanEkle('SUM() ', 4, 4, '{A.B}');
    expect(s.metin).toBe('SUM({A.B}) ');
  });

  it('koşullu toplam şablonu üç tıklamayla dolar', () => {
    let d = sablonYerlestir('', 0, 0, SABLONLAR.find((x) => x.ad === 'Koşullu toplam')!.sablon);
    d = alanEkle(d.metin, d.secimBas, d.secimSon, '{Talepler.Durum}');
    // ikinci yer tutucu literal içindeki '…' — kullanıcı oraya değeri elle yazar; alan tıklasa da dolar
    d = alanEkle(d.metin, d.secimBas, d.secimSon, 'Acik');
    d = alanEkle(d.metin, d.secimBas, d.secimSon, '{Talepler.Tutar}');
    expect(d.metin).toBe("SUM(CASE WHEN {Talepler.Durum} = 'Acik' THEN {Talepler.Tutar} ELSE 0 END)");
  });
});
