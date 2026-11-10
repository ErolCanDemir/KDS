import { useEffect, useRef, useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import CircularProgress from '@mui/material/CircularProgress';
import OpenInFullIcon from '@mui/icons-material/OpenInFull';
import { EChart } from './EChart';
import { vizDisplay, reportChartOption, scoreCardValue } from './reportVisual';
import { runReport, type ReportSummary } from '../api/reports';
import type { QueryRunResult, ParametreDegerleri } from '../api/queries';
import { boxRadius } from '../theme';
import { kadiHata } from '../kadi/kadiKanal';
import { panoKartiKatil } from '../kadi/panoSahnesi';

/** Tek pano kartı: raporu çalıştırır, kayıtlı görselleştirme türüne göre grafik / sayı kartı /
 * mini tablo çizer. Kendi yükleme + hata durumunu taşır; ana ızgarayı bloklamaz. Galeri (tüm
 * raporlar) ve pano görüntüleyici (bir panonun raporları) paylaşır. <see cref="tazeleme"/> artınca
 * yeniden çalışır. */
export function DashboardCard({ rapor, tazeleme, parametreDegerleri, ac }: { rapor: ReportSummary; tazeleme: number; parametreDegerleri?: ParametreDegerleri; ac: () => void }) {
  const [sonuc, setSonuc] = useState<QueryRunResult | null>(null);
  const [hata, setHata] = useState<string | null>(null);
  const [calisiyor, setCalisiyor] = useState(true);
  const iptalRef = useRef<AbortController | null>(null);

  useEffect(() => {
    iptalRef.current?.abort();
    const iptal = new AbortController();
    iptalRef.current = iptal;
    setCalisiyor(true);
    setHata(null);
    // Kadi'de kart başına AYRI sahne açılmaz: pano genelinde tek ilerleme sahnesi (panoSahnesi)
    // — 7 kart 7 kez "çalıştırıyorum" demek yerine "Panoyu çalıştırıyorum… 3/7" der.
    const sahneBitir = panoKartiKatil();
    runReport(rapor.id, parametreDegerleri, iptal.signal)
      .then((s) => { sahneBitir(); if (!iptal.signal.aborted) { setSonuc(s); setCalisiyor(false); } })
      .catch((e) => {
        sahneBitir();
        if (iptal.signal.aborted) return;
        setHata(e instanceof Error ? e.message : 'Çalıştırılamadı.'); kadiHata();
        setCalisiyor(false);
      });
    return () => { iptal.abort(); sahneBitir(); };
  }, [rapor.id, tazeleme, parametreDegerleri]);

  const g = vizDisplay[rapor.gorsellestirme];
  const secenek = sonuc ? reportChartOption(sonuc, rapor.gorsellestirme) : null;

  return (
    <Paper variant="outlined" sx={{ borderRadius: boxRadius, overflow: 'hidden', display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* başlık şeridi */}
      <Box onClick={ac}
        sx={{ px: 1.75, py: 1.1, display: 'flex', alignItems: 'center', gap: 1, cursor: 'pointer',
          borderBottom: '1px solid #e2e9f0', '&:hover .panoAc': { opacity: 1 } }}>
        <Box sx={{ width: 26, height: 26, borderRadius: '7px', display: 'flex', alignItems: 'center',
          justifyContent: 'center', bgcolor: g.arka, color: g.renk, flexShrink: 0 }}>{g.ikon}</Box>
        <Box sx={{ minWidth: 0, flexGrow: 1 }}>
          <Typography noWrap sx={{ fontSize: 13, fontWeight: 700, color: 'text.primary' }}>{rapor.ad}</Typography>
          <Typography noWrap sx={{ fontSize: 11, color: 'text.disabled' }}>
            {g.ad}{rapor.paylasim === 'Ekip' ? ' · ekip' : ''}{sonuc ? ` · ${sonuc.satirlar.length} satır` : ''}
          </Typography>
        </Box>
        <OpenInFullIcon className="panoAc" sx={{ fontSize: 15, color: 'text.disabled', opacity: 0, transition: 'opacity .15s' }} />
      </Box>

      {/* gövde: grafik / sayı kartı / mini tablo */}
      <Box sx={{ flexGrow: 1, minHeight: 200, display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
        {calisiyor ? (
          <Box sx={{ textAlign: 'center', py: 4 }}><CircularProgress size={20} /></Box>
        ) : hata ? (
          <Box sx={{ p: 2.5, textAlign: 'center' }}>
            <Typography sx={{ fontSize: 12, color: 'error.main' }}>{hata}</Typography>
          </Box>
        ) : !sonuc || sonuc.satirlar.length === 0 ? (
          <Typography sx={{ fontSize: 12.5, color: 'text.disabled', textAlign: 'center', py: 4 }}>Sonuç boş.</Typography>
        ) : secenek ? (
          <EChart secenek={secenek} yukseklik={230} />
        ) : rapor.gorsellestirme === 'SayiKarti' ? (
          <SayiKarti sonuc={sonuc} renk={g.renk} />
        ) : (
          <MiniTablo sonuc={sonuc} />
        )}
      </Box>
    </Paper>
  );
}

function SayiKarti({ sonuc, renk }: { sonuc: QueryRunResult; renk: string }) {
  const { deger, etiket } = scoreCardValue(sonuc);
  return (
    <Box sx={{ textAlign: 'center', py: 3 }}>
      <Typography sx={{ fontSize: 40, fontWeight: 800, color: renk, lineHeight: 1.1 }}>{deger}</Typography>
      <Typography noWrap sx={{ fontSize: 12.5, color: 'text.secondary', mt: 0.5, px: 2 }}>{etiket}</Typography>
    </Box>
  );
}

/** Grafik uygun değilse (tablo raporu / sayısal ölçü yok) ilk birkaç satırı kompakt gösterir. */
function MiniTablo({ sonuc }: { sonuc: QueryRunResult }) {
  const kolonlar = sonuc.kolonlar.slice(0, 4);
  const satirlar = sonuc.satirlar.slice(0, 6);
  const bicim = (v: unknown) =>
    v === null || v === undefined ? '—'
      : typeof v === 'number' ? v.toLocaleString('tr-TR')
        : typeof v === 'boolean' ? (v ? '✓' : '✗')
          : String(v);
  return (
    <Box sx={{ overflowX: 'auto', p: 1 }}>
      <Box component="table" sx={{ width: '100%', borderCollapse: 'collapse', fontSize: 11.5 }}>
        <Box component="thead">
          <Box component="tr">
            {kolonlar.map((k) => (
              <Box component="th" key={k.ad}
                sx={{ textAlign: 'left', px: 1, py: 0.6, color: 'text.secondary', fontWeight: 700,
                  borderBottom: '1px solid #e2e9f0', whiteSpace: 'nowrap' }}>{k.ad}</Box>
            ))}
          </Box>
        </Box>
        <Box component="tbody">
          {satirlar.map((r, i) => (
            <Box component="tr" key={i} sx={{ '&:nth-of-type(even)': { bgcolor: 'var(--kds-zemin2)' } }}>
              {kolonlar.map((_, ci) => (
                <Box component="td" key={ci}
                  sx={{ px: 1, py: 0.5, color: 'text.secondary', whiteSpace: 'nowrap', maxWidth: 160,
                    overflow: 'hidden', textOverflow: 'ellipsis' }}>{bicim(r[ci])}</Box>
              ))}
            </Box>
          ))}
        </Box>
      </Box>
      {sonuc.satirlar.length > 6 && (
        <Typography sx={{ fontSize: 10.5, color: 'text.disabled', px: 1, pt: 0.6 }}>
          +{(sonuc.satirlar.length - 6).toLocaleString('tr-TR')} satır daha…
        </Typography>
      )}
    </Box>
  );
}
