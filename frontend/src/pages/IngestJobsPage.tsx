import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import Drawer from '@mui/material/Drawer';
import Switch from '@mui/material/Switch';
import { CronSecici } from '../components/CronSecici';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import Collapse from '@mui/material/Collapse';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import Chip from '@mui/material/Chip';
import Alert from '@mui/material/Alert';
import Tooltip from '@mui/material/Tooltip';
import CircularProgress from '@mui/material/CircularProgress';
import AddIcon from '@mui/icons-material/Add';
import CloudSyncIcon from '@mui/icons-material/CloudSync';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import CloseIcon from '@mui/icons-material/Close';
import RefreshIcon from '@mui/icons-material/Refresh';
import SearchIcon from '@mui/icons-material/Search';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import TableChartIcon from '@mui/icons-material/TableChart';
import RestartAltIcon from '@mui/icons-material/RestartAlt';
import TrendingUpIcon from '@mui/icons-material/TrendingUp';
import { PageHeader, bandButtonWhite } from '../components/PageHeader';
import { boxRadius } from '../theme';
import { LoadingCurtain } from '../components/Loading';
import { useConfirm } from '../components/ConfirmDialog';
import {
  getIngestJobs, getIngestJob, addIngestJob, updateIngestJob, deleteIngestJob, runIngestJob,
  type IngestJobSummary, type IngestTable, type SaveIngestJobInput,
} from '../api/ingest';
import { getConnections, type Connection } from '../api/connections';
import { searchSchemaTables, getSchemaColumns, type SchemaTableDto, type SchemaColumnDto } from '../api/dataMaps';
import { kadiYukleniyor, kadiHata, kadiBasarili } from '../kadi/kadiKanal';

const anahtar = (t: { sema: string; tablo: string }) => `${t.sema}.${t.tablo}`;

/* S44 — artımlı kolon adayları: değeri hep BÜYÜYEN kolon tipleri. rowversion güncellemeleri de
 * yakalar (her yazımda artar); Id/tarih yalnız yeni satırları (değişiklik tarihi kolonu varsa
 * güncellemeleri de). NULL olabilen kolon aday değildir — NULL satırlar dilime hiç girmez. */
const ARTIMLI_TIPLER = new Set([
  'timestamp', 'rowversion', 'int', 'bigint', 'smallint',
  'date', 'datetime', 'datetime2', 'smalldatetime', 'datetimeoffset',
]);
const artimliAday = (c: SchemaColumnDto) => !c.nullOlabilir && ARTIMLI_TIPLER.has(c.veriTipi.toLowerCase());

type Form = { ad: string; baglantiId: string; tablolar: IngestTable[]; zamanlama: string; aktif: boolean };
const bosForm: Form = { ad: '', baglantiId: '', tablolar: [], zamanlama: '', aktif: true };

function DurumCip({ j }: { j: IngestJobSummary }) {
  const stil = (bg: string, fg: string, dot: string, l: string) => (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.7, bgcolor: bg, color: fg, fontSize: 12, fontWeight: 700, borderRadius: 999, px: 1, py: '3px', whiteSpace: 'nowrap' }}>
      <Box component="span" sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: dot }} />{l}
    </Box>
  );
  if (!j.sonDurum) return stil('var(--kds-dolgu)', 'text.secondary', 'text.disabled', 'Hiç çalışmadı');
  // S45: iş ajana dağıtıldı, sonuç henüz gelmedi. Ajan ölürse durum burada kalır ve bir sonraki
  // cron vadesinde yeniden dağıtılır (kendi kendini onarır) — bu yüzden ayrı bir "takıldı" durumu yok.
  if (j.sonDurum === 'Kosuyor') return stil('var(--kds-tealdolgu)', 'secondary.main', 'secondary.main', 'Ajanda koşuyor…');
  if (j.sonDurum === 'Basarili') {
    const parca = [`${(j.sonSatirSayisi ?? 0).toLocaleString('tr-TR')} satır`];
    if (j.sonSenkronSaniye != null) parca.push(`${j.sonSenkronSaniye} sn`);
    return stil('var(--kds-yesildolgu)', 'success.main', 'success.main', `Başarılı · ${parca.join(' · ')}`);
  }
  return (
    <Tooltip title={j.sonHata ?? ''}>{stil('var(--kds-kirmizidolgu)', 'error.main', 'error.main', 'Hata')}</Tooltip>
  );
}

