import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import Dialog from '@mui/material/Dialog';
import DialogContent from '@mui/material/DialogContent';
import MenuItem from '@mui/material/MenuItem';
import Select from '@mui/material/Select';
import Divider from '@mui/material/Divider';
import Tooltip from '@mui/material/Tooltip';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import AddIcon from '@mui/icons-material/Add';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import CloseIcon from '@mui/icons-material/Close';
import SaveIcon from '@mui/icons-material/Save';
import TableChartOutlinedIcon from '@mui/icons-material/TableChartOutlined';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import { getConnections, type Connection, type DataMode } from '../api/connections';
import {
  getDataMaps, getDataMap, addDataMap, updateDataMap, deleteDataMap,
  searchSchemaTables, getSchemaColumns, getSchemaRelationships,
  type DataMapSummary, type ColumnRole, type SchemaTableDto, type SchemaColumnDto,
} from '../api/dataMaps';
import { LoadingCurtain } from '../components/Loading';
import { useConfirm } from '../components/ConfirmDialog';
import { PageHeader, bandButtonWhite } from '../components/PageHeader';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import { boxRadius as R_KART, smallRadius as R_KUCUK } from '../theme';
import { kadiYukleniyor, kadiHata, kadiBasarili, kadiOner } from '../kadi/kadiKanal';

type EditKolon = { hamAd: string; dostcaAd: string; rol: ColumnRole; veriTipi: string | null };
type EditTablo = { id: string; sema: string; hamAd: string; dostcaAd: string; kolonlar: EditKolon[] };
type EditIliski = { kaynakTabloId: string; kaynakKolon: string; hedefTabloId: string; hedefKolon: string; fktenMi: boolean };
type Editor = { id: string | null; ad: string; baglantiId: string; mod: DataMode; tablolar: EditTablo[]; iliskiler: EditIliski[] };

const uuid = () => crypto.randomUUID();
const olculer = ['int', 'bigint', 'smallint', 'tinyint', 'decimal', 'numeric', 'money', 'smallmoney', 'float', 'real'];
function varsayilanRol(k: SchemaColumnDto): ColumnRole {
  if (k.birincilAnahtarMi || /id$/i.test(k.ad)) return 'Anahtar';
  return olculer.includes(k.veriTipi.toLowerCase()) ? 'Olcu' : 'Boyut';
}
// Kolon "kullanımı": müşteri raporlarında bu kolonun ne işe yarayacağı (eylem odaklı, sade dille).
const rolBilgi: Record<ColumnRole, { etiket: string; renk: string; aciklama: string }> = {
  Boyut: { etiket: 'Kırılım', renk: '#2f6df6', aciklama: 'Kategorik alan — bununla grupla / kır (şehir, kategori, tarih).' },
  Olcu: { etiket: 'Sayısal', renk: 'success.main', aciklama: 'Sayısal ölçü — toplanır / ortalanır (tutar, adet).' },
  Anahtar: { etiket: 'Anahtar', renk: '#b0851f', aciklama: 'Kimlik / ilişki alanı — raporda genelde gizli tutulur.' },
  Gizli: { etiket: 'Gizli', renk: '#8a8f98', aciklama: 'Müşteriye hiç gösterilmez.' },
};
const rolSira: ColumnRole[] = ['Boyut', 'Olcu', 'Anahtar', 'Gizli'];

// Yarıçap standardı temadan gelir (R_KART=boxRadius=10px, R_KUCUK=smallRadius=8px) — tüm ekranlar aynı.
// Outlined input'lar theme yarıçapı + 16px font ile iri/hap gibi duruyor: daha keskin
// köşe + kompakt yükseklik + 13px font ile derli-toplu hâle getiriyoruz.
const alanStil = {
  '& .MuiOutlinedInput-root': { borderRadius: R_KUCUK },
  '& .MuiOutlinedInput-input': { padding: '6.5px 11px', fontSize: 13 },
};
const ekranStil = { '& .MuiButton-root': { borderRadius: R_KUCUK }, ...alanStil };
const diyalogKagit = { borderRadius: R_KART, ...alanStil };

