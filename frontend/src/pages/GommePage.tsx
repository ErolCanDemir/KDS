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
import MenuItem from '@mui/material/MenuItem';
import Autocomplete from '@mui/material/Autocomplete';
import IntegrationInstructionsIcon from '@mui/icons-material/IntegrationInstructions';
import { PageHeader, bandButtonWhite } from '../components/PageHeader';
import { LoadingCurtain } from '../components/Loading';
import { boxRadius } from '../theme';
import { getGommeAnahtarlari, uretGommeAnahtari, iptalGommeAnahtari, type EmbedToken, type EmbedTokenSonuc } from '../api/gomme';
import { getReports, type ReportSummary } from '../api/reports';
import { kullaniciAra, type RolUyesi } from '../api/roller';
import { kadiYukleniyor, kadiHata, kadiBasarili } from '../kadi/kadiKanal';

/** Admin bilerek yok: admin satır güvenliğinden muaftır, yani admin rollü bir anahtar bütün
 * kuralları devre dışı bırakır. Sunucu da reddediyor — burada listelemek, kullanıcıya
 * seçilebilirmiş gibi göstermek olurdu. */
const SECILEBILIR_ROLLER = ['gelistirici', 'musteri'];

const tarih = (s: string) => new Date(s).toLocaleDateString('tr-TR', { day: '2-digit', month: 'short', year: 'numeric' });

const durumRengi: Record<string, 'success' | 'warning' | 'error'> = {
  gecerli: 'success', 'suresi-dolmus': 'warning', iptal: 'error',
};
const durumEtiketi: Record<string, string> = {
  gecerli: 'geçerli', 'suresi-dolmus': 'süresi dolmuş', iptal: 'iptal edildi',
};

/** S42 — Yönetim → Gömme & Marka: dış siteye rapor gömmek için imzalı anahtarlar.
 *
 * Güvenlik modeli ekranda da görünür: anahtar KİMİN adına koşacak, HANGİ rollerle (satır
 * güvenliği buna bakar) ve NEREDEN kullanılabilir. Üretilen anahtar yalnız bir kez gösterilir. */
