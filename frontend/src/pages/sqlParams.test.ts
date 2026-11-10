import { describe, expect, it } from 'vitest';
import { sqlParametreleriBul, sqlParametreleriHazirla } from './sqlParams';

// M11: ham SQL editörü @parametre algılama + değer hazırlama.

describe('sqlParametreleriBul', () => {
  it('parametreleri sırayla, tekilleştirerek bulur', () => {
    expect(sqlParametreleriBul(
      'SELECT * FROM t WHERE Ad = @ad AND Tutar > @tutar AND Ad <> @ad',
    )).toEqual(['ad', 'tutar']);
  });

  it('string literal, köşeli ad ve yorumlardaki @ işaretlerini saymaz', () => {
    expect(sqlParametreleriBul(
      "SELECT 'mail@ornek.com', [garip@kolon] FROM t -- @yorumda\n/* @blokta */ WHERE X = @gercek",
    )).toEqual(['gercek']);
  });

  it("@@sistem değişkenlerini ve '' kaçışlı literal'i doğru işler", () => {
    expect(sqlParametreleriBul(
      "SELECT @@ROWCOUNT, 'O''Brien @x' WHERE Y = @deger",
    )).toEqual(['deger']);
  });

  it('Türkçe karakterli parametre adlarını destekler', () => {
    expect(sqlParametreleriBul('WHERE Il = @şehir')).toEqual(['şehir']);
  });
});

describe('sqlParametreleriHazirla', () => {
  it('eksik değer için Türkçe hata döner', () => {
    const s = sqlParametreleriHazirla(['ad'], {});
    expect('hata' in s && s.hata).toContain('@ad');
  });

  it('sayı tipini number yapar (virgül kabul), bozuk sayıya hata verir', () => {
    const s1 = sqlParametreleriHazirla(['t'], { t: { tip: 'Sayi', deger: '12,5' } });
    if ('hata' in s1) throw new Error(s1.hata);
    expect(s1.parametreler.t).toBe(12.5);
    const s2 = sqlParametreleriHazirla(['t'], { t: { tip: 'Sayi', deger: 'abc' } });
    expect('hata' in s2 && s2.hata).toContain('sayısal');
  });

  it('metin/tarih tiplerini kırpılmış string bırakır', () => {
    const s = sqlParametreleriHazirla(['a', 'b'], {
      a: { tip: 'Metin', deger: ' Audi ' }, b: { tip: 'Tarih', deger: '2026-08-14' },
    });
    if ('hata' in s) throw new Error(s.hata);
    expect(s.parametreler).toEqual({ a: 'Audi', b: '2026-08-14' });
  });
});