function RolNokta({ rol, ustBosluk }: { rol: ColumnRole; ustBosluk?: boolean }) {
  return <Box component="span" sx={{ width: 9, height: 9, borderRadius: '50%', bgcolor: rolBilgi[rol].renk, flexShrink: 0, display: 'inline-block', ...(ustBosluk ? { mt: 0.6 } : {}) }} />;
}

export function DataMapsPage() {
  const { askConfirm, confirmDialog } = useConfirm();
  const [gorunum, setGorunum] = useState<'liste' | 'editor'>('liste');
  const [liste, setListe] = useState<DataMapSummary[]>([]);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [hata, setHata] = useState<string | null>(null);
  const [baglantilar, setBaglantilar] = useState<Connection[]>([]);

  const [yeniAcik, setYeniAcik] = useState(false);
  const [yeniAd, setYeniAd] = useState('');
  const [yeniBaglanti, setYeniBaglanti] = useState('');

  const [ed, setEd] = useState<Editor | null>(null);
  const [seciliId, setSeciliId] = useState<string | null>(null);
  const [kolonAra, setKolonAra] = useState('');
  const [kaydediyor, setKaydediyor] = useState(false);
  const [editorHata, setEditorHata] = useState<string | null>(null);
  const [kaydedildi, setKaydedildi] = useState(false);
  const [kirli, setKirli] = useState(false);

  const [ekleAcik, setEkleAcik] = useState(false);
  const [arama, setArama] = useState('');
  const [sonuc, setSonuc] = useState<SchemaTableDto[]>([]);
  const [ariyor, setAriyor] = useState(false);
  const [ekleniyor, setEkleniyor] = useState(false);

  async function refresh() {
    setYukleniyor(true);
    setHata(null);
    try {
      const [h, b] = await Promise.all([getDataMaps(), getConnections()]);
      setListe(h);
      setBaglantilar(b);
    } catch (e) {
      setHata((e as Error).message); kadiHata();
    } finally {
      setYukleniyor(false);
    }
  }
  useEffect(() => { void refresh(); }, []);
  // S29/2: boş durumda Kadi yol gösterir (oturumda bir kez)
  useEffect(() => {
    if (yukleniyor || liste.length > 0) return;
    const anahtar = 'kds.kadiBos:veri-haritalari';
    if (sessionStorage.getItem(anahtar)) return;
    sessionStorage.setItem(anahtar, '1');
    kadiOner('Henüz harita yok — bir bağlantı seçip ilk haritayı kuralım mı?', document.querySelector('[data-kadi-hedef="yeni-harita"]'));
  }, [yukleniyor, liste.length]);


  const baglantiAd = (id: string) => baglantilar.find((b) => b.id === id)?.ad ?? '';

  function openNewMap() {
    setYeniAd('');
    setYeniBaglanti(baglantilar[0]?.id ?? '');
    setYeniAcik(true);
  }
  function createNew() {
    if (!yeniAd.trim() || !yeniBaglanti) return;
    const bmod = baglantilar.find((b) => b.id === yeniBaglanti)?.mod ?? 'Canli';
    setEd({ id: null, ad: yeniAd.trim(), baglantiId: yeniBaglanti, mod: bmod, tablolar: [], iliskiler: [] });
    setSeciliId(null);
    setEditorHata(null);
    setKaydedildi(false);
    setKirli(false);
    setYeniAcik(false);
    setGorunum('editor');
  }

  async function ac(ozet: DataMapSummary) {
    setEditorHata(null);
    setKaydedildi(false);
    const kadiBitir = kadiYukleniyor('Haritayı açıyorum…');
    try {
      const d = await getDataMap(ozet.id);
      setEd({
        id: d.id, ad: d.ad, baglantiId: d.baglantiId, mod: d.mod,
        tablolar: d.tablolar.map((t) => ({ id: t.id, sema: t.sema, hamAd: t.hamAd, dostcaAd: t.dostcaAd,
          kolonlar: t.kolonlar.map((k) => ({ hamAd: k.hamAd, dostcaAd: k.dostcaAd ?? '', rol: k.rol, veriTipi: k.veriTipi })) })),
        iliskiler: d.iliskiler.map((i) => ({ kaynakTabloId: i.kaynakTabloId, kaynakKolon: i.kaynakKolon, hedefTabloId: i.hedefTabloId, hedefKolon: i.hedefKolon, fktenMi: i.fktenMi })),
      });
      setSeciliId(d.tablolar[0]?.id ?? null);
      setKirli(false);
      setGorunum('editor');
    } catch (e) {
      setHata((e as Error).message); kadiHata();
    } finally {
      kadiBitir();
    }
  }

  async function geriDon() {
    if (kirli && !(await askConfirm('Kaydedilmemiş değişiklikler var. Kaydetmeden çıkılsın mı?', { baslik: 'Kaydedilmemiş değişiklik', onayYazisi: 'Çık' }))) return;
    setGorunum('liste');
    void refresh();
  }

  async function sil(ozet: DataMapSummary) {
    if (!(await askConfirm(`"${ozet.ad}" haritası silinsin mi?`, { tehlikeli: true, onayYazisi: 'Sil' }))) return;
    const kadiBitir = kadiYukleniyor('Haritayı siliyorum…');
    try { await deleteDataMap(ozet.id); await refresh(); }
    catch (e) { setHata((e as Error).message); kadiHata(); }
    finally { kadiBitir(); }
  }

  function openAddTable() {
    setArama('');
    setSonuc([]);
    setEkleAcik(true);
    void ara('');
  }
  async function ara(q: string) {
    if (!ed) return;
    setAriyor(true);
    try { setSonuc(await searchSchemaTables(ed.baglantiId, q)); }
    catch { setSonuc([]); }
    finally { setAriyor(false); }
  }

  async function addTable(t: SchemaTableDto) {
    if (!ed || ed.tablolar.some((x) => x.sema === t.sema && x.hamAd === t.ad)) return;
    setEkleniyor(true);
    try {
      const kolonlar = await getSchemaColumns(ed.baglantiId, t.sema, t.ad);
      const yeni: EditTablo = {
        id: uuid(), sema: t.sema, hamAd: t.ad, dostcaAd: t.ad,
        kolonlar: kolonlar.map((k) => ({ hamAd: k.ad, dostcaAd: k.ad, rol: varsayilanRol(k), veriTipi: k.veriTipi })),
      };
      const fks = await getSchemaRelationships(ed.baglantiId, t.sema, t.ad).catch(() => []);
      setEd((p) => {
        if (!p) return p;
        const tablolar = [...p.tablolar, yeni];
        const iliskiler = [...p.iliskiler];
        for (const fk of fks) {
          const k = tablolar.find((x) => x.sema === fk.kaynakSema && x.hamAd === fk.kaynakTablo);
          const h = tablolar.find((x) => x.sema === fk.hedefSema && x.hamAd === fk.hedefTablo);
          if (k && h && !iliskiler.some((i) => i.kaynakTabloId === k.id && i.kaynakKolon === fk.kaynakKolon && i.hedefTabloId === h.id && i.hedefKolon === fk.hedefKolon))
            iliskiler.push({ kaynakTabloId: k.id, kaynakKolon: fk.kaynakKolon, hedefTabloId: h.id, hedefKolon: fk.hedefKolon, fktenMi: true });
        }
        return { ...p, tablolar, iliskiler };
      });
      setSeciliId(yeni.id);
      setKirli(true);
    } finally {
      setEkleniyor(false);
    }
  }

  const doUpdate = (fn: (p: Editor) => Editor) => { setEd((p) => (p ? fn(p) : p)); setKirli(true); };
  const onTableChange = (id: string, fn: (t: EditTablo) => EditTablo) =>
    doUpdate((p) => ({ ...p, tablolar: p.tablolar.map((t) => (t.id === id ? fn(t) : t)) }));
  const onColumnChange = (tabloId: string, hamAd: string, fn: (k: EditKolon) => EditKolon) =>
    onTableChange(tabloId, (t) => ({ ...t, kolonlar: t.kolonlar.map((k) => (k.hamAd === hamAd ? fn(k) : k)) }));

  function deleteTable(id: string) {
    doUpdate((p) => ({
      ...p,
      tablolar: p.tablolar.filter((t) => t.id !== id),
      iliskiler: p.iliskiler.filter((i) => i.kaynakTabloId !== id && i.hedefTabloId !== id),
    }));
    if (seciliId === id) setSeciliId(null);
  }

  async function save() {
    if (!ed) return;
    setKaydediyor(true);
    setEditorHata(null);
    const kadiBitir = kadiYukleniyor('Haritayı kaydediyorum…');
    try {
      const govde = {
        ad: ed.ad, baglantiId: ed.baglantiId, mod: ed.mod,
        tablolar: ed.tablolar.map((t) => ({ id: t.id, sema: t.sema, hamAd: t.hamAd, dostcaAd: t.dostcaAd,
          kolonlar: t.kolonlar.map((k) => ({ hamAd: k.hamAd, dostcaAd: k.dostcaAd || null, rol: k.rol, veriTipi: k.veriTipi })) })),
        iliskiler: ed.iliskiler,
      };
      const sonucOzet = ed.id ? await updateDataMap(ed.id, govde) : await addDataMap(govde);
      setEd((p) => (p ? { ...p, id: sonucOzet.id } : p));
      setKaydedildi(true);
      setKirli(false);
      void refresh();
      kadiBasarili('Haritayı kaydettim.');
    } catch (e) {
      setEditorHata((e as Error).message); kadiHata();
    } finally {
      kadiBitir();
      setKaydediyor(false);
    }
  }

  const secili = ed?.tablolar.find((t) => t.id === seciliId) ?? null;
  const gecerli = !!ed?.ad.trim() && !!ed?.baglantiId;
  const tableLabel = (id: string) => ed?.tablolar.find((t) => t.id === id)?.dostcaAd ?? '?';

  // ---------- LİSTE ----------
  if (gorunum === 'liste') {
    return (
      <Box sx={ekranStil}>
        <PageHeader ikon={<AccountTreeOutlinedIcon />} baslik="Veri Haritaları"
          aciklama="Ham tabloları müşteri için kullanılabilir hâle getir (tablo seç, adlandır, ilişkilendir, boyut/ölçü)."
          rozetler={[{ sayi: liste.length, ad: 'harita' }]}>
          <Button data-kadi-hedef="yeni-harita" variant="contained" startIcon={<AddIcon />} onClick={openNewMap} disabled={baglantilar.length === 0} sx={bandButtonWhite}>Yeni Harita</Button>
        </PageHeader>

        {hata && <Alert severity="error" sx={{ mb: 2 }}>{hata}</Alert>}
        {baglantilar.length === 0 && !yukleniyor && <Alert severity="info" sx={{ mb: 2 }}>Önce bir <b>Bağlantı</b> ekleyin — harita bir bağlantı üzerine kurulur.</Alert>}

        {yukleniyor ? (
          <LoadingCurtain />
        ) : (
          <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', lg: 'repeat(3, 1fr)' }, gap: 2 }}>
            {liste.map((h) => (
              <Paper key={h.id} variant="outlined" sx={{ borderRadius: R_KART, overflow: 'hidden', transition: 'box-shadow .15s, border-color .15s', '&:hover': { boxShadow: '0 4px 16px rgba(15,45,78,.10)', borderColor: 'divider' } }}>
                <Box sx={{ height: 4, bgcolor: h.mod === 'Canli' ? '#0e6f7a' : '#c8862a' }} />
                <Box sx={{ p: 2 }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.25 }}>
                    <TableChartOutlinedIcon sx={{ fontSize: 18, color: h.mod === 'Canli' ? '#0e6f7a' : '#c8862a' }} />
                    <Typography sx={{ fontWeight: 700, fontSize: 14.5 }} color="primary" noWrap>{h.ad}</Typography>
                  </Box>
                  <Typography sx={{ fontSize: 12 }} color="text.secondary">{h.baglantiAd ?? '—'} · {h.tabloSayisi} tablo · {h.iliskiSayisi} ilişki</Typography>
                  <Box sx={{ display: 'flex', gap: 1, mt: 1.5 }}>
                    <Button size="small" variant="outlined" fullWidth onClick={() => void ac(h)}>Aç / Düzenle</Button>
                    <Tooltip title="Sil"><IconButton size="small" onClick={() => void sil(h)}><DeleteOutlineIcon fontSize="small" /></IconButton></Tooltip>
                  </Box>
                </Box>
              </Paper>
            ))}
            <Paper variant="outlined" onClick={baglantilar.length ? openNewMap : undefined}
              sx={{ borderRadius: R_KART, borderStyle: 'dashed', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 1, minHeight: 130, cursor: baglantilar.length ? 'pointer' : 'default', color: 'secondary.main', opacity: baglantilar.length ? 1 : 0.5, transition: 'background-color .15s', '&:hover': baglantilar.length ? { bgcolor: 'var(--kds-zemin2)' } : {} }}>
              <Box sx={{ width: 42, height: 42, borderRadius: '50%', bgcolor: 'var(--kds-tealdolgu)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}><AddIcon /></Box>
              <Typography sx={{ fontWeight: 700, fontSize: 14 }}>Yeni Harita</Typography>
            </Paper>
          </Box>
        )}

        <Dialog open={yeniAcik} onClose={() => setYeniAcik(false)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: diyalogKagit } }}>
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, pt: 2 }}>
            <Typography variant="h6" color="primary">Yeni Harita</Typography>
            <IconButton size="small" onClick={() => setYeniAcik(false)}><CloseIcon fontSize="small" /></IconButton>
          </Box>
          <DialogContent>
            <TextField label="Harita adı" size="small" fullWidth value={yeniAd} onChange={(e) => setYeniAd(e.target.value)} placeholder="ör. Satış Haritası" sx={{ mt: 1 }} />
            <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: 'text.secondary', mt: 2, mb: 0.5 }}>Bağlantı</Typography>
            <Select size="small" fullWidth value={yeniBaglanti} onChange={(e) => setYeniBaglanti(e.target.value)}>
              {baglantilar.map((b) => <MenuItem key={b.id} value={b.id}>{b.ad} — {b.sunucu}/{b.veritabani}</MenuItem>)}
            </Select>
          </DialogContent>
          <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1, px: 3, pb: 2 }}>
            <Button onClick={() => setYeniAcik(false)} color="inherit">İptal</Button>
            <Button variant="contained" onClick={createNew} disabled={!yeniAd.trim() || !yeniBaglanti}>Oluştur</Button>
          </Box>
        </Dialog>

        {/* M1: onay diyaloğu LİSTE görünümünde de render edilmeli — yalnız editörde olunca
            "Sil"e basınca hiç tepki gelmiyor, diyalog ancak editöre girince beliriyordu. */}
        {confirmDialog}
      </Box>
    );
  }

  // ---------- EDİTÖR ----------
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', ...ekranStil }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 2, flexWrap: 'wrap' }}>
        <IconButton onClick={() => void geriDon()}><ArrowBackIcon /></IconButton>
        <TextField size="small" value={ed?.ad ?? ''} onChange={(e) => doUpdate((p) => ({ ...p, ad: e.target.value }))} sx={{ minWidth: 220 }} />
        <Typography sx={{ fontSize: 12.5 }} color="text.secondary">
          Bağlantı: <b>{baglantiAd(ed!.baglantiId)}</b> · {ed?.mod === 'Canli' ? 'Canlı' : 'İçe Alım'} <Box component="span" sx={{ color: 'text.disabled' }}>(bağlantıdan)</Box>
        </Typography>
        <Box sx={{ flexGrow: 1 }} />
        {kirli
          ? <Typography sx={{ fontSize: 12, color: 'warning.main', fontWeight: 700 }}>● kaydedilmemiş değişiklik</Typography>
          : kaydedildi ? <Typography sx={{ fontSize: 12, color: 'success.main', fontWeight: 600 }}>✓ Kaydedildi</Typography> : null}
        <Button variant="contained" startIcon={kaydediyor ? <CircularProgress size={15} color="inherit" /> : <SaveIcon />} onClick={() => void save()} disabled={!gecerli || kaydediyor}>Kaydet</Button>
      </Box>

      {editorHata && <Alert severity="error" sx={{ mb: 2 }}>{editorHata}</Alert>}

      <Box sx={{ display: 'flex', gap: 2, height: 'calc(100vh - 200px)', minHeight: 400 }}>
        {/* sol: haritadaki tablolar */}
        <Paper variant="outlined" sx={{ width: 248, flexShrink: 0, borderRadius: R_KART, display: 'flex', flexDirection: 'column', overflow: 'hidden', bgcolor: 'var(--kds-zemin2)' }}>
          <Box sx={{ p: 1.5 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1.25 }}>
              <Typography sx={{ fontSize: 12.5, fontWeight: 800, color: 'primary.main' }}>Haritadaki tablolar</Typography>
              <Box sx={{ fontSize: 11.5, fontWeight: 700, color: 'text.secondary', bgcolor: 'var(--kds-dolgu)', borderRadius: 5, px: 1, py: '1px', minWidth: 22, textAlign: 'center' }}>{ed?.tablolar.length ?? 0}</Box>
            </Box>
            <Button fullWidth variant="contained" startIcon={<AddIcon />} onClick={openAddTable}>Tablo Ekle</Button>
          </Box>
          <Divider />
          <Box sx={{ overflowY: 'auto', flexGrow: 1, py: 0.5, bgcolor: 'background.paper' }}>
            {ed?.tablolar.map((t) => {
              const sec = seciliId === t.id;
              return (
                <Box key={t.id} onClick={() => setSeciliId(t.id)}
                  sx={{ mx: 1, my: 0.4, px: 1.25, py: 0.85, borderRadius: R_KUCUK, cursor: 'pointer', display: 'flex', gap: 1, alignItems: 'center',
                    bgcolor: sec ? 'var(--kds-tealdolgu)' : 'transparent', boxShadow: sec ? 'inset 3px 0 0 #0e6f7a' : 'none',
                    '&:hover': { bgcolor: sec ? 'var(--kds-tealdolgu)' : 'var(--kds-zemin2)' }, '&:hover .silBtn': { opacity: 1 } }}>
                  <TableChartOutlinedIcon sx={{ fontSize: 18, color: sec ? 'primary.main' : 'text.disabled', flexShrink: 0 }} />
                  <Box sx={{ minWidth: 0, flexGrow: 1 }}>
                    <Typography noWrap sx={{ fontSize: 13, fontWeight: sec ? 700 : 600, color: sec ? 'primary.main' : 'text.primary' }}>{t.dostcaAd || t.hamAd}</Typography>
                    <Typography noWrap sx={{ fontSize: 10.5 }} color="text.secondary">{t.hamAd} · {t.kolonlar.length} kolon</Typography>
                  </Box>
                  <IconButton className="silBtn" size="small" onClick={(e) => { e.stopPropagation(); deleteTable(t.id); }} sx={{ opacity: 0, transition: 'opacity .15s' }}><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                </Box>
              );
            })}
            {ed?.tablolar.length === 0 && (
              <Box sx={{ px: 2, py: 4, textAlign: 'center', color: 'text.secondary' }}>
                <TableChartOutlinedIcon sx={{ fontSize: 34, color: 'text.disabled', mb: 1 }} />
                <Typography sx={{ fontSize: 12.5, fontWeight: 600 }}>Henüz tablo yok</Typography>
                <Typography sx={{ fontSize: 11.5, mt: 0.5 }}>Yukarıdan <b>Tablo Ekle</b> ile başla.</Typography>
              </Box>
            )}
          </Box>
          <Box sx={{ borderTop: '1px solid', borderColor: 'divider', px: 1.5, py: 1, display: 'flex', gap: 1, fontSize: 11, color: 'text.secondary', bgcolor: 'var(--kds-zemin2)' }}>
            <span><b>{ed?.tablolar.length}</b> tablo</span><span>·</span><span><b>{ed?.iliskiler.length}</b> ilişki</span>
          </Box>
        </Paper>

        {/* orta: seçili tablonun kolonları + ilişkiler */}
        <Box sx={{ flexGrow: 1, minWidth: 0, overflowY: 'auto', bgcolor: 'background.paper' }}>
          {secili ? (
            <>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 1.5, flexWrap: 'wrap' }}>
                <Typography sx={{ fontSize: 12.5, fontWeight: 600 }} color="text.secondary">Özelleştirilmiş isim</Typography>
                <TextField size="small" value={secili.dostcaAd} onChange={(e) => onTableChange(secili.id, (t) => ({ ...t, dostcaAd: e.target.value }))} sx={{ maxWidth: 240 }} />
                <Typography sx={{ fontSize: 12 }} color="text.secondary">← {secili.hamAd}</Typography>
                <Box sx={{ flexGrow: 1 }} />
                <TextField size="small" placeholder="Kolon ara…" value={kolonAra} onChange={(e) => setKolonAra(e.target.value)} sx={{ maxWidth: 180 }} />
              </Box>
              <Paper variant="outlined" sx={{ borderRadius: R_KART, overflow: 'hidden' }}>
                <Box sx={{ display: 'grid', gridTemplateColumns: '1.1fr 1.3fr 136px', gap: 1, px: 1.5, py: 1, bgcolor: 'var(--kds-zemin2)', fontSize: 11, fontWeight: 700, color: 'text.secondary', textTransform: 'uppercase', alignItems: 'center' }}>
                  <span>Kolon</span>
                  <span>Özelleştirilmiş isim</span>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                    <span>Kullanım</span>
                    <Tooltip arrow title={
                      <Box sx={{ p: 0.5 }}>
                        <Typography sx={{ fontSize: 11.5, mb: 0.75, fontWeight: 700 }}>Bu kolon müşteri raporlarında nasıl kullanılsın?</Typography>
                        {rolSira.map((r) => (
                          <Box key={r} sx={{ display: 'flex', gap: 0.8, alignItems: 'flex-start', mb: 0.5 }}>
                            <RolNokta rol={r} ustBosluk />
                            <Typography sx={{ fontSize: 11.5 }}><b>{rolBilgi[r].etiket}</b> — {rolBilgi[r].aciklama}</Typography>
                          </Box>
                        ))}
                      </Box>
                    }><InfoOutlinedIcon sx={{ fontSize: 14, color: 'text.disabled', cursor: 'help' }} /></Tooltip>
                  </Box>
                </Box>
                {secili.kolonlar.filter((k) => !kolonAra || k.hamAd.toLowerCase().includes(kolonAra.toLowerCase()) || k.dostcaAd.toLowerCase().includes(kolonAra.toLowerCase())).map((k) => (
                  <Box key={k.hamAd} sx={{ display: 'grid', gridTemplateColumns: '1.1fr 1.3fr 136px', gap: 1, px: 1.5, py: 0.5, alignItems: 'center', borderTop: '1px solid #eef2f6' }}>
                    <Typography sx={{ fontSize: 12.5 }} color="text.secondary" noWrap title={`${k.hamAd} · ${k.veriTipi ?? ''}`}>{k.hamAd}</Typography>
                    <TextField variant="standard" value={k.dostcaAd} onChange={(e) => onColumnChange(secili.id, k.hamAd, (x) => ({ ...x, dostcaAd: e.target.value }))} slotProps={{ input: { sx: { fontSize: 13 } } }} />
                    <Select
                      size="small" variant="standard" value={k.rol}
                      onChange={(e) => onColumnChange(secili.id, k.hamAd, (x) => ({ ...x, rol: e.target.value as ColumnRole }))}
                      renderValue={(v) => (
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.7 }}>
                          <RolNokta rol={v} />
                          <Box component="span" sx={{ fontSize: 12.5 }}>{rolBilgi[v].etiket}</Box>
                        </Box>
                      )}
                      sx={{
                        fontSize: 12.5,
                        '&::before': { borderBottom: 'none !important' },
                        '&::after': { borderBottom: 'none' },
                        '& .MuiSelect-select': { py: 0.4, pl: 0.75, borderRadius: 1, '&:hover': { bgcolor: 'var(--kds-zemin2)' } },
                      }}
                    >
                      {rolSira.map((r) => (
                        <MenuItem key={r} value={r} sx={{ alignItems: 'flex-start', py: 0.9, maxWidth: 320, whiteSpace: 'normal' }}>
                          <Box sx={{ display: 'flex', gap: 1 }}>
                            <RolNokta rol={r} ustBosluk />
                            <Box>
                              <Typography sx={{ fontSize: 13, fontWeight: 600, lineHeight: 1.3 }}>{rolBilgi[r].etiket}</Typography>
                              <Typography sx={{ fontSize: 11.5, whiteSpace: 'normal' }} color="text.secondary">{rolBilgi[r].aciklama}</Typography>
                            </Box>
                          </Box>
                        </MenuItem>
                      ))}
                    </Select>
                  </Box>
                ))}
              </Paper>
            </>
          ) : (
            <Paper variant="outlined" sx={{ p: 4, borderRadius: R_KART, borderStyle: 'dashed', textAlign: 'center' }}>
              <Typography color="text.secondary">Soldan bir tablo seç ya da “Tablo Ekle” ile başla.</Typography>
            </Paper>
          )}

          {/* ilişkiler */}
          <Paper variant="outlined" sx={{ borderRadius: R_KART, p: 1.5, mt: 2 }}>
            <Typography sx={{ fontWeight: 800, fontSize: 12.5, color: 'primary.main', mb: 0.5 }}>İlişkiler (join)</Typography>
            {ed?.iliskiler.length === 0 && <Typography sx={{ fontSize: 12 }} color="text.secondary">İlişki yok. Tablo eklendikçe veritabanının FK'ları otomatik önerilir.</Typography>}
            {ed?.iliskiler.map((i, idx) => (
              <Box key={idx} sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 0.5, borderTop: idx ? '1px solid #f0f3f6' : 0, fontSize: 12.5 }}>
                <Typography sx={{ fontSize: 12.5 }}><b>{tableLabel(i.kaynakTabloId)}</b>.{i.kaynakKolon} → <b>{tableLabel(i.hedefTabloId)}</b>.{i.hedefKolon}</Typography>
                <Box component="span" sx={{ fontSize: 10, fontWeight: 700, borderRadius: 1, px: 0.7, py: '1px', bgcolor: i.fktenMi ? 'var(--kds-yesildolgu)' : 'var(--kds-dolgu)', color: i.fktenMi ? 'success.main' : 'text.secondary' }}>{i.fktenMi ? "FK'dan" : 'elle'}</Box>
                <Box sx={{ flexGrow: 1 }} />
                <IconButton size="small" onClick={() => doUpdate((p) => ({ ...p, iliskiler: p.iliskiler.filter((_, j) => j !== idx) }))}><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
              </Box>
            ))}
          </Paper>
        </Box>
      </Box>

      {/* Tablo Ekle arama dialog */}
      <Dialog open={ekleAcik} onClose={() => setEkleAcik(false)} maxWidth="sm" fullWidth slotProps={{ paper: { sx: diyalogKagit } }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, pt: 2 }}>
          <Typography variant="h6" color="primary">Tablo Ekle</Typography>
          <IconButton size="small" onClick={() => setEkleAcik(false)}><CloseIcon fontSize="small" /></IconButton>
        </Box>
        <DialogContent>
          <TextField autoFocus size="small" fullWidth placeholder="Tablo ara… (600 tablo içinde yaz-bul)" value={arama}
            onChange={(e) => { setArama(e.target.value); void ara(e.target.value); }} sx={{ mb: 1 }} />
          <Box sx={{ maxHeight: 320, overflowY: 'auto', bgcolor: 'background.paper' }}>
            {ariyor && <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', color: 'text.secondary', py: 1 }}><CircularProgress size={16} /> Aranıyor…</Box>}
            {!ariyor && sonuc.length === 0 && <Typography sx={{ py: 1, fontSize: 13 }} color="text.secondary">Eşleşme yok.</Typography>}
            {sonuc.map((t) => {
              const ekli = ed?.tablolar.some((x) => x.sema === t.sema && x.hamAd === t.ad);
              return (
                <Box key={`${t.sema}.${t.ad}`} sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 0.8, borderTop: '1px solid', borderColor: 'divider' }}>
                  <Box sx={{ minWidth: 0 }}>
                    <Typography sx={{ fontSize: 13, fontWeight: 700 }}>{t.ad}</Typography>
                    <Typography sx={{ fontSize: 11 }} color="text.secondary">{t.sema} · {t.kolonSayisi} kolon</Typography>
                  </Box>
                  <Box sx={{ flexGrow: 1 }} />
                  {ekli ? <Typography sx={{ fontSize: 11.5 }} color="text.secondary">✓ ekli</Typography>
                    : <Button size="small" variant="contained" color="secondary" disabled={ekleniyor} onClick={() => void addTable(t)}>＋ Ekle</Button>}
                </Box>
              );
            })}
          </Box>
          <Typography sx={{ mt: 1, fontSize: 11.5 }} color="text.disabled">İlk sonuçlar gösterilir; daraltmak için yazın. Tablo eklenince kolonları o an yüklenir (tembel) ve FK ilişkileri önerilir.</Typography>
        </DialogContent>
      </Dialog>
      {confirmDialog}
    </Box>
  );
}
