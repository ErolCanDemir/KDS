// Zamanlama (cron) ↔ anlaşılır seçim çevirileri (test turu M22/M23) — SAF; vitest edilir.
// Desteklenen biçimler UI'nın ürettikleridir; dışarıdan gelen başka her cron "Ozel" sayılır.

export type CronMod = 'Elle' | 'Saatlik' | 'Gunluk' | 'Haftalik' | 'Ozel';
export type CronSecim = { mod: CronMod; dakika: number; saat: number; gun: number; ozel: string };

export const GUN_ADLARI = ['Pazar', 'Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma', 'Cumartesi'];

const iki = (n: number) => String(n).padStart(2, '0');

/** Cron metnini UI seçimine çözer ('' = Elle; tanınmayan desen = Ozel). */
export function cronCoz(cron: string | null | undefined): CronSecim {
  const c = (cron ?? '').trim();
  const varsayilan: CronSecim = { mod: 'Elle', dakika: 0, saat: 3, gun: 1, ozel: c };
  if (c === '') return varsayilan;
  let m = /^(\d{1,2}) \* \* \* \*$/.exec(c);
  if (m) return { ...varsayilan, mod: 'Saatlik', dakika: +m[1] };
  m = /^(\d{1,2}) (\d{1,2}) \* \* \*$/.exec(c);
  if (m) return { ...varsayilan, mod: 'Gunluk', dakika: +m[1], saat: +m[2] };
  m = /^(\d{1,2}) (\d{1,2}) \* \* ([0-6])$/.exec(c);
  if (m) return { ...varsayilan, mod: 'Haftalik', dakika: +m[1], saat: +m[2], gun: +m[3] };
  return { ...varsayilan, mod: 'Ozel' };
}

/** UI seçiminden cron metni üretir ('' = Elle). */
export function cronKur(s: CronSecim): string {
  switch (s.mod) {
    case 'Elle': return '';
    case 'Saatlik': return `${s.dakika} * * * *`;
    case 'Gunluk': return `${s.dakika} ${s.saat} * * *`;
    case 'Haftalik': return `${s.dakika} ${s.saat} * * ${s.gun}`;
    case 'Ozel': return s.ozel.trim();
  }
}

/** İnsan-okur Türkçe özet — "bu iş ne zaman çalışacak?" sorusunun net cevabı. */
export function cronAciklama(cron: string | null | undefined): string {
  const s = cronCoz(cron);
  switch (s.mod) {
    case 'Elle': return 'Zamanlayıcı çalıştırmaz — yalnız "Şimdi" butonuyla elle çalışır.';
    case 'Saatlik': return `Her saatin ${s.dakika}. dakikasında çalışır (günde 24 kez).`;
    case 'Gunluk': return `Her gün saat ${iki(s.saat)}:${iki(s.dakika)}'de çalışır.`;
    case 'Haftalik': return `Her ${GUN_ADLARI[s.gun]} saat ${iki(s.saat)}:${iki(s.dakika)}'de çalışır.`;
    case 'Ozel': return 'Özel cron — alanlar soldan sağa: dakika · saat · ayın günü · ay · haftanın günü (* = her).';
  }
}
