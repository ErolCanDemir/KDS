import { useEffect, useState, type ReactNode } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import Drawer from '@mui/material/Drawer';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Pagination from '@mui/material/Pagination';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import HistoryIcon from '@mui/icons-material/History';
import SearchIcon from '@mui/icons-material/Search';
import RefreshIcon from '@mui/icons-material/Refresh';
import CloseIcon from '@mui/icons-material/Close';
import { PageHeader, bandButtonWhite } from '../components/PageHeader';
import { boxRadius } from '../theme';
import { getAudit, type AuditPage as AuditSayfa, type AuditRecord } from '../api/audit';
import { kadiHata } from '../kadi/kadiKanal';

const SAYFA_BOYUT = 50;
type Durum = 'hepsi' | 'ok' | 'hata';

function DurumCip({ basarili, hata }: { basarili: boolean; hata: string | null }) {
  return basarili
    ? <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.6, bgcolor: 'var(--kds-yesildolgu)', color: 'success.main', fontSize: 12, fontWeight: 700, borderRadius: 999, px: 1, py: '2px' }}>
        <Box component="span" sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: 'success.main' }} />Başarılı
      </Box>
    : <Box component="span" title={hata ?? ''} sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.6, bgcolor: 'var(--kds-kirmizidolgu)', color: 'error.main', fontSize: 12, fontWeight: 700, borderRadius: 999, px: 1, py: '2px' }}>
        <Box component="span" sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: 'error.main' }} />Engellendi / Hata
      </Box>;
}

