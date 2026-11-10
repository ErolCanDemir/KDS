import { useEffect, useMemo, useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import Switch from '@mui/material/Switch';
import IconButton from '@mui/material/IconButton';
import FormControlLabel from '@mui/material/FormControlLabel';
import Dialog from '@mui/material/Dialog';
import DialogContent from '@mui/material/DialogContent';
import AddIcon from '@mui/icons-material/Add';
import CloseIcon from '@mui/icons-material/Close';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import ShieldOutlinedIcon from '@mui/icons-material/ShieldOutlined';
import { PageHeader, bandButtonWhite } from '../components/PageHeader';
import { LoadingCurtain } from '../components/Loading';
import { boxRadius, smallRadius } from '../theme';
import { getDataMaps, getDataMap, type DataMapSummary, type DataMapDetail } from '../api/dataMaps';
import {
  getSatirKurallari, saveSatirKurali, deleteSatirKurali,
  type SatirKurali, type KuralDegerKaynagi,
} from '../api/satirKurallari';
import type { ComparisonOp } from '../api/queries';
import { opLabel, opOrder } from './designer/shared';
import { kadiYukleniyor, kadiHata, kadiBasarili } from '../kadi/kadiKanal';

/** Değer kaynaklarının EKRANDAKİ karşılığı — yönetici "claim" gibi teknik sözcüklerle değil,
 * kaynağın ne işe yaradığıyla karşılaşsın. */
const kaynaklar: { deger: KuralDegerKaynagi; etiket: string; aciklama: string; degerEtiketi?: string }[] = [
  {
    deger: 'TokenIddiasi', etiket: 'Kullanıcının kimlik bilgisinden',
    aciklama: 'Değer, kullanıcının giriş bilgisinde (SSO) tanımlı bir nitelikten okunur — ör. “servis_kodu”. Böylece her kullanıcı için ayrı kural yazmanız gerekmez.',
    degerEtiketi: 'Nitelik adı (ör. servis_kodu)',
  },
  {
    deger: 'SabitDeger', etiket: 'Sabit değer',
    aciklama: 'Kuralda yazan değerle karşılaştırılır. Birden çok değeri virgülle ayırın.',
    degerEtiketi: 'Değer(ler) — virgülle',
  },
  {
    deger: 'KullaniciAdi', etiket: 'Kullanıcı adı (yalnız kendi kayıtları)',
    aciklama: 'Kolon, giriş yapan kullanıcının adıyla eşleşen satırları bırakır.',
  },
  {
    deger: 'EPosta', etiket: 'E-posta (yalnız kendi kayıtları)',
    aciklama: 'Kolon, giriş yapan kullanıcının e-postasıyla eşleşen satırları bırakır.',
  },
];

type Taslak = {
  id?: string; veriHaritasiId: string; haritaTabloId: string; ad: string; kolon: string;
  op: ComparisonOp; degerKaynagi: KuralDegerKaynagi; deger: string; roller: string; aktif: boolean;
};

const bosTaslak: Taslak = {
  veriHaritasiId: '', haritaTabloId: '', ad: '', kolon: '', op: 'Esit',
  degerKaynagi: 'TokenIddiasi', deger: '', roller: '', aktif: true,
};

/** Yönetim → Satır Güvenliği (RLS). Kiracı yalıtımı zaten var; buradaki kurallar kiracının
 * İÇİNDE kullanıcı/rol bazlı daraltma yapar. Kurallar sorgu ÜRETİMİNDE uygulanır: kullanıcı
 * kaldıramaz, filtresini VEYA ile atlatamaz, birleşimle (UNION) kaçamaz. */
export function RowSecurityPage() {
  const [kurallar, setKurallar] = useState<SatirKurali[]>([]);
  const [haritalar, setHaritalar] = useState<DataMapSummary[]>([]);
  const [detay, setDetay] = useState<DataMapDetail | null>(null);
  const [taslak, setTaslak] = useState<Taslak | null>(null);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [hata, setHata] = useState<string | null>(null);
  const [basari, setBasari] = useState<string | null>(null);

  async function yukle() {
    setHata(null);
    try {
      const [k, h] = await Promise.all([getSatirKurallari(), getDataMaps()]);
      setKurallar(k);
      setHaritalar(h);
    } catch (e) { setHata((e as Error).message); kadiHata(); }
    finally { setYukleniyor(false); }
  }
  useEffect(() => { void yukle(); }, []);

  // Taslakta harita değişince o haritanın tabloları/kolonları gelsin.
  const secilenHarita = taslak?.veriHaritasiId;
  useEffect(() => {
    if (!secilenHarita) { setDetay(null); return; }
    void (async () => {
      try { setDetay(await getDataMap(secilenHarita)); }
      catch (e) { setHata((e as Error).message); kadiHata(); }
    })();
  }, [secilenHarita]);

  const secilenTablo = useMemo(
    () => detay?.tablolar.find((t) => t.id === taslak?.haritaTabloId) ?? null,
    [detay, taslak?.haritaTabloId]);
  const kaynak = kaynaklar.find((k) => k.deger === taslak?.degerKaynagi);

  async function kaydet() {
    if (!taslak) return;
    setHata(null);
    const kadiBitir = kadiYukleniyor('Kuralı kaydediyorum…');
    try {
      await saveSatirKurali({
        id: taslak.id ?? null,
        veriHaritasiId: taslak.veriHaritasiId, haritaTabloId: taslak.haritaTabloId,
        ad: taslak.ad, kolon: taslak.kolon, op: taslak.op,
        degerKaynagi: taslak.degerKaynagi, deger: taslak.deger.trim() || null,
        roller: taslak.roller.trim() || null, aktif: taslak.aktif,
      });
      setTaslak(null);
      setBasari('Kural kaydedildi — bundan sonraki çalıştırmalarda geçerli.'); kadiBasarili();
      await yukle();
    } catch (e) { setHata((e as Error).message); kadiHata(); }
    finally { kadiBitir(); }
  }

  async function sil(id: string) {
    setHata(null);
    const kadiBitir = kadiYukleniyor('Kuralı siliyorum…');
    try { await deleteSatirKurali(id); await yukle(); }
    catch (e) { setHata((e as Error).message); kadiHata(); }
    finally { kadiBitir(); }
  }

  if (yukleniyor) return <LoadingCurtain />;

  return (
    <Box>
      <PageHeader
        ikon={<ShieldOutlinedIcon />}
        baslik="Satır Güvenliği (RLS)"
        aciklama="Kullanıcı bir tablonun YALNIZ belirli satırlarını görsün — “bayi yalnız kendi servisinin taleplerini görür” gibi. Kural sorgu üretilirken eklenir: kullanıcı kaldıramaz, filtresini değiştirerek atlatamaz."
        rozetler={[{ sayi: kurallar.length, ad: 'kural' }, { sayi: kurallar.filter((k) => k.aktif).length, ad: 'aktif' }]}>
        <Button sx={bandButtonWhite} startIcon={<AddIcon />} onClick={() => setTaslak({ ...bosTaslak })}>
          Yeni Kural
        </Button>
      </PageHeader>

      {hata && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setHata(null)}>{hata}</Alert>}
      {basari && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setBasari(null)}>{basari}</Alert>}

      <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 2, mb: 2 }}>
        <Typography sx={{ fontSize: 12.5 }} color="text.secondary">
          <b>Nasıl çalışır?</b> Kural bir tabloyu kısıtlar. Sorguda o tablo kullanılıyorsa kural
          otomatik eklenir; kullanılmıyorsa bir şey değişmez (o tablodan zaten satır dönmez).
          <b> Yönetici (admin) kurallardan muaftır.</b> Kuralın istediği bilgi kullanıcının
          kimliğinde yoksa sorgu <b>çalıştırılmaz</b> — sessizce filtresiz çalıştırmak kullanıcıya
          her şeyi göstermek olurdu. Kurala tabi kullanıcılar <b>elle SQL</b> çalıştıramaz ve ham
          SQL ile kurulmuş raporları açamaz (o yolda kural uygulanamıyor).
        </Typography>
      </Paper>

      {kurallar.length === 0 ? (
        <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary">
            Henüz kural yok — herkes, yetkisi olduğu raporlarda tüm satırları görüyor.
          </Typography>
        </Paper>
      ) : (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
          {kurallar.map((k) => (
            <Paper key={k.id} variant="outlined"
              sx={{ borderRadius: boxRadius, p: 1.5, display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
              <Box sx={{ flex: '1 1 340px', minWidth: 0 }}>
                <Typography sx={{ fontSize: 13.5, fontWeight: 700 }}>
                  {k.ad}
                  {!k.aktif && <Box component="span" sx={{ ml: 1, fontSize: 11, color: 'text.disabled' }}>(pasif)</Box>}
                </Typography>
                <Typography sx={{ fontSize: 12 }} color="text.secondary">
                  {k.veriHaritasiAdi} · <b>{k.tabloAdi}</b> tablosunda{' '}
                  <Box component="code" sx={{ bgcolor: 'var(--kds-dolgu)', px: 0.5, borderRadius: '4px' }}>
                    {k.kolon} {opLabel[k.op]} {kaynaklar.find((x) => x.deger === k.degerKaynagi)?.etiket}
                    {k.deger ? ` (${k.deger})` : ''}
                  </Box>
                </Typography>
                <Typography sx={{ fontSize: 11 }} color="text.disabled">
                  {k.roller?.trim() ? `Yalnız şu roller: ${k.roller}` : 'Yönetici dışında herkese uygulanır'}
                </Typography>
              </Box>
              <Button size="small" onClick={() => setTaslak({
                id: k.id, veriHaritasiId: k.veriHaritasiId, haritaTabloId: k.haritaTabloId,
                ad: k.ad, kolon: k.kolon, op: k.op, degerKaynagi: k.degerKaynagi,
                deger: k.deger ?? '', roller: k.roller ?? '', aktif: k.aktif,
              })}>Düzenle</Button>
              <IconButton size="small" title="Kuralı sil" onClick={() => void sil(k.id)}>
                <DeleteOutlineIcon sx={{ fontSize: 18 }} />
              </IconButton>
            </Paper>
          ))}
        </Box>
      )}

      {taslak && (
        <Dialog open onClose={() => setTaslak(null)} maxWidth="sm" fullWidth
          slotProps={{ paper: { sx: { borderRadius: boxRadius } } }}>
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, pt: 2 }}>
            <Typography variant="h6" color="primary">{taslak.id ? 'Kuralı Düzenle' : 'Yeni Satır Kuralı'}</Typography>
            <IconButton size="small" onClick={() => setTaslak(null)}><CloseIcon fontSize="small" /></IconButton>
          </Box>
          <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.75 }}>
            <TextField size="small" label="Kural adı" fullWidth value={taslak.ad}
              placeholder="ör. Bayi kapsamı"
              helperText="Hata mesajlarında bu ad görünür — hangi kuralın engellediği anlaşılsın."
              onChange={(e) => setTaslak({ ...taslak, ad: e.target.value })} />

            <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 1.5 }}>
              <Select size="small" displayEmpty value={taslak.veriHaritasiId}
                onChange={(e) => setTaslak({ ...taslak, veriHaritasiId: e.target.value, haritaTabloId: '', kolon: '' })}>
                <MenuItem value="" disabled><em>Veri haritası…</em></MenuItem>
                {haritalar.map((h) => <MenuItem key={h.id} value={h.id}>{h.ad}</MenuItem>)}
              </Select>
              <Select size="small" displayEmpty value={taslak.haritaTabloId} disabled={!detay}
                onChange={(e) => setTaslak({ ...taslak, haritaTabloId: e.target.value, kolon: '' })}>
                <MenuItem value="" disabled><em>Kısıtlanacak tablo…</em></MenuItem>
                {(detay?.tablolar ?? []).map((t) => <MenuItem key={t.id} value={t.id}>{t.dostcaAd || t.hamAd}</MenuItem>)}
              </Select>
            </Box>

            <Box sx={{ display: 'flex', gap: 1.5, alignItems: 'center', flexWrap: 'wrap' }}>
              <Select size="small" displayEmpty value={taslak.kolon} disabled={!secilenTablo} sx={{ minWidth: 190 }}
                onChange={(e) => setTaslak({ ...taslak, kolon: e.target.value })}>
                <MenuItem value="" disabled><em>Hangi kolon…</em></MenuItem>
                {(secilenTablo?.kolonlar ?? []).map((c) => <MenuItem key={c.hamAd} value={c.hamAd}>{c.hamAd}</MenuItem>)}
              </Select>
              <Select size="small" value={taslak.op} sx={{ minWidth: 110 }}
                onChange={(e) => setTaslak({ ...taslak, op: e.target.value as ComparisonOp })}>
                {opOrder.filter((o) => o !== 'Icerir').map((o) => <MenuItem key={o} value={o}>{opLabel[o]}</MenuItem>)}
              </Select>
            </Box>

            <Box>
              <Select size="small" fullWidth value={taslak.degerKaynagi}
                onChange={(e) => setTaslak({ ...taslak, degerKaynagi: e.target.value as KuralDegerKaynagi, deger: '' })}>
                {kaynaklar.map((k) => <MenuItem key={k.deger} value={k.deger}>{k.etiket}</MenuItem>)}
              </Select>
              <Typography sx={{ fontSize: 11.5, mt: 0.5 }} color="text.secondary">{kaynak?.aciklama}</Typography>
            </Box>

            {kaynak?.degerEtiketi && (
              <TextField size="small" fullWidth label={kaynak.degerEtiketi} value={taslak.deger}
                onChange={(e) => setTaslak({ ...taslak, deger: e.target.value })} />
            )}

            <TextField size="small" fullWidth label="Hangi roller (boş = yönetici dışında herkes)"
              placeholder="musteri, gelistirici" value={taslak.roller}
              helperText="Virgülle ayırın. Yönetici (admin) her hâlükârda muaftır."
              onChange={(e) => setTaslak({ ...taslak, roller: e.target.value })} />

            <FormControlLabel
              control={<Switch size="small" checked={taslak.aktif}
                onChange={(e) => setTaslak({ ...taslak, aktif: e.target.checked })} />}
              label={<Typography sx={{ fontSize: 12.5 }}>Kural aktif</Typography>} />

            <Box sx={{ bgcolor: 'var(--kds-amberdolgu)', border: '1px solid #fed7aa', borderRadius: smallRadius, p: 1.25 }}>
              <Typography sx={{ fontSize: 11.5 }} color="text.secondary">
                Kaydettikten sonra <b>bir kullanıcıyla deneyin</b>: kuralın istediği bilgi o
                kullanıcının kimliğinde yoksa sorgu çalışmaz ve mesajda bu kuralın adı görünür.
              </Typography>
            </Box>

            <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1 }}>
              <Button color="inherit" onClick={() => setTaslak(null)}>İptal</Button>
              <Button variant="contained" onClick={() => void kaydet()}
                disabled={!taslak.veriHaritasiId || !taslak.haritaTabloId || !taslak.kolon || !taslak.ad.trim()}>
                Kaydet
              </Button>
            </Box>
          </DialogContent>
        </Dialog>
      )}
    </Box>
  );
}
