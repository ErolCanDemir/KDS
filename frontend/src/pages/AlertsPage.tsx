import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import Drawer from '@mui/material/Drawer';
import Switch from '@mui/material/Switch';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import Chip from '@mui/material/Chip';
import MuiAlert from '@mui/material/Alert';
import Tooltip from '@mui/material/Tooltip';
import CircularProgress from '@mui/material/CircularProgress';
import AddIcon from '@mui/icons-material/Add';
import NotificationsActiveIcon from '@mui/icons-material/NotificationsActive';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import CloseIcon from '@mui/icons-material/Close';
import RefreshIcon from '@mui/icons-material/Refresh';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import { CronSecici } from '../components/CronSecici';
import { PageHeader, bandButtonWhite } from '../components/PageHeader';
import { boxRadius } from '../theme';
import { LoadingCurtain } from '../components/Loading';
import { useConfirm } from '../components/ConfirmDialog';
import {
  getAlerts, getAlert, addAlert, updateAlert, deleteAlert, tryAlert,
  type AlertSummary, type SaveAlertInput, type OzetTuru, type UyariOp,
} from '../api/alerts';
import { getReports, getReport, type ReportSummary } from '../api/reports';
import { getMetrics, type MetricSummary } from '../api/metrics';
import { ParametrePanel, ilkParametreDegerleri } from '../components/ParametrePanel';
import type { ReportParameter, ParametreDegerleri } from '../api/queries';
import { kadiYukleniyor, kadiHata, kadiBasarili } from '../kadi/kadiKanal';

const OZETLER: { deger: OzetTuru; etiket: string; kolonIster: boolean }[] = [
  { deger: 'SatirSayisi', etiket: 'Satır sayısı', kolonIster: false },
  { deger: 'Toplam', etiket: 'Toplam', kolonIster: true },
  { deger: 'Ortalama', etiket: 'Ortalama', kolonIster: true },
  { deger: 'EnBuyuk', etiket: 'En büyük', kolonIster: true },
  { deger: 'EnKucuk', etiket: 'En küçük', kolonIster: true },
  { deger: 'IlkSatir', etiket: 'İlk satır', kolonIster: true },
];

const OPLAR: { deger: UyariOp; etiket: string }[] = [
  { deger: 'Buyuk', etiket: '>' },
  { deger: 'BuyukEsit', etiket: '≥' },
  { deger: 'Kucuk', etiket: '<' },
  { deger: 'KucukEsit', etiket: '≤' },
  { deger: 'Esit', etiket: '=' },
  { deger: 'EsitDegil', etiket: '≠' },
];

type Form = {
  ad: string;
  // S30: kaynak İKİLİ — uyarı ya bir raporu ya bir metriği izler.
  kaynak: 'rapor' | 'metrik';
  raporId: string; metrikId: string;
  kolon: string; ozet: OzetTuru; op: UyariOp; esik: string;
  alicilar: string[]; zamanlama: string; aktif: boolean; surekliBildir: boolean;
  // Parametreli raporun uyarıyla SAKLANAN değerleri: bunlar gönderilmezse zorunlu parametreli
  // rapora kurulan uyarı her koşumda "değer girin" hatasıyla düşer (abonelikteki M24 dersi).
  parametreDegerleri: ParametreDegerleri;
};
const bosForm: Form = {
  ad: '', kaynak: 'rapor', raporId: '', metrikId: '', kolon: '', ozet: 'SatirSayisi', op: 'Buyuk', esik: '0',
  alicilar: [], zamanlama: '', aktif: true, surekliBildir: false, parametreDegerleri: {},
};

