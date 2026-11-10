import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Dialog from '@mui/material/Dialog';
import DialogContent from '@mui/material/DialogContent';
import Drawer from '@mui/material/Drawer';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import Tooltip from '@mui/material/Tooltip';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import AddIcon from '@mui/icons-material/Add';
import BoltIcon from '@mui/icons-material/Bolt';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import ViewModuleIcon from '@mui/icons-material/ViewModule';
import StorageIcon from '@mui/icons-material/Storage';
import { PageHeader, bandButtonWhite } from '../components/PageHeader';
import TableRowsIcon from '@mui/icons-material/TableRows';
import CloseIcon from '@mui/icons-material/Close';
import RefreshIcon from '@mui/icons-material/Refresh';
import {
  getConnections, addConnection, updateConnection, deleteConnection,
  testConnectionForm, testConnectionById,
  type Connection, type ConnectionInput, type DataMode,
} from '../api/connections';
import { boxRadius } from '../theme';
import { LoadingCurtain } from '../components/Loading';
import { useConfirm } from '../components/ConfirmDialog';
import { kadiGorevSahnesi, kadiYukleniyor, kadiHata, kadiBasarili, kadiOner } from '../kadi/kadiKanal';
import { getAjanlar, cevrimici, type Ajan } from '../api/ajanlar';

const bosForm: ConnectionInput = { ad: '', sunucu: '', veritabani: '', kullaniciAdi: '', sifre: '', mod: 'Canli', depoBaglantiId: '', ajanId: '' };

type TestDurumu = { durum: 'idle' | 'calisiyor' | 'ok' | 'hata'; ms?: number | null; mesaj?: string | null };

function DurumCip({ basarili, ms }: { basarili: boolean | null; ms: number | null }) {
  const i = basarili == null
    ? { l: 'Test edilmedi', bg: 'var(--kds-dolgu)', fg: 'text.secondary', dot: 'text.disabled' }
    : basarili
      ? { l: ms != null ? `Bağlı · ${ms} ms` : 'Bağlı', bg: 'var(--kds-yesildolgu)', fg: 'success.main', dot: 'success.main' }
      : { l: 'Hata', bg: 'var(--kds-kirmizidolgu)', fg: 'error.main', dot: 'error.main' };
  return (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.7, bgcolor: i.bg, color: i.fg, fontSize: 12, fontWeight: 700, borderRadius: 999, px: 1, py: '3px', whiteSpace: 'nowrap' }}>
      <Box component="span" sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: i.dot }} />{i.l}
    </Box>
  );
}

function ModCip({ mod }: { mod: DataMode }) {
  const canli = mod === 'Canli';
  return (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', bgcolor: canli ? 'var(--kds-tealdolgu)' : 'var(--kds-amberdolgu)', color: canli ? 'secondary.main' : 'warning.main', fontSize: 12, fontWeight: 700, borderRadius: 999, px: 1, py: '3px', whiteSpace: 'nowrap' }}>
      {canli ? 'Canlı' : 'İçe Alım'}
    </Box>
  );
}

