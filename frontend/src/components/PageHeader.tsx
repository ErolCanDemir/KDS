// oxlint-disable react/only-export-components -- şerit stil sabitleri bileşenle birlikte yaşar
import type { ReactNode } from 'react';
import Box from '@mui/material/Box';

/** S25 (21 Ağu mockup yapısı) — sayfa başlığı ARTIK GLOBAL bantta (AppShell/UstBant): ekran adı,
 * açıklaması ve ikonu rota→menü eşlemesinden gelir. Bu bileşen geriye dönük aynı props'larla
 * yaşar ama yalnız SAYFA AKSİYON ŞERİDİ çizer: solda `sol` (geri oku vb.), sağda rozet sayaçları
 * ve `children` (butonlar/seçiciler). `ikon/baslik/aciklama` bilerek çizilmez — çifte başlık
 * olmasın diye kabul edilip yok sayılır; 19 sayfanın çağrısı değişmeden çalışır. */
export function PageHeader({ rozetler, sol, children }: {
  ikon?: ReactNode;
  baslik?: ReactNode;
  aciklama?: ReactNode;
  rozetler?: { sayi: number | string; ad: string }[];
  /** Şeridin başına küçük öğe — ör. geri oku. */
  sol?: ReactNode;
  /** Sağ taraf aksiyonları (butonlar, seçiciler). */
  children?: ReactNode;
}) {
  const bos = !sol && !children && (!rozetler || rozetler.length === 0);
  if (bos) return null;   // yalnız başlık taşıyan çağrılar hiç şerit çizmez (başlık artık bantta)

  return (
    <Box sx={{ mb: 1.5, display: 'flex', alignItems: 'center', gap: 1.25, flexWrap: 'wrap' }}>
      {sol}
      <Box sx={{ flexGrow: 1 }} />
      {rozetler && rozetler.length > 0 && (
        <Box sx={{ display: 'flex', gap: 0.75, flexWrap: 'wrap' }}>
          {rozetler.map((r) => (
            <Box key={r.ad} sx={{
              px: 1.25, py: 0.45, borderRadius: 999, fontSize: 12, fontWeight: 700, whiteSpace: 'nowrap',
              bgcolor: 'action.hover', border: '1px solid', borderColor: 'divider', color: 'text.primary',
            }}>
              {typeof r.sayi === 'number' ? r.sayi.toLocaleString('tr-TR') : r.sayi}{' '}
              <Box component="span" sx={{ fontWeight: 400, color: 'text.secondary' }}>{r.ad}</Box>
            </Box>
          ))}
        </Box>
      )}
      {children}
    </Box>
  );
}

/** Aksiyon şeridindeki Select/TextField kutu stili (eski adıyla; artık zemin-nötr). */
export const whiteBox = {
  bgcolor: 'background.paper', borderRadius: '8px',
} as const;

/** Şeritte çerçeveli (ikincil) buton stili — eski "bant üstü beyaz çerçeve"nin zemin-nötr eşi. */
export const bandButton = {
  fontWeight: 700,
} as const;

/** Şeritte birincil aksiyon stili — temanın containedPrimary gradyanına bırakılır. */
export const bandButtonWhite = {
  fontWeight: 800,
} as const;
