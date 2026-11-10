import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import Chip from '@mui/material/Chip';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import RouterIcon from '@mui/icons-material/Router';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import BlockIcon from '@mui/icons-material/Block';
import { PageHeader, bandButtonWhite } from '../components/PageHeader';
import { LoadingCurtain } from '../components/Loading';
import { useConfirm } from '../components/ConfirmDialog';
import { boxRadius } from '../theme';
import { getAjanlar, uretAjan, iptalAjan, cevrimici, type Ajan, type AjanSonuc } from '../api/ajanlar';
import { kadiYukleniyor, kadiHata, kadiBasarili } from '../kadi/kadiKanal';

const tarihSaat = (s: string) => new Date(s).toLocaleString('tr-TR');

/** S45 — Yönetim → Ajanlar: müşteri ağına kurulan KDS Ajanı kayıtları.
 *
 * Güvenlik modeli ekranda da görünür: kaynak bağlantı dizeleri AJANIN makinesindeki ajan.json'da
 * durur, KDS'ye gönderilmez; buradaki anahtar yalnız ajanı KDS'ye tanıtır. Anahtar bir kez
 * gösterilir (parola deseni); iptal anında etkilidir. */
export function AjanlarPage() {
  const { askConfirm, confirmDialog } = useConfirm();
  const [ajanlar, setAjanlar] = useState<Ajan[]>([]);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [hata, setHata] = useState<string | null>(null);
  const [acik, setAcik] = useState(false);
  const [ad, setAd] = useState('');
  const [kaydediyor, setKaydediyor] = useState(false);
  const [uretilen, setUretilen] = useState<AjanSonuc | null>(null);

  const tazele = () =>
    getAjanlar().then(setAjanlar).catch((e) => {
      setHata(e instanceof Error ? e.message : 'Ajanlar alınamadı.'); kadiHata();
    });

  useEffect(() => { void tazele().finally(() => setYukleniyor(false)); }, []);

  const uret = async () => {
    setKaydediyor(true); setHata(null);
    const kadiBitir = kadiYukleniyor('Ajan anahtarını üretiyorum…');
    try {
      const sonuc = await uretAjan(ad.trim());
      // {KDS_ADRESI} yer tutucusu tarayıcıdaki gerçek adresle doldurulur (Gömme'deki karar:
      // sunucu ters vekil arkasında kendi dış adresini güvenilir bilemez).
      setUretilen({ ...sonuc, ornekYapilandirma: sonuc.ornekYapilandirma.replace('{KDS_ADRESI}', window.location.origin.replace(':5173', ':5210')) });
      setAcik(false); setAd('');
      await tazele();
      kadiBasarili('Ajan hazır — anahtarı şimdi kopyala, bir daha gösterilmeyecek.');
    } catch (e) {
      setHata(e instanceof Error ? e.message : 'Ajan üretilemedi.'); kadiHata();
    } finally {
      kadiBitir(); setKaydediyor(false);
    }
  };

  const iptal = async (a: Ajan) => {
    if (!(await askConfirm(
      `"${a.ad}" ajanı iptal edilsin mi? Ajanın anahtarı anında geçersizleşir; bağlı ${a.bagliBaglantiSayisi} bağlantının işleri koşamaz olur.`,
      { tehlikeli: true, onayYazisi: 'İptal Et' }))) return;
    try {
      await iptalAjan(a.id);
      await tazele();
      kadiBasarili();
    } catch (e) {
      setHata(e instanceof Error ? e.message : 'İptal edilemedi.'); kadiHata();
    }
  };

  if (yukleniyor) return <LoadingCurtain mesaj="Ajanları getiriyorum…" />;

  return (
    <Box>
      <PageHeader ikon={<RouterIcon />} baslik="Ajanlar"
        aciklama="Müşteri ağına kurulan KDS Ajanı: içe alım işlerini kendi tarafında koşturur, veriyi depoya iter.">
        <Button variant="contained" onClick={() => setAcik(true)} sx={bandButtonWhite}>Ajan üret</Button>
      </PageHeader>

      {hata && <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => setHata(null)}>{hata}</Alert>}

      {ajanlar.length === 0 ? (
        <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 5, textAlign: 'center', color: 'text.secondary' }}>
          <Typography sx={{ fontSize: 13.5, fontWeight: 600, mb: 0.5 }}>Henüz ajan yok.</Typography>
          <Typography sx={{ fontSize: 12.5 }} color="text.disabled">
            Kaynak veritabanı müşterinin ağındaysa ve buluttan erişilemiyorsa: "Ajan üret" ile anahtar al,
            ajanı o ağdaki bir makineye kur — firewall'da delik açmadan veri KDS'ye akar.
          </Typography>
        </Paper>
      ) : (
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', lg: 'repeat(3, 1fr)' }, gap: 2 }}>
          {ajanlar.map((a) => (
            <Paper key={a.id} variant="outlined" sx={{ borderRadius: boxRadius, p: 2, display: 'flex', flexDirection: 'column', gap: 1, opacity: a.durum === 'iptal' ? 0.6 : 1 }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <RouterIcon fontSize="small" color={cevrimici(a) ? 'success' : 'disabled'} />
                <Typography sx={{ fontWeight: 700, fontSize: 14.5, flexGrow: 1 }} noWrap>{a.ad}</Typography>
                {a.durum === 'iptal'
                  ? <Chip size="small" color="error" label="iptal edildi" />
                  : cevrimici(a)
                    ? <Chip size="small" color="success" label="çevrimiçi" />
                    : <Chip size="small" variant="outlined" label="çevrimdışı" />}
              </Box>
              <Typography sx={{ fontSize: 12 }} color="text.secondary">
                {a.bagliBaglantiSayisi} bağlı bağlantı
                {a.sonGorulme ? ` · son görülme ${tarihSaat(a.sonGorulme)}` : ' · hiç bağlanmadı'}
              </Typography>
              <Box sx={{ flexGrow: 1 }} />
              {a.durum === 'gecerli' && (
                <Box sx={{ display: 'flex', justifyContent: 'flex-end', pt: 1, borderTop: '1px solid', borderColor: 'divider' }}>
                  <Tooltip title="İptal et — anahtar anında geçersizleşir">
                    <IconButton size="small" onClick={() => void iptal(a)} sx={{ '&:hover': { color: 'error.main' } }}>
                      <BlockIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </Box>
              )}
            </Paper>
          ))}
        </Box>
      )}

      {/* Yeni ajan diyaloğu */}
      <Dialog open={acik} onClose={() => setAcik(false)} maxWidth="xs" fullWidth>
        <DialogTitle sx={{ fontWeight: 700 }}>Yeni Ajan</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '8px !important' }}>
          <Typography sx={{ fontSize: 12.5 }} color="text.secondary">
            Ajan, müşteri ağındaki bir makinede çalışır ve KDS'ye DIŞARI DOĞRU bağlanır.
            Kaynak bağlantı dizeleri o makinedeki ajan.json'da kalır — KDS'ye gönderilmez.
          </Typography>
          <TextField label="Ajan adı" size="small" autoFocus value={ad}
            onChange={(e) => setAd(e.target.value)} placeholder="ör. Merkez fabrika ağı" />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAcik(false)} color="inherit">İptal</Button>
          <Button variant="contained" disabled={!ad.trim() || kaydediyor} onClick={() => void uret()}>Üret</Button>
        </DialogActions>
      </Dialog>

      {/* Üretilen anahtar — YALNIZ BİR KEZ gösterilir */}
      <Dialog open={uretilen !== null} onClose={() => setUretilen(null)} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ fontWeight: 700 }}>Ajan hazır — anahtarı şimdi kopyala</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.5, pt: '8px !important' }}>
          <Alert severity="warning" sx={{ fontSize: 12.5 }}>
            Bu anahtar <b>bir daha gösterilmez</b> (saklamıyoruz). Kaybedersen ajanı iptal edip yenisini üret.
          </Alert>
          <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: 'text.secondary' }}>
            Ajanın makinesine konacak ajan.json:
          </Typography>
          <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 1.5, position: 'relative', bgcolor: 'var(--kds-dolgu)' }}>
            <Box component="pre" sx={{ m: 0, fontSize: 11.5, fontFamily: 'Consolas, monospace', whiteSpace: 'pre-wrap', wordBreak: 'break-all', maxHeight: 260, overflowY: 'auto' }}>
              {uretilen?.ornekYapilandirma}
            </Box>
            <Tooltip title="Kopyala">
              <IconButton size="small" sx={{ position: 'absolute', top: 6, right: 6 }}
                onClick={() => { void navigator.clipboard.writeText(uretilen?.ornekYapilandirma ?? ''); kadiBasarili('Kopyaladım.'); }}>
                <ContentCopyIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          </Paper>
          <Typography sx={{ fontSize: 12 }} color="text.secondary">
            Sonraki adımlar: (1) <b>baglantilar</b> bölümüne, ajanlı bağlantının kimliğini ve o ağdaki
            GERÇEK bağlantı dizesini yaz. (2) Bağlantılar ekranında İçe Alım modlu kaynağa bu ajanı seç.
            (3) Ajanı çalıştır: <code>Kds.Ajan.exe ajan.json</code>
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button variant="contained" onClick={() => setUretilen(null)}>Anahtarı kopyaladım, kapat</Button>
        </DialogActions>
      </Dialog>

      {confirmDialog}
    </Box>
  );
}
