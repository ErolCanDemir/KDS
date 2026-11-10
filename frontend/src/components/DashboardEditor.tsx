import { useEffect, useState } from 'react';
import { PaylasimSecici } from './PaylasimSecici';
import Dialog from '@mui/material/Dialog';
import DialogContent from '@mui/material/DialogContent';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Checkbox from '@mui/material/Checkbox';
import CircularProgress from '@mui/material/CircularProgress';
import CloseIcon from '@mui/icons-material/Close';
import { vizDisplay } from './reportVisual';
import { getReports, type ReportSummary } from '../api/reports';
import type { ReportSharing } from '../api/reports';
import { addDashboard, updateDashboard, type DashboardDetail } from '../api/dashboards';
import { boxRadius } from '../theme';
import { kadiYukleniyor, kadiHata } from '../kadi/kadiKanal';

/** Pano oluştur/düzenle diyaloğu — ad, paylaşım ve SIRALI rapor seçimi. Galeri (yeni/düzenle) ve
 * görüntüleyici (düzenle) paylaşır. Seçim sırası pano ızgara sırasıdır (tık = sona ekle / çıkar). */
export function DashboardEditor({ duzenlenen, close, kaydedildi }: {
  /** null = yeni pano; dolu = düzenlenen panonun mevcut içeriği. */
  duzenlenen: DashboardDetail | null;
  close: () => void;
  kaydedildi: () => void;
}) {
  const [ad, setAd] = useState(duzenlenen?.ad ?? '');
  const [paylasim, setPaylasim] = useState<ReportSharing>(duzenlenen?.paylasim ?? 'Ozel');
  const [paylasimRolleri, setPaylasimRolleri] = useState<string[]>(duzenlenen?.paylasimRolleri ?? []);   // S27
  const [secili, setSecili] = useState<string[]>(duzenlenen?.raporlar ?? []);
  const [raporlar, setRaporlar] = useState<ReportSummary[] | null>(null);
  const [kaydediyor, setKaydediyor] = useState(false);
  const [hata, setHata] = useState<string | null>(null);

  useEffect(() => {
    let iptal = false;
    getReports().then((r) => { if (!iptal) setRaporlar(r); }).catch(() => { if (!iptal) setRaporlar([]); });
    return () => { iptal = true; };
  }, []);

  const change = (id: string) =>
    setSecili((p) => (p.includes(id) ? p.filter((x) => x !== id) : [...p, id]));

  async function save() {
    if (!ad.trim()) { setHata('Pano adı gerekli.'); kadiHata(); return; }
    setKaydediyor(true);
    setHata(null);
    const kadiBitir = kadiYukleniyor('Panoyu kaydediyorum…');
    try {
      const govde = { ad: ad.trim(), paylasim, raporlar: secili,
        paylasimRolleri: paylasim === 'Roller' ? paylasimRolleri : null };   // S27
      if (duzenlenen) await updateDashboard(duzenlenen.id, govde);
      else await addDashboard(govde);
      kaydedildi();
    }
    catch (e) { setHata(e instanceof Error ? e.message : 'Kaydedilemedi.'); kadiHata(); setKaydediyor(false); }
    finally { kadiBitir(); }
  }

  return (
    <Dialog open onClose={close} maxWidth="sm" fullWidth slotProps={{ paper: { sx: { borderRadius: boxRadius } } }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, pt: 2 }}>
        <Typography variant="h6" color="primary">{duzenlenen ? 'Panoyu Düzenle' : 'Yeni Pano'}</Typography>
        <IconButton size="small" onClick={close}><CloseIcon fontSize="small" /></IconButton>
      </Box>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.75 }}>
        <TextField size="small" fullWidth autoFocus label="Pano adı" value={ad} onChange={(e) => setAd(e.target.value)} />

        <Box>
          <Typography sx={{ fontSize: 12.5, fontWeight: 700 }} color="text.secondary">Paylaşım</Typography>
          {/* S27: Özel · Ekip · Belirli roller */}
          <PaylasimSecici paylasim={paylasim} roller={paylasimRolleri}
            degisti={(p, r) => { setPaylasim(p); setPaylasimRolleri(r); }} />
        </Box>

        <Box>
          <Typography sx={{ fontSize: 12.5, fontWeight: 700, mb: 0.5 }} color="text.secondary">
            Raporlar {secili.length > 0 ? `(${secili.length} seçili)` : ''}
          </Typography>
          <Box sx={{ border: '1px solid', borderColor: 'divider', borderRadius: boxRadius, maxHeight: 280, overflowY: 'auto', bgcolor: 'background.paper' }}>
            {raporlar === null ? (
              <Box sx={{ textAlign: 'center', py: 3 }}><CircularProgress size={18} /></Box>
            ) : raporlar.length === 0 ? (
              <Typography sx={{ fontSize: 12.5, color: 'text.disabled', p: 2, textAlign: 'center' }}>
                Henüz rapor yok — önce Sorgu Tasarımcısı/SQL Editörü'nde rapor save.
              </Typography>
            ) : raporlar.map((r) => {
              const sira = secili.indexOf(r.id);
              const g = vizDisplay[r.gorsellestirme];
              return (
                <Box key={r.id} onClick={() => change(r.id)}
                  sx={{ display: 'flex', alignItems: 'center', gap: 0.5, px: 1, py: 0.35, cursor: 'pointer',
                    borderBottom: '1px solid #f0f4f8', '&:last-of-type': { borderBottom: 'none' }, '&:hover': { bgcolor: 'var(--kds-zemin2)' } }}>
                  <Checkbox size="small" checked={sira >= 0} sx={{ p: 0.5 }} />
                  <Box sx={{ width: 22, height: 22, borderRadius: '6px', display: 'flex', alignItems: 'center',
                    justifyContent: 'center', bgcolor: g.arka, color: g.renk, flexShrink: 0 }}>{g.ikon}</Box>
                  <Typography noWrap sx={{ fontSize: 12.5, flexGrow: 1 }}>{r.ad}</Typography>
                  {sira >= 0 && (
                    <Box sx={{ width: 20, height: 20, borderRadius: '50%', bgcolor: 'primary.main', color: '#fff',
                      fontSize: 11, fontWeight: 700, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>{sira + 1}</Box>
                  )}
                </Box>
              );
            })}
          </Box>
          <Typography sx={{ fontSize: 10.5, color: 'text.disabled', mt: 0.5 }}>
            Tıklama sırası pano ızgara sırasıdır.
          </Typography>
        </Box>

        {hata && <Typography sx={{ fontSize: 12, color: 'error.main' }}>{hata}</Typography>}

        <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1 }}>
          <Button color="inherit" onClick={close}>İptal</Button>
          <Button variant="contained" onClick={() => void save()} disabled={kaydediyor}
            startIcon={kaydediyor ? <CircularProgress size={14} color="inherit" /> : undefined}>Kaydet</Button>
        </Box>
      </DialogContent>
    </Dialog>
  );
}