export function GommePage() {
  const [anahtarlar, setAnahtarlar] = useState<EmbedToken[]>([]);
  const [raporlar, setRaporlar] = useState<ReportSummary[]>([]);
  const [kullanicilar, setKullanicilar] = useState<RolUyesi[]>([]);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [hata, setHata] = useState<string | null>(null);
  const [acik, setAcik] = useState(false);
  const [uretilen, setUretilen] = useState<EmbedTokenSonuc | null>(null);

  // Form
  const [ad, setAd] = useState('');
  const [raporId, setRaporId] = useState('');
  const [kullanici, setKullanici] = useState<RolUyesi | null>(null);
  const [roller, setRoller] = useState<string[]>([]);
  const [alanlar, setAlanlar] = useState<string[]>([]);
  const [gun, setGun] = useState(90);
  const [kaydediyor, setKaydediyor] = useState(false);

  const tazele = () =>
    getGommeAnahtarlari().then(setAnahtarlar).catch((e) => {
      setHata(e instanceof Error ? e.message : 'Anahtarlar alınamadı.'); kadiHata();
    });

  useEffect(() => {
    void Promise.all([
      tazele(),
      getReports().then(setRaporlar).catch(() => setRaporlar([])),
      kullaniciAra('').then(setKullanicilar).catch(() => setKullanicilar([])),
    ]).finally(() => setYukleniyor(false));
  }, []);

  const uret = async () => {
    setKaydediyor(true); setHata(null);
    const kadiBitir = kadiYukleniyor('Gömme anahtarını üretiyorum…');
    try {
      const sonuc = await uretGommeAnahtari({
        ad: ad.trim(),
        raporId: raporId || null,
        panoId: null,
        kosanKullaniciId: kullanici?.keycloakId ?? '',
        roller,
        izinliAlanlar: alanlar,
        gecerlilikGun: gun,
      });
      // {KDS_ADRESI} yer tutucusunu tarayıcıdaki gerçek adresle doldur: sunucu ters vekil
      // arkasında kendi dış adresini güvenilir bilemez.
      setUretilen({ ...sonuc, gomKodu: sonuc.gomKodu.replace('{KDS_ADRESI}', window.location.origin) });
      setAcik(false);
      setAd(''); setRaporId(''); setKullanici(null); setRoller([]); setAlanlar([]);
      await tazele();
      kadiBasarili();
    } catch (e) {
      setHata(e instanceof Error ? e.message : 'Anahtar üretilemedi.'); kadiHata();
    } finally {
      kadiBitir(); setKaydediyor(false);
    }
  };

  const iptal = async (a: EmbedToken) => {
    if (!window.confirm(`"${a.ad}" anahtarı iptal edilsin mi? Gömülü sayfalar anında çalışmayı bırakır.`)) return;
    try {
      await iptalGommeAnahtari(a.id);
      await tazele();
      kadiBasarili();
    } catch (e) {
      setHata(e instanceof Error ? e.message : 'İptal edilemedi.'); kadiHata();
    }
  };

  if (yukleniyor) return <LoadingCurtain mesaj="Gömme anahtarlarını getiriyorum…" />;

  const formGecerli = ad.trim() && raporId && kullanici && roller.length > 0 && alanlar.length > 0;

  return (
    <Box>
      <PageHeader ikon={<IntegrationInstructionsIcon />} baslik="Gömme & Marka"
        aciklama="Raporu dış siteye göm: anahtar kimin adına, hangi rollerle ve nereden çalışacağını taşır.">
        <Button variant="contained" onClick={() => setAcik(true)} sx={bandButtonWhite}>Anahtar üret</Button>
      </PageHeader>

      {hata && <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => setHata(null)}>{hata}</Alert>}

      <Alert severity="info" sx={{ mb: 1.5, borderRadius: boxRadius }}>
        Gömülü rapor, anahtarın taşıdığı kimlikle çalışır ve <b>satır güvenliği aynen uygulanır</b>.
        Bu yüzden rol seçimi zorunludur; admin rolü verilemez (admin kuralların dışındadır).
      </Alert>

      <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 2 }}>
        {anahtarlar.length === 0 && (
          <Typography sx={{ fontSize: 13 }} color="text.disabled">
            Henüz anahtar yok. "Anahtar üret" ile bir raporu dış siteye açabilirsiniz.
          </Typography>
        )}
        {anahtarlar.map((a) => (
          <Box key={a.id} sx={{ display: 'flex', alignItems: 'center', gap: 1.5, py: 1,
            borderBottom: '1px solid', borderColor: 'divider', flexWrap: 'wrap' }}>
            <Box sx={{ minWidth: 220, flexGrow: 1 }}>
              <Typography sx={{ fontSize: 13.5, fontWeight: 700 }}>{a.ad}</Typography>
              <Typography sx={{ fontSize: 11.5 }} color="text.disabled">
                {a.hedefTur}: {a.hedefAd ?? '(silinmiş)'} · {a.kosanKullanici ?? '—'} adına ·
                {' '}{a.izinliAlanlar.join(', ')}
              </Typography>
            </Box>
            <Typography sx={{ fontSize: 11.5 }} color="text.secondary">
              {tarih(a.gecerlilikBitis)}'e kadar
              {a.sonKullanim ? ` · son kullanım ${tarih(a.sonKullanim)}` : ' · hiç kullanılmadı'}
            </Typography>
            <Chip size="small" variant="outlined" color={durumRengi[a.durum] ?? 'default'}
              label={durumEtiketi[a.durum] ?? a.durum} sx={{ height: 22, fontSize: 11 }} />
            {a.durum === 'gecerli' && (
              <Button size="small" color="error" onClick={() => void iptal(a)}>İptal et</Button>
            )}
          </Box>
        ))}
      </Paper>

      {/* Üretim formu */}
      <Dialog open={acik} onClose={() => setAcik(false)} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ fontWeight: 800 }}>Gömme anahtarı üret</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '8px !important' }}>
          <TextField label="Anahtar adı" size="small" value={ad} onChange={(e) => setAd(e.target.value)}
            placeholder="ör. Bayi portalı — ciro kartı" autoFocus />

          <TextField select label="Gömülecek rapor" size="small" value={raporId}
            onChange={(e) => setRaporId(e.target.value)}
            helperText="Elle SQL ile kurulmuş raporlar gömülemez — onlarda satır güvenliği uygulanamıyor.">
            {raporlar.map((r) => <MenuItem key={r.id} value={r.id}>{r.ad}</MenuItem>)}
          </TextField>

          <Autocomplete size="small" options={kullanicilar} value={kullanici}
            onChange={(_, v) => setKullanici(v)}
            getOptionLabel={(o) => o.adSoyad ?? o.kullaniciAdi}
            renderInput={(p) => <TextField {...p} label="Kimin adına çalışsın"
              helperText="Satır güvenliği bu kimliğe göre uygulanır." />} />

          <Autocomplete multiple size="small" options={SECILEBILIR_ROLLER} value={roller}
            onChange={(_, v) => setRoller(v)}
            renderInput={(p) => <TextField {...p} label="Roller (zorunlu)"
              helperText="Satır kuralları role bakar; rolsüz anahtar o kuralları atlar. Admin seçilemez." />} />

          <Autocomplete multiple freeSolo size="small" options={[]} value={alanlar}
            onChange={(_, v) => setAlanlar(v.map((x) => String(x).trim().toLowerCase()))}
            renderInput={(p) => <TextField {...p} label="İzinli alan adları (zorunlu)"
              placeholder="portal.firma.com"
              helperText="Anahtar yalnız bu adreslerden çalışır. Yazıp Enter'a basın." />} />

          <TextField type="number" label="Geçerlilik (gün)" size="small" value={gun}
            onChange={(e) => setGun(Number(e.target.value))} slotProps={{ htmlInput: { min: 1, max: 3650 } }} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAcik(false)}>Vazgeç</Button>
          <Button variant="contained" disabled={!formGecerli || kaydediyor} onClick={() => void uret()}>Üret</Button>
        </DialogActions>
      </Dialog>

      {/* Üretilen anahtar — YALNIZ BİR KEZ gösterilir */}
      <Dialog open={uretilen !== null} onClose={() => setUretilen(null)} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ fontWeight: 800 }}>Anahtar üretildi</DialogTitle>
        <DialogContent>
          <Alert severity="warning" sx={{ mb: 1.5 }}>
            Bu anahtar <b>yalnız şimdi</b> gösteriliyor; kapatınca bir daha görüntülenemez.
            Kaybederseniz yenisini üretip eskisini iptal edin.
          </Alert>
          <Typography sx={{ fontSize: 12, fontWeight: 700, mb: 0.5 }}>Anahtar</Typography>
          <Box component="pre" sx={{ fontSize: 11.5, whiteSpace: 'pre-wrap', wordBreak: 'break-all',
            bgcolor: 'action.hover', p: 1.25, borderRadius: 1, m: 0, mb: 1.5 }}>
            {uretilen?.anahtar}
          </Box>
          <Typography sx={{ fontSize: 12, fontWeight: 700, mb: 0.5 }}>Gömme kodu</Typography>
          <Box component="pre" sx={{ fontSize: 11.5, whiteSpace: 'pre-wrap', wordBreak: 'break-all',
            bgcolor: 'action.hover', p: 1.25, borderRadius: 1, m: 0 }}>
            {uretilen?.gomKodu}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => void navigator.clipboard?.writeText(uretilen?.gomKodu ?? '')}>Gömme kodunu kopyala</Button>
          <Button variant="contained" onClick={() => setUretilen(null)}>Kapat</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
