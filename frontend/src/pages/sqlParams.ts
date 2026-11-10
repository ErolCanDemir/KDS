// Ham SQL editörü parametre desteği (test turu M11) — SAF yardımcı (Monaco'suz; vitest edilir).

/** SQL metnindeki @parametre adlarını bulur: string literal'ler ('...' — '' kaçışı dahil),
 * köşeli tanımlayıcılar [..], satır (--) ve blok (/* *\/) yorumları DIŞINDA kalan @ad geçişleri.
 * @@sistem değişkenleri hariçtir; sıra korunur, tekrarlar tekilleştirilir. */
export function sqlParametreleriBul(sql: string): string[] {
  const temiz = sql
    .replace(/'(?:[^']|'')*'/g, "''")   // string literal (içindeki @ veri sayılır)
    .replace(/\[[^\]]*\]/g, '[]')       // köşeli tanımlayıcı
    .replace(/--[^\n]*/g, '')           // satır yorumu
    .replace(/\/\*[\s\S]*?\*\//g, '');  // blok yorum
  const bulunan: string[] = [];
  for (const m of temiz.matchAll(/(^|[^@\w])@([A-Za-z_ÇĞİÖŞÜçğıöşü][\wÇĞİÖŞÜçğıöşü]*)/g)) {
    const ad = m[2];
    if (!bulunan.includes(ad)) bulunan.push(ad);
  }
  return bulunan;
}

export type SqlParamTip = 'Metin' | 'Sayi' | 'Tarih';
export type SqlParamGirdi = { tip: SqlParamTip; deger: string };

/** Girdileri çalıştırma sözlüğüne çevirir; eksik/bozuk girdide Türkçe hata döner.
 * Sayı → number (virgül kabul edilir), Tarih/Metin → string (tarih YYYY-AA-GG; SQL Server çevirir). */
export function sqlParametreleriHazirla(
  adlar: string[], girdiler: Record<string, SqlParamGirdi | undefined>,
): { parametreler: Record<string, unknown> } | { hata: string } {
  const parametreler: Record<string, unknown> = {};
  for (const ad of adlar) {
    const g = girdiler[ad];
    if (!g || g.deger.trim() === '') return { hata: `@${ad} parametresi için değer girin.` };
    if (g.tip === 'Sayi') {
      const n = Number(g.deger.trim().replace(',', '.'));
      if (Number.isNaN(n)) return { hata: `@${ad} sayısal bir değer olmalı.` };
      parametreler[ad] = n;
    } else {
      parametreler[ad] = g.deger.trim();
    }
  }
  return { parametreler };
}