const epostaGecerli = (e: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(e.trim());
const kolonIster = (o: OzetTuru) => OZETLER.find((x) => x.deger === o)?.kolonIster ?? true;

/** Kuralı cümle gibi okutur — "Toplam Tutar > 500" / "Metrik 'Ciro' < 100". Ekran ve e-posta aynı dili kullanır. */
function kosulMetni(u: { ozet: OzetTuru; kolon: string | null; op: UyariOp; esik: number; metrikAd?: string | null }) {
  const sol = u.metrikAd
    ? `Metrik '${u.metrikAd}'`
    : u.ozet === 'SatirSayisi'
      ? 'Satır sayısı'
      : `${OZETLER.find((o) => o.deger === u.ozet)?.etiket ?? ''} ${u.kolon ?? ''}`.trim();
  return `${sol} ${OPLAR.find((o) => o.deger === u.op)?.etiket ?? '>'} ${u.esik}`;
}

function DurumCip({ u }: { u: AlertSummary }) {
  const stil = (bg: string, fg: string, dot: string, l: string) => (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.7, bgcolor: bg, color: fg, fontSize: 12, fontWeight: 700, borderRadius: 999, px: 1, py: '3px', whiteSpace: 'nowrap' }}>
      <Box component="span" sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: dot }} />{l}
    </Box>
  );
  if (u.durum === 'Hata') {
    return <Tooltip title={u.sonHata ?? ''}>{stil('var(--kds-kirmizidolgu)', 'error.main', 'error.main', 'Hata')}</Tooltip>;
  }
  if (u.durum === 'Tetiklendi') return stil('#fdf1e3', '#a35a09', '#e8912d', 'Tetiklendi');
  if (u.durum === 'Sakin') return stil('#e9f6ec', '#1e7a37', '#34a853', 'Sakin');
  return stil('var(--kds-dolgu)', 'text.secondary', 'text.disabled', 'Hiç kontrol edilmedi');
}

