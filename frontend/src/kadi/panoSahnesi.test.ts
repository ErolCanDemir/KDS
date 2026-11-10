import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

/* kadiKanal taklit edilir: panoSahnesi'nin ne ZAMAN sahne açtığı ve cümleyi nasıl ilerlettiği
 * sınanır — Kadi'nin kendi davranışı değil. */
const acilanlar: string[] = [];
const soylenenler: string[] = [];
const bitirilenler: number[] = [];

vi.mock('./kadiKanal', () => ({
  kadiYukleniyor: (mesaj: string) => {
    const sira = acilanlar.length;
    acilanlar.push(mesaj);
    return () => bitirilenler.push(sira);
  },
  kadiSoyle: (mesaj: string) => { soylenenler.push(mesaj); },
}));

const { panoKartiKatil } = await import('./panoSahnesi');

describe('panoSahnesi', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    acilanlar.length = 0; soylenenler.length = 0; bitirilenler.length = 0;
  });
  afterEach(() => {
    vi.runAllTimers();
    vi.useRealTimers();
  });

  it('cok kart icin TEK sahne acar ve gercek toplami yazar', () => {
    const b = [panoKartiKatil(), panoKartiKatil(), panoKartiKatil()];
    // Açılış bir tick ertelenir: o ana kadar tüm kartlar katılmış olur.
    expect(acilanlar).toHaveLength(0);
    vi.advanceTimersByTime(1);
    expect(acilanlar).toEqual(['Panoyu çalıştırıyorum… 0/3']);
    b.forEach((f) => f());
  });

  it('kart bittikce cumle ilerler, sonuncuda sahne kapanir', () => {
    const b = [panoKartiKatil(), panoKartiKatil()];
    vi.advanceTimersByTime(1);
    b[0]();
    expect(soylenenler).toEqual(['Panoyu çalıştırıyorum… 1/2']);
    expect(bitirilenler).toHaveLength(0);
    b[1]();
    expect(bitirilenler).toEqual([0]); // sahne yalnız SON kartta kapanır
  });

  it('tek kart varken pano degil rapor cumlesi kurulur', () => {
    const b = panoKartiKatil();
    vi.advanceTimersByTime(1);
    expect(acilanlar).toEqual(['Raporu çalıştırıyorum…']);
    b();
  });

  it('hepsi tick icinde biterse sahne HIC acilmaz (onbellekten donen kartlar)', () => {
    const b = [panoKartiKatil(), panoKartiKatil()];
    b.forEach((f) => f());
    vi.advanceTimersByTime(1);
    expect(acilanlar).toHaveLength(0);
  });

  it('ayni kartin bitisi iki kez sayilmaz', () => {
    const b = [panoKartiKatil(), panoKartiKatil()];
    vi.advanceTimersByTime(1);
    b[0](); b[0](); b[0]();
    expect(bitirilenler).toHaveLength(0); // ikinci kart hâlâ koşuyor
    b[1]();
    expect(bitirilenler).toEqual([0]);
  });

  it('yeni tazeleme turu sayaci sifirdan baslatir', () => {
    const ilk = [panoKartiKatil(), panoKartiKatil()];
    vi.advanceTimersByTime(1);
    ilk.forEach((f) => f());
    vi.advanceTimersByTime(500); // sıfırlama penceresi geçer

    const ikinci = [panoKartiKatil(), panoKartiKatil(), panoKartiKatil()];
    vi.advanceTimersByTime(1);
    expect(acilanlar[1]).toBe('Panoyu çalıştırıyorum… 0/3');
    ikinci.forEach((f) => f());
  });
});
