import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Drawer from '@mui/material/Drawer';
import IconButton from '@mui/material/IconButton';
import Typography from '@mui/material/Typography';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import CloseIcon from '@mui/icons-material/Close';
import TroubleshootIcon from '@mui/icons-material/Troubleshoot';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import { boxRadius, smallRadius } from '../theme';
import { onyuzAyarlariAl } from '../api/ayarlar';
import type { QueryDiagnosis, DiagnoseFinding } from '../api/queries';
import { kadiHata } from '../kadi/kadiKanal';

/** S16/A3 — "yavaş sorgu" eşiği (ms) sunucudaki Sistem Ayarları'ndan gelir; derlemeye gömülü
 * değildir. Ayar okunana kadar ürün varsayılanı (10 sn) kullanılır. */
export function useYavasEsik(): number {
  const [esik, setEsik] = useState(10000);
  useEffect(() => {
    let iptal = false;
    void onyuzAyarlariAl().then((a) => { if (!iptal) setEsik(a.yavasEsikMs); });
    return () => { iptal = true; };
  }, []);
  return esik;
}

const siddetGorunum: Record<DiagnoseFinding['siddet'], { etiket: string; renk: string; arka: string }> = {
  yuksek: { etiket: 'Yüksek', renk: 'error.main', arka: 'var(--kds-kirmizidolgu)' },
  orta: { etiket: 'Orta', renk: '#b07515', arka: '#fdf3e3' },
  bilgi: { etiket: 'Bilgi', renk: '#0e6f7a', arka: 'var(--kds-tealdolgu)' },
};

/** M33 "Sorun ne?" — yavaş/zaman aşımı almış koşumdan sonra görünen tanı butonu + bulgu çekmecesi.
 * Sorguyu ÇALIŞTIRMAZ: backend tahmini planı (SHOWPLAN) yorumlar; plana yetki yoksa bunu söyler
 * ve genel önerilere düşer. `tanila` çağrısı sayfanın son koşum bağlamını kullanır. */
export function SorunNeButonu({ tanila }: { tanila: () => Promise<QueryDiagnosis> }) {
  const [acik, setAcik] = useState(false);
  const [yukleniyor, setYukleniyor] = useState(false);
  const [tani, setTani] = useState<QueryDiagnosis | null>(null);
  const [hata, setHata] = useState<string | null>(null);
  const [kopyalanan, setKopyalanan] = useState<number | null>(null);

  async function ac() {
    setAcik(true);
    setYukleniyor(true);
    setHata(null);
    setTani(null);
    try { setTani(await tanila()); }
    catch (e) { setHata((e as Error).message); kadiHata(); }
    finally { setYukleniyor(false); }
  }

  return (
    <>
      <Button size="small" color="warning" variant="outlined" startIcon={<TroubleshootIcon sx={{ fontSize: 16 }} />}
        onClick={() => void ac()} sx={{ fontSize: 12, whiteSpace: 'nowrap' }}>
        Sorun ne?
      </Button>
      <Drawer anchor="right" open={acik} onClose={() => setAcik(false)}
        slotProps={{ paper: { sx: { width: { xs: '94%', sm: 500 } } } }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', p: 2, borderBottom: '1px solid', borderColor: 'divider' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <TroubleshootIcon color="warning" />
            <Typography sx={{ fontWeight: 700 }} color="primary">Sorgu tanısı</Typography>
          </Box>
          <IconButton size="small" onClick={() => setAcik(false)}><CloseIcon fontSize="small" /></IconButton>
        </Box>
        <Box sx={{ p: 2, overflowY: 'auto', bgcolor: 'background.paper', display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {yukleniyor && (
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, color: 'text.secondary', py: 3 }}>
              <CircularProgress size={20} /> Sorgunun planı inceleniyor (çalıştırılmıyor)…
            </Box>
          )}
          {hata && <Alert severity="error">{hata}</Alert>}
          {tani && (
            <>
              <Alert severity={tani.bulgular.some((b) => b.siddet === 'yuksek') ? 'warning' : 'info'} sx={{ fontSize: 13 }}>
                {tani.ozet}
              </Alert>
              {tani.bulgular.map((b, i) => {
                const g = siddetGorunum[b.siddet];
                return (
                  <Box key={i} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: boxRadius, p: 1.5 }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
                      <Box component="span" sx={{ fontSize: 10.5, fontWeight: 800, color: g.renk, bgcolor: g.arka, borderRadius: 999, px: 1, py: '2px', textTransform: 'uppercase', letterSpacing: 0.4 }}>
                        {g.etiket}
                      </Box>
                      <Typography sx={{ fontSize: 13.5, fontWeight: 700 }}>{b.baslik}</Typography>
                    </Box>
                    <Typography sx={{ fontSize: 12.5 }} color="text.secondary">{b.aciklama}</Typography>
                    {b.oneri && <Typography sx={{ fontSize: 12.5, mt: 0.6 }}><b>Öneri:</b> {b.oneri}</Typography>}
                    {b.kopyalanabilirSql && (
                      <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 0.5, mt: 0.8, bgcolor: 'var(--kds-zemin2)', border: '1px solid', borderColor: 'divider', borderRadius: smallRadius, p: 1 }}>
                        <Box component="code" sx={{ fontSize: 11.5, whiteSpace: 'pre-wrap', wordBreak: 'break-all', flexGrow: 1 }}>{b.kopyalanabilirSql}</Box>
                        <IconButton size="small" title="Kopyala" sx={{ p: 0.4 }}
                          onClick={() => { void navigator.clipboard.writeText(b.kopyalanabilirSql!); setKopyalanan(i); setTimeout(() => setKopyalanan(null), 1500); }}>
                          <ContentCopyIcon sx={{ fontSize: 15, color: kopyalanan === i ? 'success.main' : undefined }} />
                        </IconButton>
                      </Box>
                    )}
                  </Box>
                );
              })}
            </>
          )}
        </Box>
      </Drawer>
    </>
  );
}