export default function AlertsPage() {
  const [liste, setListe] = useState<AlertSummary[] | null>(null);
  const [raporlar, setRaporlar] = useState<ReportSummary[]>([]);
  const [metrikler, setMetrikler] = useState<MetricSummary[]>([]);
  const [acik, setAcik] = useState(false);
  const [duzenlenenId, setDuzenlenenId] = useState<string | null>(null);
  const [form, setForm] = useState<Form>(bosForm);
  const [yeniAlici, setYeniAlici] = useState('');
  const [denenenId, setDenenenId] = useState<string | null>(null);
  const [bilgi, setBilgi] = useState<string | null>(null);
  const [hata, setHata] = useState<string | null>(null);
  // Kaydetme hatası ÇEKMECEDE gösterilir: sayfa gövdesindeki şerit modal çekmecenin arkasında
  // kalıyordu — doğrulama reddi (ör. hatalı cron) kullanıcıya hiç ulaşmıyordu (inceleme bulgusu).
  const [formHata, setFormHata] = useState<string | null>(null);
  const [kaydediliyor, setKaydediliyor] = useState(false);
  const [raporParametreleri, setRaporParametreleri] = useState<ReportParameter[]>([]);
  const { askConfirm, confirmDialog } = useConfirm();

  const yenile = async () => {
    try {
      setListe(await getAlerts());
    } catch (h) {
      setHata((h as Error).message); kadiHata();
    }
  };

  useEffect(() => {
    void yenile();
    getReports().then(setRaporlar).catch(() => setRaporlar([]));
    getMetrics().then((m) => setMetrikler(m.filter((x) => x.aktif))).catch(() => setMetrikler([]));
  }, []);

  // Seçilen raporun parametreleri: eksik anahtarlar varsayılanla tohumlanır, düzenlemede kayıtlı
  // değerlerin ÜZERİNE yazılmaz.
  useEffect(() => {
    if (!form.raporId) { setRaporParametreleri([]); return; }
    let iptal = false;
    getReport(form.raporId).then((d) => {
      if (iptal) return;
      const params = d.model?.parametreler ?? [];
      setRaporParametreleri(params);
      setForm((f) => ({ ...f, parametreDegerleri: { ...ilkParametreDegerleri(params), ...f.parametreDegerleri } }));
    }).catch(() => setRaporParametreleri([]));
    return () => { iptal = true; };
  }, [form.raporId]);

  function yeniAc() {
    setFormHata(null);
    setDuzenlenenId(null);
    setForm(bosForm);
    setYeniAlici('');
    setAcik(true);
  }

  async function duzenleAc(id: string) {
    setFormHata(null);
    setHata(null);
    const kadiBitir = kadiYukleniyor('Uyarıyı açıyorum…');
    let d: Awaited<ReturnType<typeof getAlert>>;
    try { d = await getAlert(id); }
    catch (h) { setHata((h as Error).message); kadiHata(); return; }
    finally { kadiBitir(); }
    setDuzenlenenId(id);
    setForm({
      ad: d.ad, kaynak: d.metrikId ? 'metrik' : 'rapor',
      raporId: d.raporId ?? '', metrikId: d.metrikId ?? '',
      kolon: d.kolon ?? '', ozet: d.ozet, op: d.op, esik: String(d.esik),
      alicilar: d.alicilar, zamanlama: d.zamanlama ?? '', aktif: d.aktif, surekliBildir: d.surekliBildir,
      parametreDegerleri: (d.parametreDegerleri as ParametreDegerleri) ?? {},
    });
    setYeniAlici('');
    setAcik(true);
  }

  function aliciEkle() {
    const e = yeniAlici.trim();
    if (!epostaGecerli(e) || form.alicilar.includes(e)) return;
    setForm((f) => ({ ...f, alicilar: [...f.alicilar, e] }));
    setYeniAlici('');
  }

  async function kaydet() {
    setKaydediliyor(true);
    setFormHata(null);
    const kadiBitir = kadiYukleniyor('Uyarıyı kaydediyorum…');
    try {
      const raporYolu = form.kaynak === 'rapor';
      const girdi: SaveAlertInput = {
        ad: form.ad.trim(),
        raporId: raporYolu ? form.raporId : null,
        metrikId: raporYolu ? null : form.metrikId,
        // Metrik yolunda kolon/özet anlamsız — metrik zaten tek değere iner.
        kolon: raporYolu && kolonIster(form.ozet) ? form.kolon.trim() : null,
        ozet: form.ozet,
        op: form.op,
        esik: Number(form.esik) || 0,
        alicilar: form.alicilar,
        zamanlama: form.zamanlama.trim() === '' ? null : form.zamanlama.trim(),
        aktif: form.aktif,
        surekliBildir: form.surekliBildir,
        parametreDegerleri: raporYolu && raporParametreleri.length > 0 ? form.parametreDegerleri : null,
      };
      if (duzenlenenId) await updateAlert(duzenlenenId, girdi);
      else await addAlert(girdi);
      setAcik(false);
      await yenile();
      kadiBasarili(duzenlenenId ? 'Uyarıyı güncelledim.' : 'Uyarıyı kurdum — nöbetteyim.');
    } catch (h) {
      setFormHata((h as Error).message); kadiHata();
    } finally {
      kadiBitir();
      setKaydediliyor(false);
    }
  }

  async function sil(u: AlertSummary) {
    if (!(await askConfirm(`"${u.ad}" uyarısı silinsin mi?`, { tehlikeli: true, onayYazisi: 'Sil' }))) return;
    const kadiBitir = kadiYukleniyor('Uyarıyı siliyorum…');
    try {
      await deleteAlert(u.id);
      await yenile();
    } catch (h) { setHata((h as Error).message); kadiHata(); }
    finally { kadiBitir(); }
  }

  async function dene(u: AlertSummary) {
    setDenenenId(u.id);
    // S28c: buton kilitlenirken Kadi ortada sahnede — 'bir şey oluyor' hissi Kadi'den
    const kadiBitir = kadiYukleniyor('Uyarıyı deniyorum…', 'kos');
    setHata(null);
    setBilgi(null);
    try {
      const s = await tryAlert(u.id);
      const olcum = s.deger === null ? 'ölçülemedi (veri yok)' : String(s.deger);
      // Deneme E-POSTA ATMAZ; mesaj bunu açıkça söyler ki kullanıcı "gitti mi?" diye kalmasın.
      setBilgi(`"${u.ad}": ölçülen değer ${olcum} · ${s.tetiklendi ? 'EŞİK AŞILDI' : 'eşik aşılmadı'}`
        + ` · ${s.satirSayisi.toLocaleString('tr-TR')} satır. Deneme olduğu için e-posta gönderilmedi.`);
      await yenile();
    } catch (h) {
      setHata(`"${u.ad}" denenemedi: ${(h as Error).message}`); kadiHata();
    } finally {
      kadiBitir();
      setDenenenId(null);
    }
  }

  if (liste === null) return <LoadingCurtain />;

  return (
    <Box>
      <PageHeader ikon={<NotificationsActiveIcon />} baslik="Uyarılar"
        aciklama="Eşik uyarısı: izlenen raporun ya da metriğin değeri sınırı aşınca e-posta gelir. Yalnız DURUM DEĞİŞİNCE bildirilir — eşik aşılı kaldığı sürece tekrar tekrar yazılmaz."
        rozetler={[{ sayi: liste.length, ad: 'uyarı' }, { sayi: liste.filter((u) => u.durum === 'Tetiklendi').length, ad: 'tetiklenmiş' }]}>
        <Button data-kadi-hedef="yeni-uyari" variant="contained" startIcon={<AddIcon />} onClick={yeniAc} sx={bandButtonWhite}>Yeni Uyarı</Button>
      </PageHeader>

      <Box sx={{ px: 3, py: 2.5 }}>
        {hata && <MuiAlert severity="error" sx={{ mb: 2, borderRadius: boxRadius }} onClose={() => setHata(null)}>{hata}</MuiAlert>}
        {bilgi && <MuiAlert severity="info" sx={{ mb: 2, borderRadius: boxRadius }} onClose={() => setBilgi(null)}>{bilgi}</MuiAlert>}

        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1.5 }}>
          <Typography sx={{ fontSize: 13, color: 'text.secondary' }}>
            {liste.length} uyarı
          </Typography>
          <IconButton size="small" onClick={() => void yenile()} title="Yenile"><RefreshIcon fontSize="small" /></IconButton>
        </Box>

        {liste.length === 0 && (
          <Paper variant="outlined" sx={{ p: 4, borderRadius: boxRadius, textAlign: 'center' }}>
            <Typography sx={{ fontWeight: 700, mb: 0.5 }}>Henüz uyarı yok</Typography>
            <Typography sx={{ fontSize: 13, color: 'text.secondary', mb: 2 }}>
              Örnek: “Açık talep sayısı 500'ü geçerse haber ver” ya da “hatalı kayıt varsa haber ver”
              (Satır sayısı &gt; 0). Uyarı bir rapora ya da bir metriğe bakar — “Toplam Ciro düşerse
              haber ver” için metrik izlemek yeter.
            </Typography>
            <Button variant="contained" startIcon={<AddIcon />} onClick={yeniAc}>Yeni Uyarı</Button>
          </Paper>
        )}

        <Box sx={{ display: 'grid', gap: 1.25 }}>
          {liste.map((u) => (
            <Paper key={u.id} variant="outlined" sx={{ p: 1.75, borderRadius: boxRadius, display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
              <Box sx={{ minWidth: 220, flexGrow: 1 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                  <Typography sx={{ fontWeight: 700 }}>{u.ad}</Typography>
                  <DurumCip u={u} />
                  {!u.aktif && <Chip size="small" label="Pasif" sx={{ fontSize: 11 }} />}
                  {!u.benimMi && <Chip size="small" variant="outlined" label="Başkasının" sx={{ fontSize: 11 }} />}
                </Box>
                <Typography sx={{ fontSize: 12.5, color: 'text.secondary', mt: 0.3 }}>
                  {u.metrikId
                    ? `Metrik: ${u.metrikAd ?? '(metrik silinmiş)'}`
                    : (u.raporAd ?? '(rapor silinmiş)')} · <b>{kosulMetni(u)}</b>
                  {u.zamanlama ? ` · ${u.zamanlama}` : ' · yalnız elle'}
                  {u.aliciSayisi > 0 ? ` · ${u.aliciSayisi} alıcı` : ' · alıcı yok'}
                </Typography>
                {u.sonKontrolTarihi && (
                  <Typography sx={{ fontSize: 12, color: 'text.disabled', mt: 0.2 }}>
                    Son ölçüm: {u.sonDeger ?? '—'} ({new Date(u.sonKontrolTarihi).toLocaleString('tr-TR')})
                  </Typography>
                )}
              </Box>
              <Box sx={{ display: 'flex', gap: 0.5 }}>
                <Button size="small" startIcon={denenenId === u.id ? <CircularProgress size={14} /> : <PlayArrowIcon />}
                  disabled={denenenId !== null} onClick={() => void dene(u)}
                  title="Şimdi ölç — e-posta göndermez">Şimdi Dene</Button>
                <IconButton size="small" onClick={() => void duzenleAc(u.id)} title="Düzenle"><EditOutlinedIcon fontSize="small" /></IconButton>
                <IconButton size="small" onClick={() => void sil(u)} title="Sil"><DeleteOutlineIcon fontSize="small" /></IconButton>
              </Box>
            </Paper>
          ))}
        </Box>
      </Box>

      <Drawer anchor="right" open={acik} onClose={() => setAcik(false)}
        slotProps={{ paper: { sx: { width: { xs: '100%', sm: 460 }, p: 2.5 } } }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 2 }}>
          <Typography sx={{ fontWeight: 800 }}>{duzenlenenId ? 'Uyarıyı düzenle' : 'Yeni uyarı'}</Typography>
          <IconButton size="small" onClick={() => setAcik(false)}><CloseIcon fontSize="small" /></IconButton>
        </Box>

        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField size="small" label="Uyarı adı" value={form.ad} placeholder="ör. Açık talep sayısı yüksek"
            onChange={(e) => setForm((f) => ({ ...f, ad: e.target.value }))} />

          {/* S30: kaynak seçimi — rapor (kolon+özet ile) ya da metrik (zaten tek değer). */}
          <ToggleButtonGroup exclusive size="small" fullWidth value={form.kaynak}
            onChange={(_, v: 'rapor' | 'metrik' | null) => { if (v) setForm((f) => ({ ...f, kaynak: v })); }}>
            <ToggleButton value="rapor" sx={{ fontWeight: 700, textTransform: 'none' }}>Rapor izle</ToggleButton>
            <ToggleButton value="metrik" sx={{ fontWeight: 700, textTransform: 'none' }}>Metrik izle</ToggleButton>
          </ToggleButtonGroup>

          {form.kaynak === 'rapor' ? (
            <FormControl size="small">
              <InputLabel>İzlenecek rapor</InputLabel>
              <Select label="İzlenecek rapor" value={form.raporId}
                onChange={(e) => setForm((f) => ({ ...f, raporId: e.target.value }))}>
                {raporlar.map((r) => <MenuItem key={r.id} value={r.id}>{r.ad}</MenuItem>)}
              </Select>
            </FormControl>
          ) : (
            <Box>
              <FormControl size="small" fullWidth>
                <InputLabel>İzlenecek metrik</InputLabel>
                <Select label="İzlenecek metrik" value={form.metrikId}
                  onChange={(e) => setForm((f) => ({ ...f, metrikId: e.target.value }))}>
                  {metrikler.map((m) => <MenuItem key={m.id} value={m.id}>{m.ad}</MenuItem>)}
                </Select>
              </FormControl>
              <Typography sx={{ fontSize: 12, color: 'text.secondary', mt: 0.5 }}>
                Metrik tek, adlandırılmış bir değerdir — kolon/özet seçmek gerekmez. Ölçüm, metriğin
                güncel tanımıyla yapılır; tanım değişirse uyarı da yeni tanımı kullanır.
              </Typography>
            </Box>
          )}

          {form.kaynak === 'rapor' && (
            <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
              <FormControl size="small" sx={{ minWidth: 130 }}>
                <InputLabel>Neye bak</InputLabel>
                <Select label="Neye bak" value={form.ozet}
                  onChange={(e) => setForm((f) => ({ ...f, ozet: e.target.value as OzetTuru }))}>
                  {OZETLER.map((o) => <MenuItem key={o.deger} value={o.deger}>{o.etiket}</MenuItem>)}
                </Select>
              </FormControl>
              {kolonIster(form.ozet) && (
                <TextField size="small" label="Kolon" value={form.kolon} sx={{ flexGrow: 1 }}
                  placeholder="rapor çıktısındaki başlık"
                  onChange={(e) => setForm((f) => ({ ...f, kolon: e.target.value }))} />
              )}
            </Box>
          )}

          <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
            <FormControl size="small" sx={{ minWidth: 96 }}>
              <InputLabel>Koşul</InputLabel>
              <Select label="Koşul" value={form.op}
                onChange={(e) => setForm((f) => ({ ...f, op: e.target.value as UyariOp }))}>
                {OPLAR.map((o) => <MenuItem key={o.deger} value={o.deger}>{o.etiket}</MenuItem>)}
              </Select>
            </FormControl>
            <TextField size="small" label="Eşik" type="number" value={form.esik} sx={{ width: 140 }}
              onChange={(e) => setForm((f) => ({ ...f, esik: e.target.value }))} />
            <Typography sx={{ fontSize: 12.5, color: 'text.secondary' }}>
              {kosulMetni({
                ozet: form.ozet, kolon: form.kolon || null, op: form.op, esik: Number(form.esik) || 0,
                metrikAd: form.kaynak === 'metrik'
                  ? (metrikler.find((m) => m.id === form.metrikId)?.ad ?? '…') : null,
              })}
            </Typography>
          </Box>

          {form.kaynak === 'rapor' && raporParametreleri.length > 0 && (
            <Box>
              <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: 'text.secondary', mb: 0.8 }}>
                Rapor parametreleri — uyarı bu değerlerle ölçer
              </Typography>
              <ParametrePanel parametreler={raporParametreleri} degerler={form.parametreDegerleri}
                onChange={(d) => setForm((f) => ({ ...f, parametreDegerleri: d }))} />
            </Box>
          )}

          <Box>
            <Typography sx={{ fontSize: 12.5, fontWeight: 700, mb: 0.5 }}>Alıcılar</Typography>
            <Box sx={{ display: 'flex', gap: 0.7, flexWrap: 'wrap', mb: 1 }}>
              {form.alicilar.map((a) => (
                <Chip key={a} size="small" label={a}
                  onDelete={() => setForm((f) => ({ ...f, alicilar: f.alicilar.filter((x) => x !== a) }))} />
              ))}
            </Box>
            <Box sx={{ display: 'flex', gap: 1 }}>
              <TextField size="small" placeholder="ad@firma.com" value={yeniAlici} sx={{ flexGrow: 1 }}
                onChange={(e) => setYeniAlici(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); aliciEkle(); } }} />
              <Button size="small" onClick={aliciEkle} disabled={!epostaGecerli(yeniAlici)}>Ekle</Button>
            </Box>
          </Box>

          <Box>
            <Typography sx={{ fontSize: 12.5, fontWeight: 700, mb: 0.5 }}>Ne sıklıkta kontrol edilsin</Typography>
            <CronSecici deger={form.zamanlama} degisti={(v) => setForm((f) => ({ ...f, zamanlama: v }))} />
          </Box>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Switch checked={form.aktif} onChange={(e) => setForm((f) => ({ ...f, aktif: e.target.checked }))} />
            <Typography sx={{ fontSize: 13 }}>Aktif</Typography>
          </Box>

          <Box>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Switch checked={form.surekliBildir}
                onChange={(e) => setForm((f) => ({ ...f, surekliBildir: e.target.checked }))} />
              <Typography sx={{ fontSize: 13 }}>Eşik aşılı kaldıkça her kontrolde tekrar bildir</Typography>
            </Box>
            <Typography sx={{ fontSize: 12, color: 'text.secondary', ml: 5.5 }}>
              Kapalıyken yalnız durum değiştiğinde yazılır (sakin → tetiklendi ve geri). Açarsanız
              saat başı kontrolde bir haftalık aşım 168 e-posta demektir — açmadan önce düşünün.
            </Typography>
          </Box>

          {formHata && <MuiAlert severity='error' sx={{ borderRadius: boxRadius }} onClose={() => setFormHata(null)}>{formHata}</MuiAlert>}

          <Box sx={{ display: 'flex', gap: 1, mt: 1 }}>
            {/* Kaydet, sunucunun kabul edeceği koşulları YANSITIR: alıcısız uyarı kimseye haber
                veremez, kolon isteyen özet kolonsuz ölçemez. Buton açık bırakılıp sunucudan hata
                aldırmak, kullanıcıya nedenini geç söylemek olurdu. */}
            <Button variant="contained" onClick={() => void kaydet()}
              disabled={kaydediliyor || form.ad.trim() === '' || form.alicilar.length === 0
                || (form.kaynak === 'rapor'
                  ? form.raporId === '' || (kolonIster(form.ozet) && form.kolon.trim() === '')
                  : form.metrikId === '')}>Kaydet</Button>
            <Button onClick={() => setAcik(false)}>Vazgeç</Button>
          </Box>
        </Box>
      </Drawer>
      {confirmDialog}
    </Box>
  );
}
