import type { QueryRunResult } from './queries';

// Rapor sonuç önbelleği (kod incelemesi O3): rapor başına SON sonuç localStorage'ta tutulur.
// LRU en çok 10 rapor — sınırsız büyüme ve "kota dolunca sessizce önbelleksiz kalma" engellenir;
// kota hatasında eski girişler boşaltılıp bir kez yeniden denenir.

const ONEK = 'kds.rapor.sonuc.';
const LISTE = 'kds.rapor.sonuc.__lru';
const EN_COK = 10;

export type CacheEntry = { sonuc: QueryRunResult; zaman: string };

function lruOku(): string[] {
  try { return JSON.parse(localStorage.getItem(LISTE) ?? '[]') as string[]; }
  catch { return []; }
}

export function cacheRead(raporId: string): CacheEntry | null {
  try {
    const ham = localStorage.getItem(ONEK + raporId);
    return ham ? (JSON.parse(ham) as CacheEntry) : null;
  } catch { return null; }
}

export function cacheWrite(raporId: string, girdi: CacheEntry): void {
  const lru = [raporId, ...lruOku().filter((x) => x !== raporId)];
  for (const eski of lru.slice(EN_COK)) localStorage.removeItem(ONEK + eski); // taşanı tahliye et
  const kisa = lru.slice(0, EN_COK);
  try {
    localStorage.setItem(ONEK + raporId, JSON.stringify(girdi));
    localStorage.setItem(LISTE, JSON.stringify(kisa));
  } catch {
    // Kota doldu: rapor önbelleğinin tamamını boşalt, bir kez daha dene; yine sığmazsa
    // (tek sonuç 5MB'ı aşıyordur) önbelleksiz devam — rapor yine görüntülenir.
    for (const id of lruOku()) localStorage.removeItem(ONEK + id);
    try {
      localStorage.setItem(ONEK + raporId, JSON.stringify(girdi));
      localStorage.setItem(LISTE, JSON.stringify([raporId]));
    } catch { localStorage.removeItem(LISTE); }
  }
}
