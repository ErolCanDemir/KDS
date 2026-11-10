import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import Drawer from '@mui/material/Drawer';
import Switch from '@mui/material/Switch';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import { CronSecici } from '../components/CronSecici';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import Chip from '@mui/material/Chip';
import Alert from '@mui/material/Alert';
import Tooltip from '@mui/material/Tooltip';
import CircularProgress from '@mui/material/CircularProgress';
import AddIcon from '@mui/icons-material/Add';
import ScheduleSendIcon from '@mui/icons-material/ScheduleSend';
import SendIcon from '@mui/icons-material/Send';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import CloseIcon from '@mui/icons-material/Close';
import RefreshIcon from '@mui/icons-material/Refresh';
import { PageHeader, bandButtonWhite } from '../components/PageHeader';
import { boxRadius } from '../theme';
import { LoadingCurtain } from '../components/Loading';
import { useConfirm } from '../components/ConfirmDialog';
import {
  getSubscriptions, getSubscription, addSubscription, updateSubscription, deleteSubscription, runSubscription,
  type SubscriptionSummary, type SaveSubscriptionInput,
} from '../api/subscriptions';
import { getReports, getReport, type ReportSummary } from '../api/reports';
import { ParametrePanel, ilkParametreDegerleri } from '../components/ParametrePanel';
import type { ReportParameter, ParametreDegerleri } from '../api/queries';
import { kadiYukleniyor, kadiHata, kadiBasarili } from '../kadi/kadiKanal';

const BICIMLER: { deger: string; etiket: string }[] = [
  { deger: 'Pdf', etiket: 'PDF' },
  { deger: 'Xlsx', etiket: 'Excel' },
  { deger: 'Csv', etiket: 'CSV' },
];

type Form = {
  ad: string; raporId: string; bicim: string; alicilar: string[]; zamanlama: string; aktif: boolean;
  parametreDegerleri: ParametreDegerleri; // M24: parametreli raporun gönderim değerleri
};
const bosForm: Form = { ad: '', raporId: '', bicim: 'Pdf', alicilar: [], zamanlama: '', aktif: true, parametreDegerleri: {} };

const epostaGecerli = (e: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(e.trim());

function DurumCip({ a }: { a: SubscriptionSummary }) {
  const stil = (bg: string, fg: string, dot: string, l: string) => (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.7, bgcolor: bg, color: fg, fontSize: 12, fontWeight: 700, borderRadius: 999, px: 1, py: '3px', whiteSpace: 'nowrap' }}>
      <Box component="span" sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: dot }} />{l}
    </Box>
  );
  if (!a.sonDurum) return stil('var(--kds-dolgu)', 'text.secondary', 'text.disabled', 'Hiç gönderilmedi');
  if (a.sonDurum === 'Basarili') {
    const parca = [`${a.sonAliciSayisi ?? 0} alıcı`];
    if (a.sonSatirSayisi != null) parca.push(`${a.sonSatirSayisi.toLocaleString('tr-TR')} satır`);
    return stil('var(--kds-yesildolgu)', 'success.main', 'success.main', `Gönderildi · ${parca.join(' · ')}`);
  }
  return <Tooltip title={a.sonHata ?? ''}>{stil('var(--kds-kirmizidolgu)', 'error.main', 'error.main', 'Hata')}</Tooltip>;
}

