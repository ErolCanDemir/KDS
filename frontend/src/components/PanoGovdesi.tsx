import GridLayout, { useContainerWidth } from 'react-grid-layout';
import 'react-grid-layout/css/styles.css';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import { DashboardCard } from './DashboardCard';
import { ParametrePanel } from './ParametrePanel';
import { SUTUN, SATIR_YUK, KENAR_BOSLUK, type PanoVerisi } from './panoVerisi';
import { boxRadius } from '../theme';

/** S43 — panonun GÖVDESİ (birleşik filtre şeridi + widget ızgarası), sayfadan bağımsız. Pano
 * görüntüleyici bunu başlık şeridiyle, ana sayfa ise çıplak kullanır; ikisi de aynı ızgarayı ve
 * aynı filtre davranışını görür. Durum <see cref="usePanoVerisi"/> kancasındadır. */
export function PanoGovdesi({ v, ac, bosMesaj }: {
  v: PanoVerisi;
  /** Karta tıklanınca (yerleşim düzenlenirken çağrılmaz). */
  ac?: (raporId: string) => void;
  /** Panoda gösterilecek rapor yoksa yazılacak ikinci satır — bağlama göre değişir. */
  bosMesaj?: string;
}) {
  const { width, containerRef } = useContainerWidth();

  return (
    <>
      {/* Pano-düzeyi filtre şeridi — raporların ortak/tüm parametrelerine tek yerden değer verilir. */}
      {!v.duzenle && v.panoParametreler.length > 0 && (
        <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 1.75, mb: 1.5, display: 'flex', alignItems: 'flex-end', gap: 2, flexWrap: 'wrap' }}>
          <ParametrePanel parametreler={v.panoParametreler} degerler={v.filtreDegerleri} onChange={v.setFiltreDegerleri} />
          <Button variant="contained" size="small" startIcon={<PlayArrowIcon />} onClick={v.uygula}>Uygula</Button>
        </Paper>
      )}

      {v.panoRaporlari.length === 0 ? (
        <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 5, textAlign: 'center', color: 'text.secondary' }}>
          <Typography sx={{ fontSize: 13.5, fontWeight: 600, mb: 0.5 }}>Bu panoda görüntülenecek rapor yok.</Typography>
          {bosMesaj && <Typography sx={{ fontSize: 12.5 }} color="text.disabled">{bosMesaj}</Typography>}
        </Paper>
      ) : (
        <Box sx={{
          mx: -0.75,
          '& .react-grid-placeholder': { bgcolor: 'primary.main', borderRadius: `${boxRadius}px`, opacity: 0.18 },
          '& .react-grid-item > div': { height: '100%' },
        }}>
          <div ref={containerRef}>
            {width > 0 && (
              <GridLayout
                width={width}
                layout={v.layout}
                gridConfig={{ cols: SUTUN, rowHeight: SATIR_YUK, margin: KENAR_BOSLUK }}
                dragConfig={{ enabled: v.duzenle }}
                resizeConfig={{ enabled: v.duzenle }}
                onLayoutChange={(l) => v.setLayout(l)}
              >
                {v.panoRaporlari.map((r) => (
                  <div key={r.id}>
                    <DashboardCard rapor={r} tazeleme={v.tazeleme} parametreDegerleri={v.uygulanan}
                      ac={() => { if (!v.duzenle) ac?.(r.id); }} />
                  </div>
                ))}
              </GridLayout>
            )}
          </div>
        </Box>
      )}
    </>
  );
}
