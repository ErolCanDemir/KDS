import { useEffect, useRef, useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import Drawer from '@mui/material/Drawer';
import Switch from '@mui/material/Switch';
import Tooltip from '@mui/material/Tooltip';
import Popover from '@mui/material/Popover';
import InputAdornment from '@mui/material/InputAdornment';
import HelpOutlineIcon from '@mui/icons-material/HelpOutlineOutlined';
import SearchIcon from '@mui/icons-material/Search';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import Chip from '@mui/material/Chip';
import MuiAlert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import AddIcon from '@mui/icons-material/Add';
import FunctionsIcon from '@mui/icons-material/Functions';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import CloseIcon from '@mui/icons-material/Close';
import RefreshIcon from '@mui/icons-material/Refresh';
import HistoryIcon from '@mui/icons-material/History';
import FileDownloadOutlinedIcon from '@mui/icons-material/FileDownloadOutlined';
import FileUploadOutlinedIcon from '@mui/icons-material/FileUploadOutlined';
import RestoreIcon from '@mui/icons-material/Restore';
import { PageHeader, bandButton, bandButtonWhite } from '../components/PageHeader';
import { boxRadius, smallRadius } from '../theme';
import { SABLONLAR, alanEkle, sablonYerlestir, type EklemeSonucu } from '../components/metrikFormul';
import { LoadingCurtain } from '../components/Loading';
import { useConfirm } from '../components/ConfirmDialog';
import { useSession } from '../identity/sessionContext';
import { Roles } from '../identity/roles';
import {
  getMetrics, addMetric, updateMetric, deleteMetric, getMetricUsage, measureMetric, metrikDegerYaz,
  getMetricVersions, restoreMetricVersion, exportMetrics, importMetrics, exportMetricsOsi, importMetricsOsi,
  getMetricFunctions, type MetricFunctionGroup,
  type MetricSummary, type SaveMetricInput, type MetrikBicimi,
  type MetricVersionSummary, type MetricExportItem, type MetricImportItemResult,
} from '../api/metrics';
import { getDataMaps, getDataMap, type DataMapSummary, type DataMapDetail } from '../api/dataMaps';
import { kadiYukleniyor, kadiHata, kadiBasarili } from '../kadi/kadiKanal';

const BICIMLER: { deger: MetrikBicimi; etiket: string }[] = [
  { deger: 'Sayi', etiket: '# Sayı' },
  { deger: 'Para', etiket: '₺ Para' },
  { deger: 'Yuzde', etiket: '% Yüzde' },
];

type Form = { ad: string; veriHaritasiId: string; ifade: string; bicim: MetrikBicimi; aciklama: string; aktif: boolean };
const bosForm: Form = { ad: '', veriHaritasiId: '', ifade: '', bicim: 'Sayi', aciklama: '', aktif: true };

/** Ölçülen değerler (id → sonuç) — sayfa açık kaldıkça hatırlanır, otomatik ölçüm YOK. */
type Olcumler = Record<string, { deger: number | null; sureMs: number }>;

export default function MetricsPage() {
  const oturum = useSession();
  // Yazma yalnız admin+geliştirici (sunucu da zorlar); müşteri kataloğu okur, Ölç'ebilir.
  const yazabilir = oturum.roller.includes(Roles.Admin) || oturum.roller.includes(Roles.Gelistirici);

  const [liste, setListe] = useState<MetricSummary[] | null>(null);
  const [haritalar, setHaritalar] = useState<DataMapSummary[]>([]);
  const [olcumler, setOlcumler] = useState<Olcumler>({});
  const [olculenId, setOlculenId] = useState<string | null>(null);
  const [acik, setAcik] = useState(false);
  const [duzenlenenId, setDuzenlenenId] = useState<string | null>(null);
  const [form, setForm] = useState<Form>(bosForm);
  const [hata, setHata] = useState<string | null>(null);
  const [formHata, setFormHata] = useState<string | null>(null);
  const [kaydediliyor, setKaydediliyor] = useState(false);
  // S30: sürüm geçmişi
  const [gecmisMetrik, setGecmisMetrik] = useState<MetricSummary | null>(null);
  const [gecmis, setGecmis] = useState<MetricVersionSummary[] | null>(null);
  const [donulenNo, setDonulenNo] = useState<number | null>(null);
  // S30: içe alım (dosya → önizleme → strateji → sonuç)
  const dosyaRef = useRef<HTMLInputElement>(null);
  const [iceOgeler, setIceOgeler] = useState<MetricExportItem[] | null>(null);
  // S46: OSI YAML içe alımı — aynı diyalog/sonuç akışı, kaynak dosya biçimi farklı.
  const [iceOsiYaml, setIceOsiYaml] = useState<string | null>(null);
  const [iceGuncelle, setIceGuncelle] = useState(false);
  const [iceSonuclar, setIceSonuclar] = useState<MetricImportItemResult[] | null>(null);
  const [iceCalisiyor, setIceCalisiyor] = useState(false);
  const { askConfirm, confirmDialog } = useConfirm();
  /* S47 — formül yardımı: alan tıkla-ekle (harita detayı önbellekli), şablon çipleri ve
   * fonksiyon ipucu (içerik backend'deki bekçinin yanından — tek kaynak). */
  const formulRef = useRef<HTMLTextAreaElement>(null);
  const [haritaDetaylari, setHaritaDetaylari] = useState<Record<string, DataMapDetail>>({});
  const [alanArama, setAlanArama] = useState('');
  const [fonksiyonlar, setFonksiyonlar] = useState<MetricFunctionGroup[]>([]);
  const [ipucuHedef, setIpucuHedef] = useState<HTMLElement | null>(null);

  const yenile = async () => {
    try {
      setListe(await getMetrics());
    } catch (h) {
      setHata((h as Error).message); kadiHata();
    }
  };

  useEffect(() => {
    void yenile();
    getDataMaps().then(setHaritalar).catch(() => setHaritalar([]));
    getMetricFunctions().then(setFonksiyonlar).catch(() => setFonksiyonlar([]));
  }, []);

  // S47: seçili haritanın tablo/kolonları alan listesine — bir kez çekilir, önbellekte kalır.
  useEffect(() => {
    const id = form.veriHaritasiId;
    if (!id || haritaDetaylari[id]) return;
    getDataMap(id)
      .then((d) => setHaritaDetaylari((h) => ({ ...h, [id]: d })))
      .catch(() => { /* liste yüklenemezse elle yazım yolu zaten açık */ });
    // haritaDetaylari önbellek — bağımlılığa girerse her yüklemede döngü kurar.
    // oxlint-disable-next-line react-hooks/exhaustive-deps
  }, [form.veriHaritasiId]);

  /** Saf mantığın sonucunu alana uygular: metni yaz, seçimi geri ver, odağı alana getir. */
  function formuleUygula(sonuc: EklemeSonucu) {
    setForm((f) => ({ ...f, ifade: sonuc.metin }));
    requestAnimationFrame(() => {
      const el = formulRef.current;
      if (!el) return;
      el.focus();
      el.setSelectionRange(sonuc.secimBas, sonuc.secimSon);
    });
  }

  function imlec(): { bas: number; son: number } {
    const el = formulRef.current;
    return { bas: el?.selectionStart ?? form.ifade.length, son: el?.selectionEnd ?? form.ifade.length };
  }

  function yeniAc() {
    setFormHata(null);
    setDuzenlenenId(null);
    setForm(bosForm);
    setAcik(true);
  }

  function duzenleAc(m: MetricSummary) {
    setFormHata(null);
    setDuzenlenenId(m.id);
    setForm({
      ad: m.ad, veriHaritasiId: m.veriHaritasiId, ifade: m.ifade,
      bicim: m.bicim, aciklama: m.aciklama ?? '', aktif: m.aktif,
    });
    setAcik(true);
  }

  async function kaydet() {
    setKaydediliyor(true);
    setFormHata(null);
    const kadiBitir = kadiYukleniyor('Metriği kaydediyorum…');
    try {
      const girdi: SaveMetricInput = {
        ad: form.ad.trim(), veriHaritasiId: form.veriHaritasiId, ifade: form.ifade.trim(),
        bicim: form.bicim, aciklama: form.aciklama.trim() === '' ? null : form.aciklama.trim(), aktif: form.aktif,
      };
      if (duzenlenenId) await updateMetric(duzenlenenId, girdi);
      else await addMetric(girdi);
      setAcik(false);
      await yenile();
      kadiBasarili(duzenlenenId ? 'Metriği güncelledim.' : 'Metriği kaydettim — katalogda.');
    } catch (h) {
      // Formül reddi dahil (üreticinin "mevcut kolonlar: ..." mesajı) — çekmece İÇİNDE görünür.
      setFormHata((h as Error).message); kadiHata();
    } finally {
      kadiBitir();
      setKaydediliyor(false);
    }
  }

  async function sil(m: MetricSummary) {
    // Kullanım sayısı onayda söylenir: silmek raporları AÇIK hataya düşürür (sessiz kırılma yok).
    const kullanan = m.kullananRaporSayisi > 0 ? await getMetricUsage(m.id).catch(() => []) : [];
    const ek = kullanan.length > 0
      ? ` DİKKAT: ${kullanan.length} rapor bu metriği kullanıyor (${kullanan.slice(0, 4).join(', ')}${kullanan.length > 4 ? '…' : ''}) — silinirse o raporlar açık hata verir.`
      : '';
    if (!(await askConfirm(`"${m.ad}" metriği silinsin mi?${ek}`, { tehlikeli: true, onayYazisi: 'Sil' }))) return;
    const kadiBitir = kadiYukleniyor('Metriği siliyorum…');
    try {
      await deleteMetric(m.id);
      await yenile();
      kadiBasarili('Sildim.');
    } catch (h) { setHata((h as Error).message); kadiHata(); }
    finally { kadiBitir(); }
  }

  async function olc(m: MetricSummary) {
    setOlculenId(m.id);
    // S28c: buton kilitlenirken Kadi ortada sahnede — 'bir şey oluyor' hissi Kadi'den
    const kadiBitir = kadiYukleniyor('Metriği ölçüyorum…', 'kos');
    setHata(null);
    try {
      const s = await measureMetric(m.id);
      setOlcumler((o) => ({ ...o, [m.id]: { deger: s.deger, sureMs: s.sureMs } }));
    } catch (h) {
      setHata(`"${m.ad}" ölçülemedi: ${(h as Error).message}`); kadiHata();
    } finally {
      kadiBitir();
      setOlculenId(null);
    }
  }

  // ——— S30: sürüm geçmişi ———
  async function gecmisAc(m: MetricSummary) {
    setGecmisMetrik(m);
    setGecmis(null);
    const kadiBitir = kadiYukleniyor('Sürüm geçmişini getiriyorum…');
    try {
      setGecmis(await getMetricVersions(m.id));
    } catch (h) {
      setGecmisMetrik(null);
      setHata((h as Error).message); kadiHata();
    } finally {
      kadiBitir();
    }
  }

  async function surumeDon(no: number) {
    if (!gecmisMetrik) return;
    if (!(await askConfirm(`"${gecmisMetrik.ad}" ${no}. sürümüne dönsün mü? Bu metriği kullanan TÜM raporlar bir sonraki koşumda o tanımı kullanır.`, { onayYazisi: 'Sürüme dön' }))) return;
    setDonulenNo(no);
    const kadiBitir = kadiYukleniyor('Eski tanıma dönüyorum…');
    try {
      await restoreMetricVersion(gecmisMetrik.id, no);
      setGecmisMetrik(null);
      await yenile();
      kadiBasarili('Döndüm — tanım eski hâline geldi.');
    } catch (h) {
      // Eski tanımdaki kolon bugün silinmiş olabilir — üreticinin reddi diyalogda görünsün.
      setHata((h as Error).message); kadiHata();
    } finally {
      kadiBitir();
      setDonulenNo(null);
    }
  }

  // ——— S30: dışa / içe aktarma ———
  function indir(icerik: string, ad: string, tip: string) {
    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([icerik], { type: tip }));
    a.download = ad;
    a.click();
    URL.revokeObjectURL(a.href);
  }

  /** S46 — OSI (Apache Ossie) YAML: satıcıdan bağımsız semantik değişim biçimi. */
  async function disaAktarOsi() {
    const kadiBitir = kadiYukleniyor('OSI dosyasını hazırlıyorum…');
    try {
      indir(await exportMetricsOsi(), 'kds-semantik.osi.yaml', 'application/yaml');
      kadiBasarili('Hazır — semantik katman OSI biçiminde indirildi.');
    } catch (h) {
      setHata((h as Error).message); kadiHata();
    } finally {
      kadiBitir();
    }
  }

  async function disaAktar() {
    const kadiBitir = kadiYukleniyor('Metrikleri dışa aktarıyorum…');
    try {
      const ogeler = await exportMetrics();
      indir(JSON.stringify(ogeler, null, 2), 'kds-metrikler.json', 'application/json');
      kadiBasarili(`Hazır — ${ogeler.length} metrik dosyaya indirildi.`);
    } catch (h) {
      setHata((h as Error).message); kadiHata();
    } finally {
      kadiBitir();
    }
  }

  function dosyaSecildi(e: React.ChangeEvent<HTMLInputElement>) {
    const dosya = e.target.files?.[0];
    e.target.value = '';   // aynı dosya tekrar seçilebilsin
    if (!dosya) return;
    void dosya.text().then((metin) => {
      try {
        const ham: unknown = JSON.parse(metin);
        if (!Array.isArray(ham) || ham.length === 0) throw new Error('boş');
        setIceOgeler(ham as MetricExportItem[]);
        setIceGuncelle(false);
        setIceSonuclar(null);
      } catch {
        // S46: JSON değilse OSI YAML olabilir — kök anahtara bakılır; ayrıntılı doğrulama sunucuda
        // (öğe öğe sonuç döner). Uzantıya değil İÇERİĞE bakıyoruz: yanlış adlandırılmış dosya da çalışsın.
        if (metin.includes('semantic_model')) {
          setIceOsiYaml(metin);
          setIceGuncelle(false);
          setIceSonuclar(null);
        } else {
          setHata('Dosya okunamadı — metrik JSON dosyası ya da OSI (semantic_model içeren) YAML dosyası seçin.'); kadiHata();
        }
      }
    });
  }

  async function iceAl() {
    if (!iceOgeler && !iceOsiYaml) return;
    setIceCalisiyor(true);
    const kadiBitir = kadiYukleniyor('Metrikleri içe alıyorum…');
    try {
      const sonuclar = iceOsiYaml
        ? await importMetricsOsi(iceOsiYaml, iceGuncelle)
        : await importMetrics(iceOgeler!, iceGuncelle);
      setIceSonuclar(sonuclar);
      await yenile();
      const sorunlu = sonuclar.filter((s) => s.sonuc === 'Hata').length;
      if (sorunlu === 0) kadiBasarili('İçe aldım — sonuç listede.');
      else kadiHata(`${sorunlu} metrik alınamadı — nedenleri listede.`);
    } catch (h) {
      setHata((h as Error).message); kadiHata();
    } finally {
      kadiBitir();
      setIceCalisiyor(false);
    }
  }

  if (liste === null) return <LoadingCurtain />;

  return (
    <Box>
      <PageHeader ikon={<FunctionsIcon />} baslik="Metrikler"
        aciklama="Merkezi ölçüt kataloğu — bir kez tanımla, her rapor aynı tanımı kullansın."
        rozetler={[{ sayi: liste.length, ad: 'metrik' }]}>
        <Button variant="outlined" startIcon={<FileDownloadOutlinedIcon />} onClick={() => void disaAktar()}
          disabled={liste.length === 0} sx={bandButton}>Dışa Aktar</Button>
        <Tooltip title="OSI (Apache Ossie): semantik katmanı satıcıdan bağımsız standart YAML biçiminde indir — başka BI/AI araçları okuyabilir.">
          <span>
            <Button variant="outlined" startIcon={<FileDownloadOutlinedIcon />} onClick={() => void disaAktarOsi()}
              disabled={liste.length === 0} sx={bandButton}>OSI</Button>
          </span>
        </Tooltip>
        {yazabilir && (
          <>
            <Button variant="outlined" startIcon={<FileUploadOutlinedIcon />}
              onClick={() => dosyaRef.current?.click()} sx={bandButton}>İçe Al</Button>
            <Button data-kadi-hedef="yeni-metrik" variant="contained" startIcon={<AddIcon />} onClick={yeniAc} sx={bandButtonWhite}>Yeni Metrik</Button>
          </>
        )}
      </PageHeader>
      <input ref={dosyaRef} type="file" accept=".json,.yaml,.yml,application/json" hidden onChange={dosyaSecildi} />

      {hata && <MuiAlert severity="error" sx={{ mb: 2, borderRadius: boxRadius }} onClose={() => setHata(null)}>{hata}</MuiAlert>}

      {liste.length === 0 && (
        <Paper variant="outlined" sx={{ p: 4, borderRadius: boxRadius, textAlign: 'center' }}>
          <Typography sx={{ fontWeight: 700, mb: 0.5 }}>Henüz metrik yok</Typography>
          <Typography sx={{ fontSize: 13, color: 'text.secondary', mb: 2 }}>
            Örnek: “Toplam Ciro = SUM({'{'}Odemeler.Tutar{'}'})”. Bir kez tanımlanır; tasarımcıda
            “Metrik Ekle” ile her rapora girer ve tanım değişince hepsi yeni tanımı kullanır.
          </Typography>
          {yazabilir && <Button variant="contained" startIcon={<AddIcon />} onClick={yeniAc}>Yeni Metrik</Button>}
        </Paper>
      )}

      <Box sx={{ display: 'grid', gap: 1.25 }}>
        {liste.map((m) => {
          const olcum = olcumler[m.id];
          return (
            <Paper key={m.id} variant="outlined" sx={{ p: 1.75, borderRadius: boxRadius, display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
              <Box sx={{ minWidth: 240, flexGrow: 1 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                  <Typography sx={{ fontWeight: 700 }}>{m.ad}</Typography>
                  <Chip size="small" label={BICIMLER.find((b) => b.deger === m.bicim)?.etiket ?? m.bicim} sx={{ fontSize: 11 }} />
                  {!m.aktif && <Chip size="small" label="Pasif" sx={{ fontSize: 11 }} />}
                </Box>
                <Typography component="code" sx={{ display: 'inline-block', fontFamily: 'monospace', fontSize: 12, bgcolor: 'var(--kds-zemin2)', border: '1px solid', borderColor: 'divider', borderRadius: '6px', px: 1, py: 0.2, my: 0.5, color: 'secondary.main' }}>
                  {m.ifade}
                </Typography>
                <Typography sx={{ fontSize: 12, color: 'text.secondary' }}>
                  {m.haritaAd ?? '(harita silinmiş)'}
                  {m.aciklama ? ` · ${m.aciklama}` : ''}
                  {` · ${m.kullananRaporSayisi > 0 ? `${m.kullananRaporSayisi} raporda kullanılıyor` : 'kullanan rapor yok'}`}
                  {m.sahipAd ? ` · ${m.sahipAd} tanımladı` : ''}
                </Typography>
              </Box>
              <Box sx={{ textAlign: 'right', minWidth: 130 }}>
                <Typography sx={{ fontSize: 21, fontWeight: 800, color: 'primary.main', letterSpacing: '-0.4px', fontVariantNumeric: 'tabular-nums' }}>
                  {olcum ? metrikDegerYaz(olcum.deger, m.bicim) : '—'}
                </Typography>
                <Typography sx={{ fontSize: 10.5, color: 'text.disabled' }}>
                  {olcum ? `${olcum.sureMs} ms'de ölçüldü` : 'henüz ölçülmedi'}
                </Typography>
              </Box>
              <Box sx={{ display: 'flex', gap: 0.5 }}>
                <Button size="small" startIcon={olculenId === m.id ? <CircularProgress size={14} /> : <PlayArrowIcon />}
                  disabled={olculenId !== null} onClick={() => void olc(m)} title="Güncel değeri şimdi hesapla">Ölç</Button>
                <IconButton size="small" onClick={() => void gecmisAc(m)} title="Sürüm geçmişi"><HistoryIcon fontSize="small" /></IconButton>
                {yazabilir && (
                  <>
                    <IconButton size="small" onClick={() => duzenleAc(m)} title="Düzenle"><EditOutlinedIcon fontSize="small" /></IconButton>
                    <IconButton size="small" onClick={() => void sil(m)} title="Sil"><DeleteOutlineIcon fontSize="small" /></IconButton>
                  </>
                )}
              </Box>
            </Paper>
          );
        })}
      </Box>

      <Box sx={{ display: 'flex', justifyContent: 'flex-end', mt: 1 }}>
        <IconButton size="small" onClick={() => void yenile()} title="Yenile"><RefreshIcon fontSize="small" /></IconButton>
      </Box>

      <Drawer anchor="right" open={acik} onClose={() => setAcik(false)}
        slotProps={{ paper: { sx: { width: { xs: '100%', sm: 480 }, p: 2.5 } } }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 2 }}>
          <Typography sx={{ fontWeight: 800 }}>{duzenlenenId ? 'Metriği düzenle' : 'Yeni metrik'}</Typography>
          <IconButton size="small" onClick={() => setAcik(false)}><CloseIcon fontSize="small" /></IconButton>
        </Box>

        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField size="small" label="Metrik adı" value={form.ad} placeholder="ör. Toplam Ciro"
            onChange={(e) => setForm((f) => ({ ...f, ad: e.target.value }))} />

          <FormControl size="small">
            <InputLabel>Veri haritası</InputLabel>
            <Select label="Veri haritası" value={form.veriHaritasiId}
              onChange={(e) => setForm((f) => ({ ...f, veriHaritasiId: e.target.value }))}>
              {haritalar.map((h) => <MenuItem key={h.id} value={h.id}>{h.ad}</MenuItem>)}
            </Select>
          </FormControl>

          {/* S47 — şablon çipleri: tıkla → şablon yerleşir, ilk … boşluğu seçili kalır;
              alan listesinden tıklanan kolon o boşluğu doldurur (zincirleme, klavyesiz kurulum). */}
          <Box>
            <Box sx={{ display: 'flex', gap: 0.75, flexWrap: 'wrap', mb: 1 }}>
              {SABLONLAR.map((t) => (
                <Chip key={t.ad} size="small" label={t.ad} variant="outlined" color="secondary"
                  sx={{ fontWeight: 700 }}
                  onClick={() => { const { bas, son } = imlec(); formuleUygula(sablonYerlestir(form.ifade, bas, son, t.sablon)); }} />
              ))}
            </Box>

            <TextField size="small" fullWidth label="Formül — tasarımcıdaki ifade diliyle aynı" value={form.ifade}
              placeholder="ör. SUM({Odemeler.Tutar}) — ya da üstten bir şablon seç" multiline minRows={2}
              inputRef={formulRef}
              slotProps={{
                input: {
                  sx: { fontFamily: 'monospace', fontSize: 13 },
                  endAdornment: (
                    <InputAdornment position="end">
                      <Tooltip title="Kullanılabilir fonksiyonlar">
                        <IconButton size="small" onClick={(e) => setIpucuHedef(e.currentTarget)}>
                          <HelpOutlineIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </InputAdornment>
                  ),
                },
              }}
              onChange={(e) => setForm((f) => ({ ...f, ifade: e.target.value }))}
              helperText="Kaydederken haritada doğrulanır — geçersiz formül kaydedilemez." />

            {/* Alan listesi: haritanın tablo/kolonları — tıkla-ekle. Gizli kolonlar görünmez. */}
            {form.veriHaritasiId === '' ? (
              <Typography sx={{ fontSize: 12, mt: 0.75 }} color="text.disabled">
                Alanları listelemek için önce veri haritasını seç.
              </Typography>
            ) : (() => {
              const detay = haritaDetaylari[form.veriHaritasiId];
              if (!detay) return null;   // yükleniyor ya da erişilemedi — elle yazım açık
              const arama = alanArama.trim().toLocaleLowerCase('tr');
              return (
                <Box sx={{ mt: 1, border: '1px solid', borderColor: 'divider', borderRadius: smallRadius }}>
                  <TextField size="small" fullWidth placeholder="Alan ara…" value={alanArama}
                    onChange={(e) => setAlanArama(e.target.value)}
                    slotProps={{ input: {
                      sx: { fontSize: 12.5, '& fieldset': { border: 'none' } },
                      startAdornment: <InputAdornment position="start"><SearchIcon sx={{ fontSize: 16 }} /></InputAdornment>,
                    } }} />
                  <Box sx={{ maxHeight: 190, overflowY: 'auto', borderTop: '1px solid', borderColor: 'divider', p: 0.75 }}>
                    {detay.tablolar.map((t) => {
                      const kolonlar = t.kolonlar
                        .filter((k) => k.rol !== 'Gizli')
                        .filter((k) => !arama
                          || (k.dostcaAd ?? k.hamAd).toLocaleLowerCase('tr').includes(arama)
                          || t.dostcaAd.toLocaleLowerCase('tr').includes(arama));
                      if (kolonlar.length === 0) return null;
                      return (
                        <Box key={t.id} sx={{ mb: 0.75 }}>
                          <Typography sx={{ fontSize: 10.5, fontWeight: 700, letterSpacing: 0.8, textTransform: 'uppercase', px: 0.5 }} color="text.secondary">
                            {t.dostcaAd}
                          </Typography>
                          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, mt: 0.25 }}>
                            {kolonlar.map((k) => {
                              const ad = k.dostcaAd ?? k.hamAd;
                              return (
                                <Chip key={k.hamAd} size="small" label={ad} variant="outlined"
                                  sx={{ fontSize: 11.5, height: 22, cursor: 'pointer' }}
                                  onClick={() => { const { bas, son } = imlec(); formuleUygula(alanEkle(form.ifade, bas, son, `{${t.dostcaAd}.${ad}}`)); }} />
                              );
                            })}
                          </Box>
                        </Box>
                      );
                    })}
                  </Box>
                </Box>
              );
            })()}
          </Box>

          <FormControl size="small">
            <InputLabel>Biçim</InputLabel>
            <Select label="Biçim" value={form.bicim}
              onChange={(e) => setForm((f) => ({ ...f, bicim: e.target.value as MetrikBicimi }))}>
              {BICIMLER.map((b) => <MenuItem key={b.deger} value={b.deger}>{b.etiket}</MenuItem>)}
            </Select>
          </FormControl>

          <TextField size="small" label="Açıklama — Soru Sor (NLQ) bunu okuyacak" value={form.aciklama}
            placeholder="Bu ölçüt neyi sayar, neyi saymaz?" multiline minRows={2}
            onChange={(e) => setForm((f) => ({ ...f, aciklama: e.target.value }))} />

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Switch checked={form.aktif} onChange={(e) => setForm((f) => ({ ...f, aktif: e.target.checked }))} />
            <Typography sx={{ fontSize: 13 }}>Aktif</Typography>
          </Box>

          {formHata && <MuiAlert severity="error" sx={{ borderRadius: boxRadius }} onClose={() => setFormHata(null)}>{formHata}</MuiAlert>}

          <Box sx={{ display: 'flex', gap: 1, mt: 1 }}>
            <Button variant="contained" onClick={() => void kaydet()}
              disabled={kaydediliyor || form.ad.trim() === '' || form.veriHaritasiId === '' || form.ifade.trim() === ''}>
              Kaydet
            </Button>
            <Button onClick={() => setAcik(false)}>Vazgeç</Button>
          </Box>
        </Box>

        {/* S47 — fonksiyon ipucu: içerik bekçinin yanından gelir (tek kaynak, testle kilitli);
            örneğe tıklanınca formüle eklenir. */}
        <Popover open={ipucuHedef !== null} anchorEl={ipucuHedef} onClose={() => setIpucuHedef(null)}
          anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
          transformOrigin={{ vertical: 'top', horizontal: 'right' }}
          slotProps={{ paper: { sx: { width: 400, maxHeight: 420, p: 1.5, borderRadius: boxRadius } } }}>
          <Typography sx={{ fontSize: 12.5, fontWeight: 700, mb: 0.5 }}>
            Kullanılabilir fonksiyonlar — örneğe tıkla, formüle eklensin
          </Typography>
          {fonksiyonlar.map((g) => (
            <Box key={g.grup} sx={{ mb: 1 }}>
              <Typography sx={{ fontSize: 10.5, fontWeight: 700, letterSpacing: 0.8, textTransform: 'uppercase' }} color="secondary.main">
                {g.grup}
              </Typography>
              {g.fonksiyonlar.map((f) => (
                <Box key={f.ad} sx={{ px: 0.75, py: 0.5, borderRadius: smallRadius, cursor: 'pointer', '&:hover': { bgcolor: 'action.hover' } }}
                  onClick={() => { const { bas, son } = imlec(); formuleUygula(alanEkle(form.ifade, bas, son, f.ornek)); setIpucuHedef(null); }}>
                  <Typography component="code" sx={{ fontFamily: 'monospace', fontSize: 12, display: 'block' }}>{f.ornek}</Typography>
                  <Typography sx={{ fontSize: 11.5 }} color="text.secondary">{f.ad} — {f.aciklama}</Typography>
                </Box>
              ))}
            </Box>
          ))}
          {fonksiyonlar.length === 0 && (
            <Typography sx={{ fontSize: 12 }} color="text.secondary">Liste alınamadı — elle yazım her zaman açık.</Typography>
          )}
        </Popover>
      </Drawer>

      {/* S30 — sürüm geçmişi: satır = tanımın ESKİ hâli; güncel hâl listede zaten görünüyor. */}
      <Dialog open={gecmisMetrik !== null} onClose={() => setGecmisMetrik(null)} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ fontWeight: 800 }}>Sürüm geçmişi — {gecmisMetrik?.ad}</DialogTitle>
        <DialogContent>
          {gecmis === null ? (
            <Box sx={{ py: 3, textAlign: 'center' }}><CircularProgress size={22} /></Box>
          ) : gecmis.length === 0 ? (
            <Typography sx={{ fontSize: 13, color: 'text.secondary', py: 1 }}>
              Henüz sürüm yok — geçmiş, metrik ilk kez <b>güncellendiğinde</b> oluşur (önceki hâl saklanır).
            </Typography>
          ) : (
            <Box sx={{ display: 'grid', gap: 1 }}>
              {gecmis.map((v) => (
                <Paper key={v.surumNo} variant="outlined" sx={{ p: 1.5, borderRadius: boxRadius, display: 'flex', gap: 1.5, alignItems: 'center', flexWrap: 'wrap' }}>
                  <Chip size="small" label={`v${v.surumNo}`} sx={{ fontWeight: 700 }} />
                  <Box sx={{ flex: 1, minWidth: 200 }}>
                    <Typography sx={{ fontWeight: 700, fontSize: 13.5 }}>{v.ad}
                      <Typography component="span" sx={{ fontSize: 11.5, color: 'text.disabled', ml: 1 }}>
                        {BICIMLER.find((b) => b.deger === v.bicim)?.etiket ?? v.bicim}
                      </Typography>
                    </Typography>
                    <Typography component="code" sx={{ display: 'inline-block', fontFamily: 'monospace', fontSize: 12, bgcolor: 'var(--kds-zemin2)', border: '1px solid', borderColor: 'divider', borderRadius: '6px', px: 1, py: 0.2, my: 0.4, color: 'secondary.main' }}>
                      {v.ifade}
                    </Typography>
                    <Typography sx={{ fontSize: 11.5, color: 'text.secondary' }}>
                      {new Date(v.tarih).toLocaleString('tr-TR')}
                      {v.degistirenAd ? ` · ${v.degistirenAd} değiştirdi` : ''}
                      {v.aciklama ? ` · ${v.aciklama}` : ''}
                    </Typography>
                  </Box>
                  {yazabilir && (
                    <Button size="small" variant="outlined" startIcon={donulenNo === v.surumNo ? <CircularProgress size={13} /> : <RestoreIcon />}
                      disabled={donulenNo !== null} onClick={() => void surumeDon(v.surumNo)}>
                      Bu sürüme dön
                    </Button>
                  )}
                </Paper>
              ))}
            </Box>
          )}
          <Typography sx={{ fontSize: 12, color: 'text.disabled', mt: 1.5 }}>
            "Sürüme dön" normal kaydetme yolundan geçer: formül bugünkü haritada yeniden doğrulanır
            ve dönüşün kendisi de sürüm üretir — geçmiş hiç kaybolmaz.
          </Typography>
        </DialogContent>
        <DialogActions><Button onClick={() => setGecmisMetrik(null)}>Kapat</Button></DialogActions>
      </Dialog>

      {/* S30 — içe alım: önizleme + çakışma stratejisi; sonuç öğe öğe raporlanır. */}
      <Dialog open={iceOgeler !== null || iceOsiYaml !== null} onClose={() => { if (!iceCalisiyor) { setIceOgeler(null); setIceOsiYaml(null); setIceSonuclar(null); } }} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ fontWeight: 800 }}>Metrikleri içe al</DialogTitle>
        <DialogContent>
          {iceSonuclar === null ? (
            <>
              <Typography sx={{ fontSize: 13, mb: 1.5 }}>
                {iceOsiYaml
                  ? <>OSI (Apache Ossie) dosyası seçildi. Modeller harita ADIYLA eşleştirilir; metrik
                    ifadeleri kaydedilirken bugünkü haritada doğrulanır — sonuç öğe öğe listelenir.</>
                  : <>Dosyada <b>{iceOgeler?.length ?? 0} metrik</b> var. Haritalar ADLA eşleştirilir;
                    formüller kaydedilirken bugünkü haritada doğrulanır.</>}
              </Typography>
              <Box sx={{ display: 'grid', gap: 0.5, mb: 1.5, maxHeight: 260, overflowY: 'auto' }}>
                {iceOgeler?.map((o, i) => (
                  <Typography key={i} sx={{ fontSize: 12.5 }}>
                    • <b>{o.ad}</b> <Typography component="span" sx={{ fontSize: 11.5, color: 'text.secondary' }}>({o.haritaAd})</Typography>
                  </Typography>
                ))}
              </Box>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <Switch checked={iceGuncelle} onChange={(e) => setIceGuncelle(e.target.checked)} />
                <Typography sx={{ fontSize: 13 }}>
                  Aynı adla metrik varsa <b>güncelle</b> (eski tanım sürüm geçmişine yazılır); kapalıysa atlanır.
                </Typography>
              </Box>
            </>
          ) : (
            <Box sx={{ display: 'grid', gap: 0.75 }}>
              {iceSonuclar.map((s, i) => (
                <Box key={i} sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                  <Chip size="small" label={{ Eklendi: 'Eklendi', Guncellendi: 'Güncellendi', Atlandi: 'Atlandı', Hata: 'Hata' }[s.sonuc]}
                    color={s.sonuc === 'Hata' ? 'error' : s.sonuc === 'Atlandi' ? 'default' : 'success'}
                    variant={s.sonuc === 'Atlandi' ? 'outlined' : 'filled'} sx={{ fontWeight: 700 }} />
                  <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{s.ad}</Typography>
                  {s.mesaj && <Typography sx={{ fontSize: 12, color: 'text.secondary' }}>{s.mesaj}</Typography>}
                </Box>
              ))}
            </Box>
          )}
        </DialogContent>
        <DialogActions>
          {iceSonuclar === null ? (
            <>
              <Button onClick={() => { setIceOgeler(null); setIceOsiYaml(null); }} disabled={iceCalisiyor}>Vazgeç</Button>
              <Button variant="contained" onClick={() => void iceAl()} disabled={iceCalisiyor}>
                {iceCalisiyor ? 'Alınıyor…' : 'İçe Al'}
              </Button>
            </>
          ) : (
            <Button onClick={() => { setIceOgeler(null); setIceOsiYaml(null); setIceSonuclar(null); }}>Kapat</Button>
          )}
        </DialogActions>
      </Dialog>
      {confirmDialog}
    </Box>
  );
}