export function SubscriptionsPage() {
  const { askConfirm, confirmDialog } = useConfirm();
  const [liste, setListe] = useState<SubscriptionSummary[]>([]);
  const [raporlar, setRaporlar] = useState<ReportSummary[]>([]);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [yuklemeHatasi, setYuklemeHatasi] = useState<string | null>(null);
  const [bilgi, setBilgi] = useState<string | null>(null);
  const [gonderilenId, setGonderilenId] = useState<string | null>(null);

  const [acik, setAcik] = useState(false);
  const [duzenlenenId, setDuzenlenenId] = useState<string | null>(null);
  /* Düzenlemede ALICILAR ve parametre değerleri yalnız detay ucundan gelir; detay inene kadar
   * Kaydet KİLİTLİDİR — eksik formla kaydetmek mevcut aboneliği alıcısız ezerdi (kullanıcı
   * bulgusu 1 Eki 2026: API kesintisinde çekmece boş açıldı ve kayda açıktı). */
  const [detayDurum, setDetayDurum] = useState<'hazir' | 'yukleniyor' | 'hata'>('hazir');
  const [form, setForm] = useState<Form>(bosForm);
  // M24: seçili raporun parametre bildirimi — panel bunlarla çizilir; değerler formda.
  const [raporParametreleri, setRaporParametreleri] = useState<ReportParameter[]>([]);
  useEffect(() => {
    if (!form.raporId) { setRaporParametreleri([]); return; }
    let iptal = false;
    getReport(form.raporId).then((d) => {
      if (iptal) return;
      const params = d.model?.parametreler ?? [];
      setRaporParametreleri(params);
      // Eksik anahtarlar varsayılanla tohumla (edit'te kayıtlı değerlerin üzerine YAZMA).
      setForm((f) => ({ ...f, parametreDegerleri: { ...ilkParametreDegerleri(params), ...f.parametreDegerleri } }));
    }).catch(() => setRaporParametreleri([]));
    return () => { iptal = true; };
  }, [form.raporId]);
  const [aliciGiris, setAliciGiris] = useState('');
  const [kaydediyor, setKaydediyor] = useState(false);
  const [formHata, setFormHata] = useState<string | null>(null);

  async function refresh() {
    setYukleniyor(true);
    setYuklemeHatasi(null);
    try {
      const [abon, raps] = await Promise.all([getSubscriptions(), getReports()]);
      setListe(abon);
      setRaporlar(raps);
    } catch (h) {
      setYuklemeHatasi((h as Error).message); kadiHata();
    } finally {
      setYukleniyor(false);
    }
  }
  useEffect(() => { void refresh(); }, []);

  function openNew() {
    setDetayDurum('hazir');
    setForm({ ...bosForm, raporId: raporlar[0]?.id ?? '' });
    setAliciGiris('');
    setDuzenlenenId(null);
    setFormHata(null);
    setAcik(true);
  }

  async function openEdit(a: SubscriptionSummary) {
    setFormHata(null);
    setAliciGiris('');
    setDuzenlenenId(a.id);
    // Özetten ÖN-doldur: çekmece asla "sıfırdan giriyormuş gibi" açılmaz; detay yalnız
    // alıcıları ve parametre değerlerini tamamlar.
    setForm({
      ad: a.ad, raporId: a.raporId, bicim: a.bicim as Form['bicim'], alicilar: [],
      zamanlama: a.zamanlama ?? '', aktif: a.aktif, parametreDegerleri: {},
    });
    setAcik(true);
    await detayYukle(a.id);
  }

  async function detayYukle(id: string) {
    setDetayDurum('yukleniyor');
    const kadiBitir = kadiYukleniyor('Aboneliği açıyorum…');
    try {
      const d = await getSubscription(id);
      setForm({
        ad: d.ad, raporId: d.raporId, bicim: d.bicim, alicilar: d.alicilar,
        zamanlama: d.zamanlama ?? '', aktif: d.aktif,
        parametreDegerleri: (d.parametreDegerleri as ParametreDegerleri) ?? {},
      });
      setDetayDurum('hazir');
    } catch (h) {
      // Kaydet kilitli kalır (detayDurum 'hata'); kullanıcı çekmeceden "Tekrar dene" ile çözer.
      setDetayDurum('hata');
      setFormHata(`Abonelik detayları alınamadı (${(h as Error).message}). Alıcılar ve parametre değerleri eksik — kaydetmek kapalı; tekrar deneyin.`);
      kadiHata();
    } finally {
      kadiBitir();
    }
  }

  function close() { setAcik(false); setDuzenlenenId(null); }

  function aliciEkle() {
    const e = aliciGiris.trim();
    if (!e) return;
    if (!epostaGecerli(e)) { setFormHata(`Geçersiz e-posta: ${e}`); kadiHata(); return; }
    if (form.alicilar.includes(e)) { setAliciGiris(''); return; }
    setForm((f) => ({ ...f, alicilar: [...f.alicilar, e] }));
    setAliciGiris('');
    setFormHata(null);
  }
  function aliciCikar(e: string) {
    setForm((f) => ({ ...f, alicilar: f.alicilar.filter((x) => x !== e) }));
  }

  async function save() {
    const kadiBitir = kadiYukleniyor('Aboneliği kaydediyorum…');
    setKaydediyor(true);
    setFormHata(null);
    try {
      const govde: SaveSubscriptionInput = {
        ad: form.ad, raporId: form.raporId, bicim: form.bicim, alicilar: form.alicilar,
        zamanlama: form.zamanlama.trim() ? form.zamanlama.trim() : null, aktif: form.aktif,
        parametreDegerleri: raporParametreleri.length > 0 ? form.parametreDegerleri : null, // M24
      };
      if (duzenlenenId) await updateSubscription(duzenlenenId, govde);
      else await addSubscription(govde);
      close();
      kadiBasarili('Kaydettim.');
      await refresh();
    } catch (h) {
      setFormHata((h as Error).message); kadiHata();
    } finally {
      kadiBitir();
      setKaydediyor(false);
    }
  }

  async function gonder(a: SubscriptionSummary) {
    setGonderilenId(a.id);
    // S28c: buton kilitlenirken Kadi ortada sahnede — 'bir şey oluyor' hissi Kadi'den
    const kadiBitir = kadiYukleniyor('Aboneliği gönderiyorum…', 'kos');
    setYuklemeHatasi(null);
    setBilgi(null);
    try {
      const s = await runSubscription(a.id);
      // Mesaj VARSAYMAZ: gerçekten gönderilip gönderilmediğini sunucu söyler (SubscriptionRunResult).
      // Eskiden "dev'de SMTP yok" diye sabitti ve SMTP kurulunca yalan söylemeye başlıyordu.
      const ozet = `${s.aliciSayisi} alıcı · ${s.satirSayisi.toLocaleString('tr-TR')} satır · ${s.dosyaAdi}`;
      setBilgi(s.epostaGonderildi
        ? `"${a.ad}" gönderildi: ${ozet}.`
        : `"${a.ad}" hazırlandı (${ozet}) ama E-POSTA GÖNDERİLMEDİ: SMTP ayarlı değil, içerik yalnız ` +
          `sunucu günlüğüne yazıldı. Göndermek için: Yönetim → Sistem Ayarları → E-posta.`);
      await refresh();
    } catch (h) {
      setYuklemeHatasi(`"${a.ad}" gönderilemedi: ${(h as Error).message}`);
      kadiHata(undefined, /smtp|posta/i.test((h as Error).message) ? [{ etiket: 'E-posta ayarlarına git', rota: '/yonetim/ayarlar' }] : undefined);
    } finally {
      kadiBitir();
      setGonderilenId(null);
    }
  }

  async function sil(a: SubscriptionSummary) {
    if (!(await askConfirm(`"${a.ad}" aboneliği silinsin mi?`, { tehlikeli: true, onayYazisi: 'Sil' }))) return;
    const kadiBitir = kadiYukleniyor('Aboneliği siliyorum…');
    try {
      await deleteSubscription(a.id);
      await refresh();
    } catch (h) {
      setYuklemeHatasi((h as Error).message); kadiHata();
    } finally {
      kadiBitir();
    }
  }

  const gecerli = form.ad.trim() && form.raporId && form.alicilar.length > 0;
  const aktifSayisi = liste.filter((a) => a.aktif && a.zamanlama).length;

  return (
    <Box>
      <PageHeader ikon={<ScheduleSendIcon />} baslik="Zamanlanmış Raporlar"
        aciklama="Bir raporu periyodik (cron) ya da elle çalıştırıp PDF/Excel/CSV ekiyle e-postayla teslim et."
        rozetler={[{ sayi: liste.length, ad: 'abonelik' }, { sayi: aktifSayisi, ad: 'zamanlı' }]}>
        <Button variant="contained" startIcon={<AddIcon />} onClick={openNew} sx={bandButtonWhite}>Yeni Abonelik</Button>
      </PageHeader>

      {bilgi && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setBilgi(null)}>{bilgi}</Alert>}
      {yuklemeHatasi && (
        <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" size="small" startIcon={<RefreshIcon />} onClick={() => void refresh()}>Tekrar</Button>}>
          {yuklemeHatasi}
        </Alert>
      )}

      {yukleniyor ? (
        <LoadingCurtain />
      ) : liste.length === 0 ? (
        <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 5, textAlign: 'center', color: 'text.secondary' }}>
          <Typography sx={{ fontSize: 13.5, fontWeight: 600, mb: 0.5 }}>Henüz abonelik yok.</Typography>
          <Typography sx={{ fontSize: 12.5, mb: 2 }} color="text.disabled">
            Bir raporu düzenli e-postayla almak için <b>Yeni Abonelik</b> ekleyin.
          </Typography>
          <Button variant="contained" startIcon={<AddIcon />} onClick={openNew}>Yeni Abonelik</Button>
        </Paper>
      ) : (
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', lg: 'repeat(3, 1fr)' }, gap: 2 }}>
          {liste.map((a) => (
            <Paper key={a.id} variant="outlined" sx={{ borderRadius: boxRadius, overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
              <Box sx={{ height: 5, bgcolor: a.aktif ? '#0e6f7a' : '#b6c0c9' }} />
              <Box sx={{ p: 2, display: 'flex', flexDirection: 'column', gap: 1, flexGrow: 1 }}>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 1 }}>
                  <Box sx={{ minWidth: 0 }}>
                    <Typography sx={{ fontWeight: 700, fontSize: 14.5 }} color="primary" noWrap>{a.ad}</Typography>
                    <Typography sx={{ fontSize: 12 }} color="text.secondary" noWrap>
                      {a.raporAd ?? '— (rapor silinmiş)'} · {a.aliciSayisi} alıcı
                    </Typography>
                  </Box>
                  <DurumCip a={a} />
                </Box>
                <Box sx={{ display: 'flex', gap: 0.7, flexWrap: 'wrap' }}>
                  <Chip size="small" variant="outlined" label={BICIMLER.find((b) => b.deger === a.bicim)?.etiket ?? a.bicim} />
                  <Chip size="small" variant="outlined" label={a.zamanlama ? `⏱ ${a.zamanlama}` : 'Elle'} />
                  <Chip size="small" variant="outlined" color={a.aktif ? 'success' : 'default'} label={a.aktif ? 'Aktif' : 'Pasif'} />
                  {a.sonGonderimTarihi && (
                    <Chip size="small" variant="outlined" label={`Son: ${new Date(a.sonGonderimTarihi).toLocaleString('tr-TR')}`} />
                  )}
                </Box>
                <Box sx={{ flexGrow: 1 }} />
                <Box sx={{ display: 'flex', gap: 1, pt: 1, borderTop: '1px solid', borderColor: 'divider' }}>
                  <Button size="small" variant="contained" fullWidth disabled={gonderilenId === a.id} onClick={() => void gonder(a)}
                    startIcon={gonderilenId === a.id ? <CircularProgress size={14} color="inherit" /> : <SendIcon />}>
                    Şimdi Gönder
                  </Button>
                  <Tooltip title="Düzenle"><IconButton size="small" aria-label="Düzenle" onClick={() => void openEdit(a)}><EditOutlinedIcon fontSize="small" /></IconButton></Tooltip>
                  <Tooltip title="Sil"><IconButton size="small" aria-label="Sil" onClick={() => void sil(a)}><DeleteOutlineIcon fontSize="small" /></IconButton></Tooltip>
                </Box>
              </Box>
            </Paper>
          ))}
          <Paper variant="outlined" onClick={openNew}
            sx={{ borderRadius: boxRadius, borderStyle: 'dashed', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 1, minHeight: 168, cursor: 'pointer', color: 'secondary.main', '&:hover': { bgcolor: 'action.hover' } }}>
            <Box sx={{ width: 44, height: 44, borderRadius: '50%', bgcolor: 'var(--kds-tealdolgu)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}><AddIcon /></Box>
            <Typography sx={{ fontWeight: 700, fontSize: 14 }}>Yeni Abonelik</Typography>
          </Paper>
        </Box>
      )}

      {/* Editör → sağ çekmece (yeni + düzenle) */}
      <Drawer anchor="right" open={acik} onClose={close} slotProps={{ paper: { sx: { width: { xs: '92%', sm: 520 } } } }}>
        <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', p: 2, borderBottom: '1px solid', borderColor: 'divider' }}>
            <Typography sx={{ fontWeight: 700 }} color="primary">{duzenlenenId ? 'Aboneliği Düzenle' : 'Yeni Abonelik'}</Typography>
            <IconButton size="small" onClick={close}><CloseIcon fontSize="small" /></IconButton>
          </Box>

          <Box sx={{ p: 2, flexGrow: 1, overflowY: 'auto', bgcolor: 'background.paper', display: 'flex', flexDirection: 'column', gap: 2 }}>
            <TextField label="Abonelik adı" size="small" fullWidth value={form.ad}
              onChange={(e) => setForm((f) => ({ ...f, ad: e.target.value }))} placeholder="ör. Haftalık Satış Özeti" />

            <FormControl size="small" fullWidth>
              <InputLabel>Rapor</InputLabel>
              <Select label="Rapor" value={raporlar.some((r) => r.id === form.raporId) ? form.raporId : ''}
                onChange={(e) => setForm((f) => ({ ...f, raporId: e.target.value }))}>
                {raporlar.map((r) => <MenuItem key={r.id} value={r.id}>{r.ad}</MenuItem>)}
              </Select>
            </FormControl>
            {raporlar.length === 0 && (
              <Alert severity="info" sx={{ fontSize: 12.5 }}>Önce bir rapor oluşturun (Raporlar ekranı) — abonelik bir raporu teslim eder.</Alert>
            )}

            {/* M24/M25: parametreli raporda değerler ABONELİKLE saklanır; gönderim bu değerlerle
                koşar. Tarih aralığında "Bugün" işareti → her gönderimde o günün tarihi. */}
            {raporParametreleri.length > 0 && (
              <Box>
                <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: 'text.secondary', mb: 0.8 }}>
                  Rapor parametreleri — gönderim bu değerlerle çalışır
                </Typography>
                <ParametrePanel parametreler={raporParametreleri} degerler={form.parametreDegerleri}
                  onChange={(d) => setForm((f) => ({ ...f, parametreDegerleri: d }))} />
              </Box>
            )}

            {/* Biçim */}
            <Box>
              <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: 'text.secondary', mb: 0.8 }}>Ek biçimi</Typography>
              <ToggleButtonGroup exclusive size="small" fullWidth value={form.bicim}
                onChange={(_, v: string | null) => { if (v != null) setForm((f) => ({ ...f, bicim: v })); }}>
                {BICIMLER.map((b) => <ToggleButton key={b.deger} value={b.deger}>{b.etiket}</ToggleButton>)}
              </ToggleButtonGroup>
            </Box>

            {/* Alıcılar */}
            <Box>
              <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: 'text.secondary', mb: 0.8 }}>
                Alıcılar {form.alicilar.length > 0 && `(${form.alicilar.length})`}
              </Typography>
              <Box sx={{ display: 'flex', gap: 1, mb: 1 }}>
                <TextField size="small" fullWidth placeholder="e-posta ekle (Enter)" value={aliciGiris} type="email"
                  onChange={(e) => setAliciGiris(e.target.value)}
                  onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); aliciEkle(); } }} />
                <Button variant="outlined" onClick={aliciEkle} startIcon={<AddIcon />}>Ekle</Button>
              </Box>
              {form.alicilar.length === 0 ? (
                <Typography sx={{ fontSize: 12.5 }} color="text.disabled">Henüz alıcı yok — e-posta ekleyin.</Typography>
              ) : (
                <Box sx={{ display: 'flex', gap: 0.7, flexWrap: 'wrap' }}>
                  {form.alicilar.map((e) => <Chip key={e} label={e} size="small" onDelete={() => aliciCikar(e)} />)}
                </Box>
              )}
            </Box>

            {/* Zamanlama — M23: çıplak cron kutusu yerine anlaşılır seçici + Türkçe özet. */}
            <Box>
              <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: 'text.secondary', mb: 0.8 }}>Zamanlama</Typography>
              <CronSecici deger={form.zamanlama} degisti={(z) => setForm((f) => ({ ...f, zamanlama: z }))} />
            </Box>

            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Switch checked={form.aktif} onChange={(e) => setForm((f) => ({ ...f, aktif: e.target.checked }))} />
              <Typography sx={{ fontSize: 13.5 }}>Aktif (zamanlayıcı bu aboneliği gönderir)</Typography>
            </Box>

            {formHata && (
              <Alert severity="error" sx={{ py: 0.2 }}
                action={detayDurum === 'hata' && duzenlenenId
                  ? <Button color="inherit" size="small" onClick={() => void detayYukle(duzenlenenId)}>Tekrar dene</Button>
                  : undefined}>
                {formHata}
              </Alert>
            )}
          </Box>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, p: 2, borderTop: '1px solid', borderColor: 'divider' }}>
            <Typography sx={{ fontSize: 11.5, flexGrow: 1 }} color="text.disabled">
              Her gönderimde rapor o an çalışır (güncel veri).
            </Typography>
            <Button onClick={close} color="inherit">İptal</Button>
            <Button variant="contained" onClick={() => void save()} disabled={!gecerli || kaydediyor || detayDurum !== 'hazir'}
              startIcon={kaydediyor ? <CircularProgress size={15} color="inherit" /> : undefined}>Kaydet</Button>
          </Box>
        </Box>
      </Drawer>

      {confirmDialog}
    </Box>
  );
}