export function ConnectionsPage() {
  const { askConfirm, confirmDialog } = useConfirm();
  const [liste, setListe] = useState<Connection[]>([]);
  // S45: ajan seçici için — uç Admin'e açık; geliştirici 403 alır ve liste sessizce boş kalır
  // (seçici yalnız "Ajansız" gösterir; ajan bağlama zaten Admin işi).
  const [ajanlar, setAjanlar] = useState<Ajan[]>([]);
  useEffect(() => { getAjanlar().then(setAjanlar).catch(() => setAjanlar([])); }, []);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [yuklemeHatasi, setYuklemeHatasi] = useState<string | null>(null);
  const [gorunum, setGorunum] = useState<'kart' | 'tablo'>('kart');

  const [yeniAcik, setYeniAcik] = useState(false);
  const [duzenlenen, setDuzenlenen] = useState<Connection | null>(null);
  const [form, setForm] = useState<ConnectionInput>(bosForm);
  const [kaydediyor, setKaydediyor] = useState(false);
  const [formHata, setFormHata] = useState<string | null>(null);
  const [test, setTest] = useState<TestDurumu>({ durum: 'idle' });
  const [hizliTestId, setHizliTestId] = useState<string | null>(null);

  async function refresh() {
    setYukleniyor(true);
    setYuklemeHatasi(null);
    try {
      setListe(await getConnections());
    } catch (h) {
      setYuklemeHatasi((h as Error).message); kadiHata();
    } finally {
      setYukleniyor(false);
    }
  }

  useEffect(() => { void refresh(); }, []);
  // S29/2: boş durumda Kadi yol gösterir (oturumda bir kez)
  useEffect(() => {
    if (yukleniyor || liste.length > 0) return;
    const anahtar = 'kds.kadiBos:baglantilar';
    if (sessionStorage.getItem(anahtar)) return;
    sessionStorage.setItem(anahtar, '1');
    kadiOner('Henüz bağlantı yok — ilk veri kaynağını birlikte bağlayalım mı?', document.querySelector('[data-kadi-hedef="yeni-baglanti"]'));
  }, [yukleniyor, liste.length]);


  function openNew() {
    setForm(bosForm);
    setTest({ durum: 'idle' });
    setFormHata(null);
    setDuzenlenen(null);
    setYeniAcik(true);
  }

  function openEdit(b: Connection) {
    setForm({ ad: b.ad, sunucu: b.sunucu, veritabani: b.veritabani, kullaniciAdi: b.kullaniciAdi ?? '', sifre: '', mod: b.mod, depoBaglantiId: b.depoBaglantiId ?? '', ajanId: b.ajanId ?? '' });
    setTest(b.sonTestBasarili == null ? { durum: 'idle' } : b.sonTestBasarili ? { durum: 'ok', ms: b.sonTestMs } : { durum: 'hata' });
    setFormHata(null);
    setYeniAcik(false);
    setDuzenlenen(b);
  }

  function close() {
    setYeniAcik(false);
    setDuzenlenen(null);
  }

  async function formTest() {
    setTest({ durum: 'calisiyor' });
    // S28c: diyalog kilitliyken Kadi ortada 'test ediyorum' der (merkez perde diyaloğun üstündedir)
    const kadiBitir = kadiYukleniyor('Bağlantıyı test ediyorum…');
    try {
      const s = await testConnectionForm({ sunucu: form.sunucu, veritabani: form.veritabani, kullaniciAdi: form.kullaniciAdi, sifre: form.sifre });
      setTest(s.basarili ? { durum: 'ok', ms: s.ms } : { durum: 'hata', mesaj: s.mesaj });
      if (s.basarili) kadiBasarili('Bağlantı başarılı.'); else kadiHata('Bağlantıyı kuramadım — ayrıntı diyalogda.');
    } catch (h) {
      setTest({ durum: 'hata', mesaj: (h as Error).message });
    } finally {
      kadiBitir();
    }
  }

  async function save() {
    const kadiBitir = kadiYukleniyor('Bağlantıyı kaydediyorum…');
    setKaydediyor(true);
    setFormHata(null);
    try {
      // M22b: '' = varsayılan KDS deposu → null gönderilir; hedef yalnız İçe Alım modunda anlamlı.
      const govde = {
        ...form,
        depoBaglantiId: form.mod === 'IceAlim' && form.depoBaglantiId ? form.depoBaglantiId : null,
        ajanId: form.mod === 'IceAlim' && form.ajanId ? form.ajanId : null,
      };
      const kayit = duzenlenen ? await updateConnection(duzenlenen.id, govde) : await addConnection(govde);
      close();
      kadiBasarili('Kaydettim.');
      await refresh();
      // Form testi kalıcı değildir; kaydedilen yapılandırmayı arka planda gerçek durumuyla
      // eşitle (düzenleme ekranındaki test sonucu ana ekrana da yansısın, "test edilmedi"de
      // takılı kalmasın).
      if (kayit?.id) void testConnectionById(kayit.id).then(() => void refresh()).catch(() => undefined);
    } catch (h) {
      setFormHata((h as Error).message); kadiHata();
    } finally {
      kadiBitir();
      setKaydediyor(false);
    }
  }

  async function hizliTest(b: Connection, hedef: HTMLElement | null = null) {
    setHizliTestId(b.id);
    try {
      // S28: Kadi sahnesi — butona uçar, "test ediyorum" der, sonucu bildirir; iş aynı çağrıdır.
      await kadiGorevSahnesi({
        hedef,
        calisirken: `${b.ad} bağlantısını test ediyorum…`,
        is: () => testConnectionById(b.id),
        sonuc: (s) => s.basarili
          ? { basarili: true, mesaj: `Bağlantı sağlam!${s.ms != null ? ` ${s.ms} ms gecikme.` : ''}` }
          : { basarili: false, mesaj: `Bağlantı kuramadım — ${s.mesaj ?? 'sunucuya ulaşılamadı'}.` },
        hataMesaji: () => 'Bağlantı kuramadım — sunucuya ulaşılamadı.',
      });
      await refresh();
    } catch {
      /* liste yenilenince durum yansır; sessizce geç */
    } finally {
      setHizliTestId(null);
    }
  }

  async function sil(b: Connection) {
    if (!(await askConfirm(`"${b.ad}" bağlantısı silinsin mi?`, { tehlikeli: true, onayYazisi: 'Sil' }))) return;
    const kadiBitir = kadiYukleniyor('Bağlantıyı siliyorum…');
    try {
      await deleteConnection(b.id);
      await refresh();
    } catch (h) {
      setYuklemeHatasi((h as Error).message); kadiHata();
    } finally {
      kadiBitir();
    }
  }

  const gecerli = form.ad.trim() && form.sunucu.trim() && form.veritabani.trim();
  const bagliSayisi = liste.filter((b) => b.sonTestBasarili).length;

  function formIcerik(ikiSutun: boolean) {
    return (
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <TextField label="Bağlantı adı" size="small" fullWidth value={form.ad} onChange={(e) => setForm((f) => ({ ...f, ad: e.target.value }))} placeholder="ör. Üretim MSSQL" />
        <Box sx={{ display: 'grid', gridTemplateColumns: ikiSutun ? '1fr 1fr' : '1fr', gap: 2 }}>
          <TextField label="Sunucu" size="small" value={form.sunucu} onChange={(e) => setForm((f) => ({ ...f, sunucu: e.target.value }))} placeholder="(localdb)\MSSQLLocalDB" />
          <TextField label="Veritabanı" size="small" value={form.veritabani} onChange={(e) => setForm((f) => ({ ...f, veritabani: e.target.value }))} placeholder="KdsMetadata" />
          {/* S25: tarayıcı bu ikiliyi "login formu" sanıp Keycloak'ta kayıtlı e-posta+şifreyi
              dolduruyordu — burası DB bağlantısı, oturum değil. autocomplete kapatılır
              (şifre alanında güvenilir tek yol: new-password) + isimler login kalıbı dışına alınır. */}
          <TextField label="Kullanıcı (boş = entegre)" size="small" value={form.kullaniciAdi}
            onChange={(e) => setForm((f) => ({ ...f, kullaniciAdi: e.target.value }))}
            slotProps={{ htmlInput: { autoComplete: 'off', name: 'kds-db-kullanici' } }} />
          <TextField label="Şifre" size="small" type="password" value={form.sifre}
            onChange={(e) => setForm((f) => ({ ...f, sifre: e.target.value }))}
            placeholder={duzenlenen ? '(değiştirmiyorsan boş bırak)' : ''}
            slotProps={{ htmlInput: { autoComplete: 'new-password', name: 'kds-db-sifre' } }} />
        </Box>
        {/* Kod incelemesi O7: uygulama yalnız SELECT'e izin verir ama login'in DB yetkisini
            ZORLAYAMAZ — operasyonel varsayım kullanıcıya açıkça söylenir. */}
        <Alert severity="warning" variant="outlined" sx={{ fontSize: 12 }}>
          Bu bağlantı için <b>salt-okunur</b> yetkili bir SQL kullanıcısı kullanın. Uygulama yalnız
          SELECT çalıştırır; ancak kullanıcının veritabanı yetkisini sınırlayamaz — geniş yetkili
          bir login, sunucudaki diğer şema/veritabanlarını da okuyabilir.
        </Alert>
        <Box>
          <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: 'text.secondary', mb: 0.8 }}>Veri alma modu</Typography>
          <ToggleButtonGroup exclusive size="small" fullWidth value={form.mod} onChange={(_, v: DataMode | null) => v && setForm((f) => ({ ...f, mod: v }))}>
            <ToggleButton value="Canli">Canlı</ToggleButton>
            <ToggleButton value="IceAlim">İçe Alım</ToggleButton>
          </ToggleButtonGroup>
          <Typography sx={{ fontSize: 11.5, color: 'text.disabled', mt: 0.7 }}>
            Canlı = kaynaktan anlık · İçe Alım = KDS deposundan. Modeller için öntanımlı başlangıç; model bazında değiştirilebilir.
          </Typography>
        </Box>
        {/* M22b: İçe Alım modunda depo HEDEFİ — çekilen tablolar seçilen (Canlı) bağlantının
            veritabanına yazılır ve raporlar oradan okur; boş = KDS deposu (otomatik). */}
        {form.mod === 'IceAlim' && (
          <TextField select label="Depo hedefi" size="small" fullWidth
            value={form.depoBaglantiId ?? ''}
            onChange={(e) => setForm((f) => ({ ...f, depoBaglantiId: e.target.value }))}
            helperText="Çekilen tablolar bu veritabanına yazılır; raporlar da oradan okur. Hedef bağlantının kullanıcısı YAZMA yetkili olmalı.">
            <MenuItem value="">KDS deposu (otomatik)</MenuItem>
            {liste.filter((x) => x.mod === 'Canli' && x.id !== duzenlenen?.id).map((x) => (
              <MenuItem key={x.id} value={x.id}>{x.ad} — {x.sunucu} / {x.veritabani}</MenuItem>
            ))}
          </TextField>
        )}
        {/* S45: İçe Alım kaynağı müşteri ağındaysa AJAN seçilir — sunucu bu kaynağa doğrudan
            bağlanmaz; işleri ajan koşturur, gerçek dize ajanın makinesindeki ajan.json'da durur. */}
        {form.mod === 'IceAlim' && (
          <TextField select label="Ajan (kaynak müşteri ağındaysa)" size="small" fullWidth
            value={form.ajanId ?? ''}
            onChange={(e) => setForm((f) => ({ ...f, ajanId: e.target.value }))}
            helperText={form.ajanId
              ? 'İşleri ajan koşturur; parola alanı boş kalabilir — gerçek dize ajanın makinesinde durur.'
              : 'Boş = KDS bu kaynağa doğrudan bağlanır. Ajanlar Yönetim → Ajanlar ekranında üretilir.'}>
            <MenuItem value="">Ajansız (doğrudan bağlantı)</MenuItem>
            {ajanlar.filter((a) => a.durum === 'gecerli').map((a) => (
              <MenuItem key={a.id} value={a.id}>{a.ad}{cevrimici(a) ? ' · çevrimiçi' : ' · çevrimdışı'}</MenuItem>
            ))}
          </TextField>
        )}
        {test.durum === 'ok' && <Alert severity="success" sx={{ py: 0.2 }}>Bağlantı başarılı{test.ms != null ? ` · ${test.ms} ms` : ''}</Alert>}
        {test.durum === 'hata' && <Alert severity="error" sx={{ py: 0.2 }}>{test.mesaj || 'Bağlanılamadı — sunucu/kimlik bilgilerini kontrol edin.'}</Alert>}
        {formHata && <Alert severity="error" sx={{ py: 0.2 }}>{formHata}</Alert>}
      </Box>
    );
  }

  function aksiyonlar() {
    return (
      <>
        <Button onClick={() => void formTest()} disabled={test.durum === 'calisiyor'} color="secondary"
          startIcon={test.durum === 'calisiyor' ? <CircularProgress size={15} color="inherit" /> : <BoltIcon />}>
          Test Et
        </Button>
        <Box sx={{ flexGrow: 1 }} />
        <Button onClick={close} color="inherit">İptal</Button>
        <Button variant="contained" onClick={() => void save()} disabled={!gecerli || kaydediyor}
          startIcon={kaydediyor ? <CircularProgress size={15} color="inherit" /> : undefined}>
          Kaydet
        </Button>
      </>
    );
  }

  return (
    <Box>
      <PageHeader ikon={<StorageIcon />} baslik="Bağlantılar"
        aciklama="Müşteri veritabanı kaynakların — test et, canlı/içe alım modunu seç."
        rozetler={[{ sayi: liste.length, ad: 'kaynak' }, { sayi: bagliSayisi, ad: 'bağlı' }]}>
        <ToggleButtonGroup size="small" exclusive value={gorunum} onChange={(_, v: 'kart' | 'tablo' | null) => v && setGorunum(v)}
          sx={{ bgcolor: 'rgba(255,255,255,.10)', borderRadius: '8px',
            '& .MuiToggleButton-root': { color: 'rgba(255,255,255,.8)', borderColor: 'rgba(255,255,255,.3)' },
            '& .Mui-selected': { bgcolor: '#fff !important', color: '#0f2d4e !important', fontWeight: 700 } }}>
          <ToggleButton value="kart"><ViewModuleIcon fontSize="small" sx={{ mr: 0.6 }} />Kart</ToggleButton>
          <ToggleButton value="tablo"><TableRowsIcon fontSize="small" sx={{ mr: 0.6 }} />Tablo</ToggleButton>
        </ToggleButtonGroup>
        <Button data-kadi-hedef="yeni-baglanti" variant="contained" startIcon={<AddIcon />} onClick={openNew} sx={bandButtonWhite}>Yeni Bağlantı</Button>
      </PageHeader>

      {yuklemeHatasi && (
        <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" size="small" startIcon={<RefreshIcon />} onClick={() => void refresh()}>Tekrar</Button>}>
          Bağlantılar yüklenemedi: {yuklemeHatasi}
        </Alert>
      )}

      {yukleniyor ? (
        <LoadingCurtain />
      ) : gorunum === 'kart' ? (
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', lg: 'repeat(3, 1fr)' }, gap: 2 }}>
          {liste.map((b) => (
            <Paper key={b.id} variant="outlined" sx={{ borderRadius: boxRadius, overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
              <Box sx={{ height: 5, bgcolor: b.mod === 'Canli' ? '#0e6f7a' : '#c8862a' }} />
              <Box sx={{ p: 2, display: 'flex', flexDirection: 'column', gap: 1, flexGrow: 1 }}>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 1 }}>
                  <Box sx={{ minWidth: 0 }}>
                    <Typography sx={{ fontWeight: 700, fontSize: 14.5 }} color="primary" noWrap>{b.ad}</Typography>
                    <Typography sx={{ fontSize: 12 }} color="text.secondary" noWrap>{b.sunucu} · {b.veritabani}</Typography>
                  </Box>
                  <DurumCip basarili={b.sonTestBasarili} ms={b.sonTestMs} />
                </Box>
                <Box><ModCip mod={b.mod} /></Box>
                <Box sx={{ flexGrow: 1 }} />
                <Box sx={{ display: 'flex', gap: 1, pt: 1, borderTop: '1px solid', borderColor: 'divider' }}>
                  <Button size="small" variant="outlined" fullWidth disabled={hizliTestId === b.id} onClick={() => void hizliTest(b)}
                    startIcon={hizliTestId === b.id ? <CircularProgress size={14} /> : <BoltIcon />}>Test</Button>
                  <Button size="small" variant="outlined" fullWidth startIcon={<EditOutlinedIcon />} onClick={() => openEdit(b)}>Düzenle</Button>
                  <Tooltip title="Sil"><IconButton size="small" onClick={() => void sil(b)}><DeleteOutlineIcon fontSize="small" /></IconButton></Tooltip>
                </Box>
              </Box>
            </Paper>
          ))}
          <Paper variant="outlined" onClick={openNew}
            sx={{ borderRadius: boxRadius, borderStyle: 'dashed', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 1, minHeight: 168, cursor: 'pointer', color: 'secondary.main', '&:hover': { bgcolor: 'action.hover' } }}>
            <Box sx={{ width: 44, height: 44, borderRadius: '50%', bgcolor: 'var(--kds-tealdolgu)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}><AddIcon /></Box>
            <Typography sx={{ fontWeight: 700, fontSize: 14 }}>Yeni Bağlantı</Typography>
          </Paper>
        </Box>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: boxRadius }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Ad</TableCell><TableCell>Sunucu / Veritabanı</TableCell>
                <TableCell>Veri alma modu</TableCell><TableCell>Durum</TableCell>
                <TableCell align="right">İşlemler</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {liste.map((b) => (
                <TableRow key={b.id} hover>
                  <TableCell><Typography sx={{ fontWeight: 700, fontSize: 13.5 }} color="primary">{b.ad}</Typography></TableCell>
                  <TableCell>{b.sunucu} · <Typography component="span" sx={{ fontSize: 12 }} color="text.secondary">{b.veritabani}</Typography></TableCell>
                  <TableCell><ModCip mod={b.mod} /></TableCell>
                  <TableCell><DurumCip basarili={b.sonTestBasarili} ms={b.sonTestMs} /></TableCell>
                  <TableCell align="right">
                    <Tooltip title="Test Et"><span><IconButton size="small" color="secondary" disabled={hizliTestId === b.id} onClick={(e) => void hizliTest(b, e.currentTarget)}>{hizliTestId === b.id ? <CircularProgress size={16} /> : <BoltIcon fontSize="small" />}</IconButton></span></Tooltip>
                    <Tooltip title="Düzenle"><IconButton size="small" onClick={() => openEdit(b)}><EditOutlinedIcon fontSize="small" /></IconButton></Tooltip>
                    <Tooltip title="Sil"><IconButton size="small" onClick={() => void sil(b)}><DeleteOutlineIcon fontSize="small" /></IconButton></Tooltip>
                  </TableCell>
                </TableRow>
              ))}
              {liste.length === 0 && (
                <TableRow><TableCell colSpan={5}><Typography sx={{ py: 2, textAlign: 'center' }} color="text.secondary">Henüz bağlantı yok — “Yeni Bağlantı” ile ekleyin.</Typography></TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Yeni → modal */}
      <Dialog open={yeniAcik} onClose={close} maxWidth="sm" fullWidth slotProps={{ paper: { sx: { borderRadius: boxRadius } } }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, pt: 2 }}>
          <Typography variant="h6" color="primary">Yeni Bağlantı</Typography>
          <IconButton size="small" onClick={close}><CloseIcon fontSize="small" /></IconButton>
        </Box>
        <DialogContent>{yeniAcik && formIcerik(true)}</DialogContent>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, px: 3, pb: 2 }}>{aksiyonlar()}</Box>
      </Dialog>

      {/* Düzenle → sağ çekmece */}
      <Drawer anchor="right" open={duzenlenen != null} onClose={close} slotProps={{ paper: { sx: { width: { xs: '86%', sm: 380 } } } }}>
        <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', p: 2, borderBottom: '1px solid', borderColor: 'divider' }}>
            <Box>
              <Typography sx={{ fontWeight: 700 }} color="primary">Bağlantıyı Düzenle</Typography>
              <Typography sx={{ fontSize: 12 }} color="text.secondary">{duzenlenen?.ad}</Typography>
            </Box>
            <IconButton size="small" onClick={close}><CloseIcon fontSize="small" /></IconButton>
          </Box>
          <Box sx={{ p: 2, flexGrow: 1, overflowY: 'auto', bgcolor: 'background.paper' }}>{duzenlenen && formIcerik(false)}</Box>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, p: 2, borderTop: '1px solid', borderColor: 'divider' }}>{aksiyonlar()}</Box>
        </Box>
      </Drawer>

      <Typography sx={{ mt: 2, fontSize: 12 }} color="text.secondary">
        Parola Data Protection ile şifreli saklanır; “Test Et” hedef veritabanına gerçekten bağlanır (Kullanıcı boşsa entegre kimlik — LocalDB için uygun).
      </Typography>
      {confirmDialog}
    </Box>
  );
}
