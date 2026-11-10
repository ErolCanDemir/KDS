/* S47 — metrik formül düzenleyicisinin SAF metin mantığı: şablon yerleştirme ve alan ekleme.
 * UI'dan ayrı tutulur ki imleç/yer-tutucu kuralları testle kilitlenebilsin (sqlParams deseni).
 *
 * YER TUTUCU: '…' (U+2026). Şablonlar boşlukları bununla işaretler; alan tıklaması önce mevcut
 * SEÇİMİ, seçim yoksa İLK yer tutucuyu, o da yoksa imleç konumunu hedefler. Ekleme sonrası sıradaki
 * yer tutucu varsa SEÇİLİ bırakılır — kullanıcı "çip → alan → alan" diye tıklayarak formülü
 * klavyesiz kurabilir. */

export const YER_TUTUCU = '…';

export type EklemeSonucu = { metin: string; secimBas: number; secimSon: number };

/** Hazır şablonlar — formül alanının üstündeki çipler. Yer tutucular alan tıklamasıyla dolar. */
export const SABLONLAR: { ad: string; sablon: string }[] = [
  { ad: 'Toplam', sablon: 'SUM(…)' },
  { ad: 'Ortalama', sablon: 'AVG(…)' },
  { ad: 'Sayım', sablon: 'COUNT(…)' },
  { ad: 'Oran %', sablon: 'SUM(…) * 100.0 / SUM(…)' },
  { ad: 'Koşullu toplam', sablon: "SUM(CASE WHEN … = '…' THEN … ELSE 0 END)" },
];

/** Şablonu seçime/imlece yerleştirir; yerleşen şablonun İLK yer tutucusunu seçili bırakır. */
export function sablonYerlestir(metin: string, secimBas: number, secimSon: number, sablon: string): EklemeSonucu {
  const yeni = metin.slice(0, secimBas) + sablon + metin.slice(secimSon);
  const ilk = yeni.indexOf(YER_TUTUCU, secimBas);
  return ilk >= 0 && ilk < secimBas + sablon.length
    ? { metin: yeni, secimBas: ilk, secimSon: ilk + 1 }
    : { metin: yeni, secimBas: secimBas + sablon.length, secimSon: secimBas + sablon.length };
}

/** Alan (ya da fonksiyon örneği) ekler: önce SEÇİM, seçim yoksa ilk yer tutucu, o da yoksa imleç.
 * Eklemeden sonra sıradaki yer tutucu varsa onu seçer (zincirleme doldurma). */
export function alanEkle(metin: string, secimBas: number, secimSon: number, eklenecek: string): EklemeSonucu {
  let bas = secimBas;
  let son = secimSon;
  if (bas === son) {
    const yerTutucu = metin.indexOf(YER_TUTUCU);
    if (yerTutucu >= 0) {
      bas = yerTutucu;
      son = yerTutucu + 1;
    }
  }

  const yeni = metin.slice(0, bas) + eklenecek + metin.slice(son);
  const ekSonu = bas + eklenecek.length;
  const siradaki = yeni.indexOf(YER_TUTUCU, ekSonu);
  return siradaki >= 0
    ? { metin: yeni, secimBas: siradaki, secimSon: siradaki + 1 }
    : { metin: yeni, secimBas: ekSonu, secimSon: ekSonu };
}