export function AuditPage() {
  const [durum, setDurum] = useState<Durum>('hepsi');
  const [arama, setArama] = useState('');
  const [aramaAktif, setAramaAktif] = useState('');
  const [sayfa, setSayfa] = useState(0);
  const [veri, setVeri] = useState<AuditSayfa | null>(null);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [hata, setHata] = useState<string | null>(null);
  const [secili, setSecili] = useState<AuditRecord | null>(null);

  useEffect(() => {
    let iptal = false;
    setYukleniyor(true);
    setHata(null);
    const basarili = durum === 'hepsi' ? null : durum === 'ok';
    getAudit({ sayfa, sayfaBoyut: SAYFA_BOYUT, basarili, arama: aramaAktif })
      .then((d) => { if (!iptal) setVeri(d); })
      // kadiHata İPTAL filtresinin İÇİNDE: sayfa terk edilince Kadi üzülmemeli.
      .catch((e) => { if (!iptal) { setHata((e as Error).message); kadiHata(); } })
      .finally(() => { if (!iptal) setYukleniyor(false); });
    return () => { iptal = true; };
  }, [durum, aramaAktif, sayfa]);

  function filtreDegis(yeni: Durum) { setSayfa(0); setDurum(yeni); }
  function aramaUygula() { setSayfa(0); setAramaAktif(arama); }

  const toplam = veri?.toplam ?? 0;
  const ilk = toplam === 0 ? 0 : sayfa * SAYFA_BOYUT + 1;
  const son = Math.min((sayfa + 1) * SAYFA_BOYUT, toplam);
  const sayfaSayisi = Math.max(1, Math.ceil(toplam / SAYFA_BOYUT));

  return (
    <Box>
      <PageHeader ikon={<HistoryIcon />} baslik="Denetim"
        aciklama="Kim, hangi SQL'i, ne zaman çalıştırdı — başarılı ve engellenmiş denemeler."
        rozetler={[{ sayi: toplam, ad: 'kayıt' }]}>
        <ToggleButtonGroup size="small" exclusive value={durum} onChange={(_, v: Durum | null) => v && filtreDegis(v)}
          sx={{ bgcolor: 'rgba(255,255,255,.10)', borderRadius: '8px',
            '& .MuiToggleButton-root': { color: 'rgba(255,255,255,.85)', borderColor: 'rgba(255,255,255,.3)', px: 1.4 },
            '& .Mui-selected': { bgcolor: '#fff !important', color: '#0f2d4e !important', fontWeight: 700 } }}>
          <ToggleButton value="hepsi">Hepsi</ToggleButton>
          <ToggleButton value="ok">Başarılı</ToggleButton>
          <ToggleButton value="hata">Engellenmiş</ToggleButton>
        </ToggleButtonGroup>
        <TextField size="small" placeholder="SQL'de ara…" value={arama}
          onChange={(e) => setArama(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') aramaUygula(); }}
          sx={{ bgcolor: 'background.paper', borderRadius: '8px', width: 200, '& .MuiOutlinedInput-notchedOutline': { border: 'none' } }} />
        <Button variant="contained" startIcon={<SearchIcon />} onClick={aramaUygula} sx={bandButtonWhite}>Ara</Button>
      </PageHeader>

      {hata && (
        <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" size="small" startIcon={<RefreshIcon />} onClick={() => setSayfa((s) => s)}>Tekrar</Button>}>
          {hata}
        </Alert>
      )}

      <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: boxRadius }}>
        <Table size="small" stickyHeader>
          <TableHead>
            <TableRow>
              <TableCell>Zaman</TableCell><TableCell>Kullanıcı</TableCell><TableCell>Rapor</TableCell><TableCell>Bağlantı</TableCell>
              <TableCell>SQL</TableCell><TableCell>Durum</TableCell>
              <TableCell align="right">Satır</TableCell><TableCell align="right">Süre</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {yukleniyor ? (
              <TableRow><TableCell colSpan={8} align="center" sx={{ py: 4 }}><CircularProgress size={22} /></TableCell></TableRow>
            ) : (veri?.kayitlar.length ?? 0) === 0 ? (
              <TableRow><TableCell colSpan={8}><Typography sx={{ py: 3, textAlign: 'center' }} color="text.secondary">Kayıt yok — filtreyi değiştirin ya da bir sorgu çalıştırın.</Typography></TableCell></TableRow>
            ) : veri!.kayitlar.map((k) => (
              <TableRow key={k.id} hover sx={{ cursor: 'pointer' }} onClick={() => setSecili(k)}>
                <TableCell sx={{ whiteSpace: 'nowrap', fontVariantNumeric: 'tabular-nums' }}>{new Date(k.zaman).toLocaleString('tr-TR')}</TableCell>
                <TableCell sx={{ whiteSpace: 'nowrap' }}>{k.kullaniciAd ?? <Typography component="span" sx={{ fontSize: 12 }} color="text.disabled">—</Typography>}</TableCell>
                {/* M26: kayıtlı rapor koşumuysa adı; serbest tasarımcı/SQL koşumunda boş. */}
                <TableCell sx={{ whiteSpace: 'nowrap', maxWidth: 160, overflow: 'hidden', textOverflow: 'ellipsis' }}>{k.raporAd ?? <Typography component="span" sx={{ fontSize: 12 }} color="text.disabled">—</Typography>}</TableCell>
                <TableCell sx={{ whiteSpace: 'nowrap' }}>{k.baglantiAd ?? <Typography component="span" sx={{ fontSize: 12 }} color="text.disabled">—</Typography>}</TableCell>
                <TableCell sx={{ maxWidth: 380 }}>
                  <Typography noWrap sx={{ fontFamily: 'ui-monospace,Consolas,monospace', fontSize: 12 }} color="text.secondary">{k.sql}</Typography>
                </TableCell>
                <TableCell><DurumCip basarili={k.basarili} hata={k.hata} /></TableCell>
                <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums' }}>{k.satirSayisi ?? '—'}</TableCell>
                <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums' }}>{k.sureMs} ms</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mt: 1.5 }}>
        <Typography sx={{ fontSize: 12.5 }} color="text.secondary">
          {toplam > 0 ? `${ilk.toLocaleString('tr-TR')}–${son.toLocaleString('tr-TR')} / ${toplam.toLocaleString('tr-TR')}` : '0 kayıt'}
        </Typography>
        <Box sx={{ flexGrow: 1 }} />
        {/* M27: ok yerine sayfa numaraları */}
        <Pagination size="small" count={sayfaSayisi} page={sayfa + 1} disabled={yukleniyor}
          onChange={(_, p) => setSayfa(p - 1)} siblingCount={1} boundaryCount={1} />
      </Box>

      {/* Detay çekmecesi */}
      <Drawer anchor="right" open={secili !== null} onClose={() => setSecili(null)} slotProps={{ paper: { sx: { width: { xs: '92%', sm: 560 } } } }}>
        {secili && (
          <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', p: 2, borderBottom: '1px solid', borderColor: 'divider' }}>
              <Box>
                <Typography sx={{ fontWeight: 700 }} color="primary">Denetim kaydı</Typography>
                <Typography sx={{ fontSize: 12 }} color="text.secondary">{new Date(secili.zaman).toLocaleString('tr-TR')}</Typography>
              </Box>
              <IconButton size="small" onClick={() => setSecili(null)}><CloseIcon fontSize="small" /></IconButton>
            </Box>
            <Box sx={{ p: 2, flexGrow: 1, overflowY: 'auto', bgcolor: 'background.paper', display: 'flex', flexDirection: 'column', gap: 1.5 }}>
              <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', alignItems: 'center' }}>
                <DurumCip basarili={secili.basarili} hata={secili.hata} />
                <Typography sx={{ fontSize: 12.5 }} color="text.secondary">
                  {secili.kullaniciAd ?? 'kullanıcı yok'} · {secili.baglantiAd ?? 'bağlantı yok'} · {secili.sureMs} ms{secili.satirSayisi != null ? ` · ${secili.satirSayisi} satır` : ''}
                </Typography>
              </Box>
              <Alan baslik="SQL"><Kod>{secili.sql}</Kod></Alan>
              {secili.parametreler && <Alan baslik="Parametreler"><Kod>{secili.parametreler}</Kod></Alan>}
              {secili.hata && <Alan baslik="Hata"><Kod hata>{secili.hata}</Kod></Alan>}
            </Box>
          </Box>
        )}
      </Drawer>
    </Box>
  );
}

function Alan({ baslik, children }: { baslik: string; children: ReactNode }) {
  return (
    <Box>
      <Typography sx={{ fontSize: 11.5, fontWeight: 700, letterSpacing: '.04em', textTransform: 'uppercase', color: 'text.disabled', mb: 0.5 }}>{baslik}</Typography>
      {children}
    </Box>
  );
}

function Kod({ children, hata }: { children: ReactNode; hata?: boolean }) {
  return (
    <Box sx={{ fontFamily: 'ui-monospace,Consolas,monospace', fontSize: 12.5, whiteSpace: 'pre-wrap', wordBreak: 'break-word',
      bgcolor: hata ? 'var(--kds-kirmizidolgu)' : 'var(--kds-zemin2)', color: hata ? 'error.main' : 'text.primary',
      border: '1px solid', borderColor: 'divider', borderRadius: 1.5, p: 1.25 }}>
      {children}
    </Box>
  );
}