export function IngestJobsPage() {
  const { askConfirm, confirmDialog } = useConfirm();
  const [liste, setListe] = useState<IngestJobSummary[]>([]);
  const [baglantilar, setBaglantilar] = useState<Connection[]>([]);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [yuklemeHatasi, setYuklemeHatasi] = useState<string | null>(null);
  const [calisanId, setCalisanId] = useState<string | null>(null);

  const [acik, setAcik] = useState(false);
  const [duzenlenenId, setDuzenlenenId] = useState<string | null>(null);
  const [form, setForm] = useState<Form>(bosForm);
  const [kaydediyor, setKaydediyor] = useState(false);
  const [formHata, setFormHata] = useState<string | null>(null);

  // Tablo seçici durumu
  const [arama, setArama] = useState('');
  const [sonuclar, setSonuclar] = useState<SchemaTableDto[]>([]);
  const [araniyor, setAraniyor] = useState(false);
  const [acikTablo, setAcikTablo] = useState<string | null>(null);
  const [kolonKes, setKolonKes] = useState<Record<string, SchemaColumnDto[]>>({});

  async function refresh() {
    setYukleniyor(true);
    setYuklemeHatasi(null);
    try {
      const [isler, bags] = await Promise.all([getIngestJobs(), getConnections()]);
      setListe(isler);
      setBaglantilar(bags);
    } catch (h) {
      setYuklemeHatasi((h as Error).message); kadiHata();
    } finally {
      setYukleniyor(false);
    }
  }
  useEffect(() => { void refresh(); }, []);

  const iceAlimBaglantilari = baglantilar.filter((b) => b.mod === 'IceAlim');

  function openNew() {
    setForm({ ...bosForm, baglantiId: iceAlimBaglantilari[0]?.id ?? '' });
    setDuzenlenenId(null);
    setFormHata(null);
    setArama(''); setSonuclar([]); setAcikTablo(null);
    setAcik(true);
  }

  async function openEdit(j: IngestJobSummary) {
    setFormHata(null);
    setArama(''); setSonuclar([]); setAcikTablo(null);
    setDuzenlenenId(j.id);
    setAcik(true);
    const kadiBitir = kadiYukleniyor('İşi açıyorum…');
    try {
      const d = await getIngestJob(j.id);
      setForm({ ad: d.ad, baglantiId: d.baglantiId, tablolar: d.tablolar, zamanlama: d.zamanlama ?? '', aktif: d.aktif });
    } catch (h) {
      setFormHata((h as Error).message); kadiHata();
    } finally {
      kadiBitir();
    }
  }

  function close() { setAcik(false); setDuzenlenenId(null); }

  async function ara() {
    if (!form.baglantiId) return;
    setAraniyor(true);
    try {
      setSonuclar(await searchSchemaTables(form.baglantiId, arama));
    } catch (h) {
      setFormHata((h as Error).message); kadiHata();
    } finally {
      setAraniyor(false);
    }
  }

  function tabloEkle(t: SchemaTableDto) {
    if (form.tablolar.some((x) => anahtar(x) === `${t.sema}.${t.ad}`)) return;
    setForm((f) => ({ ...f, tablolar: [...f.tablolar, { sema: t.sema, tablo: t.ad, kolonlar: null }] }));
  }
  function tabloCikar(k: string) {
    setForm((f) => ({ ...f, tablolar: f.tablolar.filter((x) => anahtar(x) !== k) }));
  }

  async function kolonlariAc(t: IngestTable) {
    const k = anahtar(t);
    setAcikTablo((mevcut) => (mevcut === k ? null : k));
    if (!kolonKes[k]) {
      try {
        const kols = await getSchemaColumns(form.baglantiId, t.sema, t.tablo);
        setKolonKes((c) => ({ ...c, [k]: kols }));
      } catch { /* açılınca boş kalır; sessiz geç */ }
    }
  }

  function kolonToggle(t: IngestTable, hepsi: string[], kolonAd: string, secili: boolean) {
    const k = anahtar(t);
    setForm((f) => ({
      ...f,
      tablolar: f.tablolar.map((x) => {
        if (anahtar(x) !== k) return x;
        const mevcut = x.kolonlar ?? hepsi;
        const yeni = secili ? [...mevcut, kolonAd] : mevcut.filter((c) => c !== kolonAd);
        // S44: artımlı kolonun kendisi seçimden çıkarılırsa artımlılık da düşer — seçici boşa
        // döner (görünür geri bildirim); sunucu doğrulaması da aynı durumu reddediyor.
        const artimli = !secili && x.artimliKolon === kolonAd ? null : x.artimliKolon;
        // Hepsi seçili → null'a (tüm kolonlar) normalize et; hiçbiri seçili değilse yine liste kalsın.
        return { ...x, kolonlar: yeni.length === hepsi.length ? null : yeni, artimliKolon: artimli };
      }),
    }));
  }

  /** S44 — tablonun artımlı kolonunu seçer/temizler ('' = tam yükleme). */
  function artimliSec(t: IngestTable, kolonAd: string) {
    const k = anahtar(t);
    setForm((f) => ({
      ...f,
      tablolar: f.tablolar.map((x) => (anahtar(x) === k ? { ...x, artimliKolon: kolonAd || null } : x)),
    }));
  }

  async function save() {
    const kadiBitir = kadiYukleniyor('İçe alım işini kaydediyorum…');
    setKaydediyor(true);
    setFormHata(null);
    try {
      const govde: SaveIngestJobInput = {
        ad: form.ad, baglantiId: form.baglantiId, tablolar: form.tablolar,
        zamanlama: form.zamanlama.trim() ? form.zamanlama.trim() : null, aktif: form.aktif,
      };
      if (duzenlenenId) await updateIngestJob(duzenlenenId, govde);
      else await addIngestJob(govde);
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

  async function calistir(j: IngestJobSummary, tamYukleme = false) {
    // S44: Tam Yükle su seviyelerini sıfırlar ve her tabloyu baştan çeker — bilinçli, onaylı eylem
    // (artımlının taşımadığı SİLİNMİŞ satırlar böyle temizlenir).
    if (tamYukleme && !(await askConfirm(
      `"${j.ad}" TAM yüklensin mi? Artımlı ilerleme sıfırlanır ve tüm tablolar baştan çekilir.`,
      { onayYazisi: 'Tam Yükle' }))) return;
    setCalisanId(j.id);
    // S28c: buton kilitlenirken Kadi ortada sahnede — 'bir şey oluyor' hissi Kadi'den
    const kadiBitir = kadiYukleniyor('Aktarımı çalıştırıyorum…', 'kos');
    setYuklemeHatasi(null);
    try {
      const sonuc = await runIngestJob(j.id, tamYukleme);
      if (sonuc.ajanaDevredildi) {
        // S45: iş sunucuda koşmadı — kaynak müşteri ağında, ajan yoklamasında koşacak.
        kadiBasarili('İşi ajana devrettim — ajan bir sonraki yoklamasında koşacak (durumu bu karttan izle).');
      } else {
        // S44: artımlı seçildi ama tam koştuysa (ilk koşu / şema kayması / PK yok) kullanıcı görsün.
        kadiBasarili(`Bitti: ${sonuc.toplamSatir.toLocaleString('tr-TR')} satır · ${
          sonuc.artimliTabloSayisi > 0
            ? `${sonuc.artimliTabloSayisi}/${sonuc.tabloSayisi} tablo artımlı`
            : 'tam yükleme'}`);
      }
      await refresh();
    } catch (h) {
      setYuklemeHatasi(`"${j.ad}" çalıştırılamadı: ${(h as Error).message}`); kadiHata();
    } finally {
      kadiBitir();
      setCalisanId(null);
    }
  }

  async function sil(j: IngestJobSummary) {
    if (!(await askConfirm(`"${j.ad}" içe alım işi silinsin mi?`, { tehlikeli: true, onayYazisi: 'Sil' }))) return;
    const kadiBitir = kadiYukleniyor('İşi siliyorum…');
    try {
      await deleteIngestJob(j.id);
      await refresh();
    } catch (h) {
      setYuklemeHatasi((h as Error).message); kadiHata();
    } finally {
      kadiBitir();
    }
  }

  const gecerli = form.ad.trim() && form.baglantiId && form.tablolar.length > 0;
  const aktifSayisi = liste.filter((j) => j.aktif && j.zamanlama).length;

  return (
    <Box>
      <PageHeader ikon={<CloudSyncIcon />} baslik="İçe Alım İşleri"
        aciklama="Uzak kaynaktan tablolarını kendi depona çek (Import modu) — zamanla ya da elle çalıştır."
        rozetler={[{ sayi: liste.length, ad: 'iş' }, { sayi: aktifSayisi, ad: 'zamanlı' }]}>
        <Button variant="contained" startIcon={<AddIcon />} onClick={openNew} sx={bandButtonWhite}>Yeni İş</Button>
      </PageHeader>

      {yuklemeHatasi && (
        <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" size="small" startIcon={<RefreshIcon />} onClick={() => void refresh()}>Tekrar</Button>}>
          {yuklemeHatasi}
        </Alert>
      )}

      {yukleniyor ? (
        <LoadingCurtain />
      ) : (
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', lg: 'repeat(3, 1fr)' }, gap: 2 }}>
          {liste.map((j) => (
            <Paper key={j.id} variant="outlined" sx={{ borderRadius: boxRadius, overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
              <Box sx={{ height: 5, bgcolor: j.aktif ? '#0e6f7a' : '#b6c0c9' }} />
              <Box sx={{ p: 2, display: 'flex', flexDirection: 'column', gap: 1, flexGrow: 1 }}>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 1 }}>
                  <Box sx={{ minWidth: 0 }}>
                    <Typography sx={{ fontWeight: 700, fontSize: 14.5 }} color="primary" noWrap>{j.ad}</Typography>
                    <Typography sx={{ fontSize: 12 }} color="text.secondary" noWrap>
                      {j.baglantiAd ?? '—'} · {j.tabloSayisi} tablo
                    </Typography>
                  </Box>
                  <DurumCip j={j} />
                </Box>
                <Box sx={{ display: 'flex', gap: 0.7, flexWrap: 'wrap' }}>
                  <Chip size="small" variant="outlined" label={j.zamanlama ? `⏱ ${j.zamanlama}` : 'Elle'} />
                  <Chip size="small" variant="outlined" color={j.aktif ? 'success' : 'default'} label={j.aktif ? 'Aktif' : 'Pasif'} />
                  {j.artimliTabloSayisi > 0 && (
                    <Tooltip title="Bu tablolar her koşuda baştan değil, yalnız yeni/değişen satırlarıyla çekilir.">
                      <Chip size="small" variant="outlined" color="secondary"
                        label={`Artımlı: ${j.artimliTabloSayisi}/${j.tabloSayisi}`} />
                    </Tooltip>
                  )}
                  {j.sonSenkronTarihi && (
                    <Chip size="small" variant="outlined" label={`Son: ${new Date(j.sonSenkronTarihi).toLocaleString('tr-TR')}`} />
                  )}
                </Box>
                <Box sx={{ flexGrow: 1 }} />
                <Box sx={{ display: 'flex', gap: 1, pt: 1, borderTop: '1px solid', borderColor: 'divider' }}>
                  <Button size="small" variant="contained" fullWidth disabled={calisanId === j.id} onClick={() => void calistir(j)}
                    startIcon={calisanId === j.id ? <CircularProgress size={14} color="inherit" /> : <PlayArrowIcon />}>
                    Şimdi Çalıştır
                  </Button>
                  {/* S44: yalnız artımlı iş için anlamlı — silinen satırları temizlemek için baştan çeker. */}
                  {j.artimliTabloSayisi > 0 && (
                    <Tooltip title="Tam Yükle: artımlı ilerlemeyi sıfırlar, tüm tabloları baştan çeker (silinen satırlar böyle temizlenir).">
                      <span>
                        <IconButton size="small" aria-label="Tam Yükle" disabled={calisanId === j.id}
                          onClick={() => void calistir(j, true)}>
                          <RestartAltIcon fontSize="small" />
                        </IconButton>
                      </span>
                    </Tooltip>
                  )}
                  <Tooltip title="Düzenle"><IconButton size="small" aria-label="Düzenle" onClick={() => void openEdit(j)}><EditOutlinedIcon fontSize="small" /></IconButton></Tooltip>
                  <Tooltip title="Sil"><IconButton size="small" aria-label="Sil" onClick={() => void sil(j)}><DeleteOutlineIcon fontSize="small" /></IconButton></Tooltip>
                </Box>
              </Box>
            </Paper>
          ))}
          <Paper variant="outlined" onClick={openNew}
            sx={{ borderRadius: boxRadius, borderStyle: 'dashed', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 1, minHeight: 168, cursor: 'pointer', color: 'secondary.main', '&:hover': { bgcolor: 'action.hover' } }}>
            <Box sx={{ width: 44, height: 44, borderRadius: '50%', bgcolor: 'var(--kds-tealdolgu)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}><AddIcon /></Box>
            <Typography sx={{ fontWeight: 700, fontSize: 14 }}>Yeni İş</Typography>
          </Paper>
        </Box>
      )}

      {/* Editör → sağ çekmece (yeni + düzenle) */}
      <Drawer anchor="right" open={acik} onClose={close} slotProps={{ paper: { sx: { width: { xs: '92%', sm: 560 } } } }}>
        <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', p: 2, borderBottom: '1px solid', borderColor: 'divider' }}>
            <Typography sx={{ fontWeight: 700 }} color="primary">{duzenlenenId ? 'İşi Düzenle' : 'Yeni İçe Alım İşi'}</Typography>
            <IconButton size="small" onClick={close}><CloseIcon fontSize="small" /></IconButton>
          </Box>

          <Box sx={{ p: 2, flexGrow: 1, overflowY: 'auto', bgcolor: 'background.paper', display: 'flex', flexDirection: 'column', gap: 2 }}>
            <TextField label="İş adı" size="small" fullWidth value={form.ad}
              onChange={(e) => setForm((f) => ({ ...f, ad: e.target.value }))} placeholder="ör. QMS Gecelik Senkron" />

            <FormControl size="small" fullWidth>
              <InputLabel>Kaynak bağlantı</InputLabel>
              <Select label="Kaynak bağlantı" value={form.baglantiId}
                onChange={(e) => { setForm((f) => ({ ...f, baglantiId: e.target.value, tablolar: [] })); setSonuclar([]); }}>
                {iceAlimBaglantilari.map((b) => (
                  <MenuItem key={b.id} value={b.id}>{b.ad} · {b.sunucu}/{b.veritabani}</MenuItem>
                ))}
              </Select>
            </FormControl>
            {iceAlimBaglantilari.length === 0 && (
              <Alert severity="info" sx={{ fontSize: 12.5 }}>
                Önce <b>İçe Alım</b> modlu bir bağlantı ekleyin (Bağlantılar ekranı). Kaynak yalnız İçe Alım modlu bağlantılardan seçilir.
              </Alert>
            )}

            {/* Tablo seçici */}
            <Box>
              <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: 'text.secondary', mb: 0.8 }}>
                Çekilecek tablolar {form.tablolar.length > 0 && `(${form.tablolar.length})`}
              </Typography>
              <Box sx={{ display: 'flex', gap: 1, mb: 1 }}>
                <TextField size="small" fullWidth placeholder="Tablo ara (ör. Kisi)…" value={arama} disabled={!form.baglantiId}
                  onChange={(e) => setArama(e.target.value)}
                  onKeyDown={(e) => { if (e.key === 'Enter') void ara(); }} />
                <Button variant="outlined" onClick={() => void ara()} disabled={!form.baglantiId || araniyor}
                  startIcon={araniyor ? <CircularProgress size={14} /> : <SearchIcon />}>Ara</Button>
              </Box>

              {sonuclar.length > 0 && (
                <Paper variant="outlined" sx={{ borderRadius: boxRadius, maxHeight: 168, overflowY: 'auto', mb: 1 }}>
                  {sonuclar.map((t) => {
                    const eklendi = form.tablolar.some((x) => anahtar(x) === `${t.sema}.${t.ad}`);
                    return (
                      <Box key={`${t.sema}.${t.ad}`} onClick={() => tabloEkle(t)}
                        sx={{ display: 'flex', alignItems: 'center', gap: 1, px: 1.5, py: 0.8, cursor: eklendi ? 'default' : 'pointer', opacity: eklendi ? 0.5 : 1, '&:hover': { bgcolor: eklendi ? undefined : 'action.hover' } }}>
                        <TableChartIcon fontSize="small" color="disabled" />
                        <Typography sx={{ fontSize: 13, flexGrow: 1 }} noWrap>{t.sema}.{t.ad}</Typography>
                        <Typography sx={{ fontSize: 11.5 }} color="text.disabled">{t.kolonSayisi} kolon</Typography>
                        {eklendi ? <Chip size="small" label="Eklendi" /> : <AddIcon fontSize="small" color="action" />}
                      </Box>
                    );
                  })}
                </Paper>
              )}

              {/* Seçilenler */}
              {form.tablolar.length === 0 ? (
                <Typography sx={{ fontSize: 12.5 }} color="text.disabled">Henüz tablo seçilmedi — arayıp ekleyin.</Typography>
              ) : (
                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.8 }}>
                  {form.tablolar.map((t) => {
                    const k = anahtar(t);
                    const hepsi = (kolonKes[k] ?? []).map((c) => c.ad);
                    return (
                      <Paper key={k} variant="outlined" sx={{ borderRadius: boxRadius, overflow: 'hidden' }}>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, px: 1.5, py: 0.8 }}>
                          <TableChartIcon fontSize="small" color="secondary" />
                          <Typography sx={{ fontSize: 13, fontWeight: 600, flexGrow: 1 }} noWrap>{t.sema}.{t.tablo}</Typography>
                          {t.artimliKolon && (
                            <Tooltip title={`Artımlı: her koşuda yalnız ${t.artimliKolon} kolonu son değerden büyük satırlar çekilir.`}>
                              <Chip size="small" color="secondary" icon={<TrendingUpIcon />} label={t.artimliKolon} />
                            </Tooltip>
                          )}
                          <Chip size="small" variant="outlined"
                            label={t.kolonlar == null ? 'Tüm kolonlar' : `${t.kolonlar.length} kolon`} />
                          <Button size="small" onClick={() => void kolonlariAc(t)} endIcon={<ExpandMoreIcon sx={{ transform: acikTablo === k ? 'rotate(180deg)' : 'none', transition: '.2s' }} />}>
                            Kolonlar
                          </Button>
                          <Tooltip title="Kaldır"><IconButton size="small" onClick={() => tabloCikar(k)}><DeleteOutlineIcon fontSize="small" /></IconButton></Tooltip>
                        </Box>
                        <Collapse in={acikTablo === k}>
                          {/* S44 — artımlı yükleme seçimi. PK'sız tabloda kapalı: delta PK üzerinden
                              birleştirilir; PK olmadan koşucu zaten her koşuda tam yüklemeye düşer. */}
                          {(() => {
                            const kolonlar = kolonKes[k] ?? [];
                            if (kolonlar.length === 0) return null;
                            const pkVar = kolonlar.some((c) => c.birincilAnahtarMi);
                            const secili = new Set(t.kolonlar ?? kolonlar.map((c) => c.ad));
                            const adaylar = kolonlar.filter((c) => artimliAday(c) && secili.has(c.ad));
                            return (
                              <Box sx={{ px: 1.5, py: 1, borderTop: '1px solid', borderColor: 'divider', display: 'flex', alignItems: 'center', gap: 1 }}>
                                <FormControl size="small" sx={{ minWidth: 210 }} disabled={!pkVar || adaylar.length === 0}>
                                  <InputLabel>Artımlı kolon</InputLabel>
                                  <Select label="Artımlı kolon" value={t.artimliKolon ?? ''}
                                    onChange={(e) => artimliSec(t, e.target.value)}>
                                    <MenuItem value=""><em>Yok — her koşuda tam yükle</em></MenuItem>
                                    {adaylar.map((c) => (
                                      <MenuItem key={c.ad} value={c.ad}>{c.ad} · {c.veriTipi}</MenuItem>
                                    ))}
                                  </Select>
                                </FormControl>
                                <Typography sx={{ fontSize: 11.5, flexGrow: 1 }} color="text.disabled">
                                  {!pkVar
                                    ? 'Bu tabloda birincil anahtar yok — artımlı yükleme yapılamaz (birleştirme PK ister).'
                                    : adaylar.length === 0
                                      ? 'Uygun kolon yok — hep artan, NULL olmayan bir kolon gerekir (rowversion / Id / tarih).'
                                      : t.artimliKolon
                                        ? 'Her koşuda yalnız yeni/değişen satırlar çekilir; silinenler taşınmaz (Tam Yükle temizler).'
                                        : 'Seçersen tablo her koşuda baştan değil, yalnız yeni satırlarıyla çekilir.'}
                                </Typography>
                              </Box>
                            );
                          })()}
                          <Box sx={{ px: 1.5, pb: 1, borderTop: '1px solid', borderColor: 'divider', maxHeight: 180, overflowY: 'auto', bgcolor: 'background.paper' }}>
                            {hepsi.length === 0 ? (
                              <Typography sx={{ fontSize: 12, py: 1 }} color="text.disabled">Kolonlar yükleniyor…</Typography>
                            ) : (
                              <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', columnGap: 1 }}>
                                {hepsi.map((ad) => (
                                  <FormControlLabel key={ad} sx={{ m: 0, minWidth: 0 }}
                                    control={<Checkbox size="small" checked={t.kolonlar == null || t.kolonlar.includes(ad)}
                                      onChange={(e) => kolonToggle(t, hepsi, ad, e.target.checked)} />}
                                    label={<Typography sx={{ fontSize: 12.5 }} noWrap>{ad}</Typography>} />
                                ))}
                              </Box>
                            )}
                          </Box>
                        </Collapse>
                      </Paper>
                    );
                  })}
                </Box>
              )}
            </Box>

            {/* Zamanlama — M22: çıplak cron kutusu yerine anlaşılır seçici + Türkçe özet. */}
            <Box>
              <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: 'text.secondary', mb: 0.8 }}>Zamanlama</Typography>
              <CronSecici deger={form.zamanlama} degisti={(z) => setForm((f) => ({ ...f, zamanlama: z }))} />
            </Box>

            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Switch checked={form.aktif} onChange={(e) => setForm((f) => ({ ...f, aktif: e.target.checked }))} />
              <Typography sx={{ fontSize: 13.5 }}>Aktif (zamanlayıcı bu işi çalıştırır)</Typography>
            </Box>

            {formHata && <Alert severity="error" sx={{ py: 0.2 }}>{formHata}</Alert>}
          </Box>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, p: 2, borderTop: '1px solid', borderColor: 'divider' }}>
            <Typography sx={{ fontSize: 11.5, flexGrow: 1 }} color="text.disabled">
              Kaydedince depo şeması seçimden otomatik yaratılır.
            </Typography>
            <Button onClick={close} color="inherit">İptal</Button>
            <Button variant="contained" onClick={() => void save()} disabled={!gecerli || kaydediyor}
              startIcon={kaydediyor ? <CircularProgress size={15} color="inherit" /> : undefined}>Kaydet</Button>
          </Box>
        </Box>
      </Drawer>

      {confirmDialog}
    </Box>
  );
}
