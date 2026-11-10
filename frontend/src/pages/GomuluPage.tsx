import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import CircularProgress from '@mui/material/CircularProgress';
import { EChart } from '../components/EChart';
import { ResultTable } from '../components/ResultTable';
import { reportChartOption } from '../components/reportVisual';
import type { QueryRunResult } from '../api/queries';
import type { VisualizationType } from '../api/reports';

type EmbedMarka = { ad: string; markaRengi: string | null; logoDataUri: string | null };
type EmbeddedResult = {
  baslik: string;
  hedefTur: string;
  gorsellestirme: string | null;
  sonuc: QueryRunResult;
  marka: EmbedMarka;
};

/** S42 — GÖMÜLÜ GÖRÜNÜM. Dış sitenin iframe'inde açılır: menü yok, oturum yok, KDS kabuğu yok.
 *
 * <b>Neden kabuk dışında:</b> gömülen şey rapordur, ürün değil. Menü/kimlik/tema anahtarları
 * müşterinin sayfasında yeri olmayan öğelerdir; ayrıca oturum zorlayan bir kabuk, oturumu
 * olmayan ziyaretçiyi Keycloak'a atardı.
 *
 * Kimlik doğrulama tamamen ANAHTARLA yapılır — sunucu tarafında imza, iptal/süre, izinli alan
 * ve RLS zinciri işler (RunEmbeddedHandler). Bu sayfa yalnız sonucu çizer. */
export function GomuluPage() {
  const [veri, setVeri] = useState<EmbeddedResult | null>(null);
  const [hata, setHata] = useState<string | null>(null);

  useEffect(() => {
    const anahtar = new URLSearchParams(window.location.search).get('anahtar') ?? '';
    if (!anahtar) { setHata('Gömme anahtarı verilmedi.'); return; }

    const api = import.meta.env.VITE_API_ADRES ?? window.location.origin;
    fetch(`${api}/api/gomulu/calistir`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ anahtar }),
    })
      .then(async (r) => {
        if (!r.ok) {
          // Sunucu tek ayrımsız mesaj döner (anahtar yok / iptal / süresi dolmuş / izinsiz kaynak);
          // burada da ayrım yapmıyoruz — ziyaretçiye teşhis vermek saldırgana yardım olurdu.
          const g = (await r.json().catch(() => null)) as { hata?: string } | null;
          throw new Error(g?.hata ?? 'Bu içerik görüntülenemiyor.');
        }
        return r.json() as Promise<EmbeddedResult>;
      })
      .then(setVeri)
      .catch((e: unknown) => setHata(e instanceof Error ? e.message : 'Bu içerik görüntülenemiyor.'));
  }, []);

  if (hata) {
    return (
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100vh', p: 3 }}>
        <Typography sx={{ fontSize: 14, textAlign: 'center' }} color="text.secondary">{hata}</Typography>
      </Box>
    );
  }

  if (!veri) {
    return (
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100vh' }}>
        <CircularProgress size={32} />
      </Box>
    );
  }

  const tur = (veri.gorsellestirme ?? 'Tablo') as VisualizationType;
  const grafik = tur === 'Tablo' ? null : reportChartOption(veri.sonuc, tur);
  const vurgu = veri.marka.markaRengi ?? undefined;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100vh', p: 1.5, gap: 1 }}>
      {/* Beyaz-etiket başlık: kuruluşun markası, KDS'in değil. */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexShrink: 0 }}>
        {veri.marka.logoDataUri && (
          <Box component="img" src={veri.marka.logoDataUri} alt={veri.marka.ad}
            sx={{ height: 24, maxWidth: 120, objectFit: 'contain' }} />
        )}
        <Typography sx={{ fontSize: 14, fontWeight: 700, color: vurgu }}>{veri.baslik}</Typography>
      </Box>

      <Box sx={{ flexGrow: 1, minHeight: 0 }}>
        {grafik
          ? <EChart secenek={grafik} />
          : <ResultTable sonuc={veri.sonuc} />}
      </Box>

      <Typography sx={{ fontSize: 10.5, flexShrink: 0 }} color="text.disabled">
        {veri.sonuc.satirlar.length} satır · {veri.marka.ad}
      </Typography>
    </Box>
  );
}
