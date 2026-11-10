import { useCallback, useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import Chip from '@mui/material/Chip';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import MuiAlert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import Avatar from '@mui/material/Avatar';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import RefreshIcon from '@mui/icons-material/Refresh';
import { PageHeader } from '../components/PageHeader';
import { boxRadius, smallRadius } from '../theme';
import { LoadingCurtain } from '../components/Loading';
import { useConfirm } from '../components/ConfirmDialog';
import {
  getRoller, rolOlustur, rolSil, getRolUyeleri, rolUyeEkle, rolUyeCikar, kullaniciAra, rolDurum,
  type RolOzeti, type RolUyesi, type RolYonetimDurumu,
} from '../api/roller';
import { getFolders, setFolderRoles, type FolderDto } from '../api/reports';
import { getReports, type ReportSummary } from '../api/reports';
import { getSatirKurallari, type SatirKurali } from '../api/satirKurallari';
import { kadiYukleniyor, kadiHata, kadiBasarili } from '../kadi/kadiKanal';

/** S27 — Roles & Yetkiler: rol tanımla, üye ata; rapor/klasör görünürlüğünü role bağla.
 * Roller Keycloak realm rolleridir: burada açılan rol GİRİŞTE token'a yazılır — üye değişikliği
 * kullanıcının SONRAKİ girişinde geçerli olur (ekran her üye işleminde bunu hatırlatır).
 * Düzen mockup'la birebir: solda rol listesi, sağda Üyeler / Erişim / RLS sekmeleri. */
export default function RolesPage() {
  const { askConfirm, confirmDialog } = useConfirm();
  const [durum, setDurum] = useState<RolYonetimDurumu | null>(null);
  const [roller, setRoller] = useState<RolOzeti[] | null>(null);
  const [secili, setSecili] = useState<string | null>(null);
  const [sekme, setSekme] = useState(0);
  const [hata, setHata] = useState<string | null>(null);
  const [bilgi, setBilgi] = useState<string | null>(null);

  // sağ panel verileri
  const [uyeler, setUyeler] = useState<RolUyesi[] | null>(null);
  const [klasorler, setKlasorler] = useState<FolderDto[]>([]);
  const [raporlar, setRaporlar] = useState<ReportSummary[]>([]);
  const [kurallar, setKurallar] = useState<SatirKurali[]>([]);

  // diyaloglar
  const [yeniAcik, setYeniAcik] = useState(false);
  const [yeniAd, setYeniAd] = useState('');
  const [yeniAciklama, setYeniAciklama] = useState('');
  const [uyeAcik, setUyeAcik] = useState(false);
  const [arama, setArama] = useState('');
  const [adaylar, setAdaylar] = useState<RolUyesi[] | null>(null);
  const [mesgul, setMesgul] = useState(false);

  const yenile = useCallback(async () => {
    try {
      setRoller(await getRoller());
      setHata(null);
    } catch (h) {
      setHata((h as Error).message); kadiHata();
      setRoller([]);
    }
  }, []);

  useEffect(() => {
    rolDurum()
      .then((d) => {
        setDurum(d);
        if (d.hazir) void yenile();
        else setRoller([]);
      })
      .catch((h: Error) => { setDurum({ hazir: false, sebep: h.message }); setRoller([]); });
    getFolders().then(setKlasorler).catch(() => setKlasorler([]));
    getReports().then(setRaporlar).catch(() => setRaporlar([]));
    getSatirKurallari().then(setKurallar).catch(() => setKurallar([]));
  }, [yenile]);

  useEffect(() => {
    if (!secili) return;
    setUyeler(null);
    getRolUyeleri(secili).then(setUyeler).catch((h: Error) => { setHata(h.message); kadiHata(); setUyeler([]); });
  }, [secili]);

  const seciliRol = roller?.find((r) => r.ad === secili) ?? null;
  // Bu role açık içerik: rol paylaşımlı raporlar + erişimli klasörler (mockup'taki Erişim sekmesi).
  const rolluKlasorler = klasorler.filter((k) => (k.erisimRolleri ?? []).includes(secili ?? ''));
  const rolluRaporlar = raporlar.filter((r) => (r.paylasimRolleri ?? []).includes(secili ?? ''));
  const rolluKurallar = kurallar.filter((k) =>
    (k.roller ?? '').split(',').map((x) => x.trim()).includes(secili ?? ''));

  async function olustur() {
    setMesgul(true);
    const kadiBitir = kadiYukleniyor('Rolü oluşturuyorum…');
    try {
      await rolOlustur(yeniAd.trim(), yeniAciklama.trim() || null);
      setYeniAcik(false); setYeniAd(''); setYeniAciklama('');
      await yenile();
      setSecili(yeniAd.trim());
      setBilgi('Rol oluşturuldu. Üye atamaları kullanıcıların SONRAKİ girişinde token\'a yansır.'); kadiBasarili();
    } catch (h) { setHata((h as Error).message); kadiHata(); }
    finally { kadiBitir(); setMesgul(false); }
  }

  async function sil(r: RolOzeti) {
    if (!(await askConfirm(
      `"${r.ad}" rolü silinsin mi? Bu rolü kullanan RLS kuralları ve paylaşım listeleri etkisiz kalır.`,
      { tehlikeli: true, onayYazisi: 'Sil' }))) return;
    try {
      await rolSil(r.ad);
      if (secili === r.ad) setSecili(null);
      await yenile();
    } catch (h) { setHata((h as Error).message); kadiHata(); }
  }

  async function uyeIslem(islem: () => Promise<unknown>) {
    setMesgul(true);
    try {
      await islem();
      if (secili) setUyeler(await getRolUyeleri(secili));
      await yenile();
      setBilgi('Kaydedildi — değişiklik kullanıcının SONRAKİ girişinde token\'a yansır.'); kadiBasarili();
    } catch (h) { setHata((h as Error).message); kadiHata(); }
    finally { setMesgul(false); }
  }

  async function klasorErisim(k: FolderDto, ekle: boolean) {
    if (!secili) return;
    const yeni = ekle
      ? [...(k.erisimRolleri ?? []), secili]
      : (k.erisimRolleri ?? []).filter((r) => r !== secili);
    try {
      await setFolderRoles(k.id, yeni);
      setKlasorler(await getFolders());
    } catch (h) { setHata((h as Error).message); kadiHata(); }
  }

  if (durum === null || roller === null) return <LoadingCurtain />;

  return (
    <Box>
      <PageHeader rozetler={[{ sayi: roller.length, ad: 'rol' }]}>
        <Button size="small" startIcon={<RefreshIcon />} onClick={() => void yenile()}>Yenile</Button>
        <Button size="small" variant="contained" startIcon={<AddIcon />}
          disabled={!durum.hazir} onClick={() => setYeniAcik(true)}>Yeni Rol</Button>
      </PageHeader>

      {!durum.hazir && (
        <MuiAlert severity="warning" sx={{ mb: 2, borderRadius: boxRadius }}>
          <b>Rol servisi hazır değil.</b> {durum.sebep}
        </MuiAlert>
      )}
      {hata && <MuiAlert severity="error" sx={{ mb: 2, borderRadius: boxRadius }} onClose={() => setHata(null)}>{hata}</MuiAlert>}
      {bilgi && <MuiAlert severity="success" sx={{ mb: 2, borderRadius: boxRadius }} onClose={() => setBilgi(null)}>{bilgi}</MuiAlert>}

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '300px 1fr' }, gap: 1.5, alignItems: 'start' }}>
        {/* SOL: rol listesi */}
        <Paper variant="outlined" sx={{ borderRadius: boxRadius, overflow: 'hidden' }}>
          {roller.map((r) => (
            <Box key={r.ad} onClick={() => { setSecili(r.ad); setSekme(0); }} sx={{
              display: 'flex', alignItems: 'center', gap: 1, px: 1.75, py: 1.25, cursor: 'pointer',
              borderBottom: '1px solid', borderColor: 'divider',
              bgcolor: secili === r.ad ? 'var(--kds-tealdolgu)' : undefined,
              boxShadow: secili === r.ad ? 'inset 3px 0 0 var(--kds-mor)' : undefined,
              '&:hover': { bgcolor: 'var(--kds-dolgu)' },
            }}>
              <Box sx={{ minWidth: 0, flex: 1 }}>
                {/* component="div": Chip div çizer, p içine giremez (React DOM uyarısı) */}
                <Typography component="div" sx={{ fontSize: 13.5, fontWeight: 700 }}>
                  {r.ad}{' '}
                  {r.sistem && <Chip label="sistem" size="small" sx={{ height: 17, fontSize: 9.5, fontWeight: 700 }} />}
                </Typography>
                {r.aciklama && <Typography noWrap sx={{ fontSize: 11.5 }} color="text.secondary">{r.aciklama}</Typography>}
              </Box>
              <Box sx={{ textAlign: 'right' }}>
                <Typography sx={{ fontSize: 13.5, fontWeight: 700 }}>{r.uyeSayisi}</Typography>
                <Typography sx={{ fontSize: 10.5 }} color="text.secondary">üye</Typography>
              </Box>
              {!r.sistem && (
                <IconButton size="small" title="Sil" onClick={(e) => { e.stopPropagation(); void sil(r); }}>
                  <DeleteOutlineIcon sx={{ fontSize: 17 }} />
                </IconButton>
              )}
            </Box>
          ))}
          {roller.length === 0 && (
            <Typography sx={{ p: 2.5, fontSize: 13, textAlign: 'center' }} color="text.secondary">
              {durum.hazir ? 'Rol yok.' : 'Rol servisi yapılandırılınca roller burada listelenir.'}
            </Typography>
          )}
        </Paper>

        {/* SAĞ: seçili rol detayı */}
        <Paper variant="outlined" sx={{ borderRadius: boxRadius, minHeight: 380, display: 'flex', flexDirection: 'column' }}>
          {!seciliRol ? (
            <Typography sx={{ m: 'auto', fontSize: 13.5 }} color="text.secondary">
              Soldan bir rol seç — üyelerini ve erişimlerini burada yönetirsin.
            </Typography>
          ) : (
            <>
              <Box sx={{ px: 2, pt: 1.75, pb: 1 }}>
                <Typography component="div" sx={{ fontSize: 16.5, fontWeight: 800 }}>
                  {seciliRol.ad}{' '}
                  <Chip size="small" label={seciliRol.sistem ? 'sistem rolü' : 'özel rol'} color={seciliRol.sistem ? 'default' : 'secondary'}
                    sx={{ height: 19, fontSize: 10.5, fontWeight: 700 }} />
                </Typography>
                <Typography sx={{ fontSize: 12 }} color="text.secondary">
                  Keycloak realm rolü — girişte token'a yazılır; RLS kuralları ve paylaşım listeleri bu ada bakar.
                </Typography>
              </Box>
              <Tabs value={sekme} onChange={(_, v) => setSekme(v)} sx={{ px: 1, borderBottom: '1px solid', borderColor: 'divider', minHeight: 38 }}>
                <Tab label={`Üyeler (${uyeler?.length ?? '…'})`} sx={{ minHeight: 38, fontSize: 12.5, fontWeight: 700 }} />
                <Tab label="Rapor / Klasör Erişimi" sx={{ minHeight: 38, fontSize: 12.5, fontWeight: 700 }} />
                <Tab label={`RLS Kuralları (${rolluKurallar.length})`} sx={{ minHeight: 38, fontSize: 12.5, fontWeight: 700 }} />
              </Tabs>

              <Box sx={{ p: 2, flex: 1 }}>
                {sekme === 0 && (
                  <UyelerSekmesi uyeler={uyeler} mesgul={mesgul}
                    ekle={() => { setArama(''); setAdaylar(null); setUyeAcik(true); }}
                    cikar={(u) => void uyeIslem(() => rolUyeCikar(seciliRol.ad, u.keycloakId))} />
                )}
                {sekme === 1 && (
                  <ErisimSekmesi rol={seciliRol.ad} klasorler={klasorler} rolluKlasorler={rolluKlasorler}
                    rolluRaporlar={rolluRaporlar} klasorErisim={(k, e) => void klasorErisim(k, e)} />
                )}
                {sekme === 2 && <RlsSekmesi kurallar={rolluKurallar} />}
              </Box>
            </>
          )}
        </Paper>
      </Box>

      {/* Yeni rol */}
      <Dialog open={yeniAcik} onClose={() => setYeniAcik(false)} maxWidth="xs" fullWidth>
        <DialogTitle sx={{ fontWeight: 800 }}>Yeni Rol</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '8px !important' }}>
          <TextField label="Rol adı" size="small" fullWidth autoFocus value={yeniAd}
            onChange={(e) => setYeniAd(e.target.value)} placeholder="ör. bolge_muduru"
            helperText="2-50 karakter; küçük harf, rakam, - ve _. Ad; token'a, RLS'e ve paylaşım listelerine aynen yazılır." />
          <TextField label="Açıklama (isteğe bağlı)" size="small" fullWidth value={yeniAciklama}
            onChange={(e) => setYeniAciklama(e.target.value)} slotProps={{ htmlInput: { maxLength: 300 } }} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setYeniAcik(false)}>Vazgeç</Button>
          <Button variant="contained" disabled={mesgul || !yeniAd.trim()} onClick={() => void olustur()}>Oluştur</Button>
        </DialogActions>
      </Dialog>

      {/* Üye ekle */}
      <Dialog open={uyeAcik} onClose={() => setUyeAcik(false)} maxWidth="xs" fullWidth>
        <DialogTitle sx={{ fontWeight: 800 }}>Üye Ekle — {secili}</DialogTitle>
        <DialogContent sx={{ pt: '8px !important' }}>
          <TextField size="small" fullWidth autoFocus placeholder="Kullanıcı adı / e-posta ara…" value={arama}
            onChange={(e) => setArama(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') void kullaniciAra(arama).then(setAdaylar).catch(() => setAdaylar([])); }} />
          <Button size="small" sx={{ mt: 1 }} onClick={() => void kullaniciAra(arama).then(setAdaylar).catch(() => setAdaylar([]))}>
            Ara
          </Button>
          <Box sx={{ mt: 1, maxHeight: 260, overflowY: 'auto', bgcolor: 'background.paper' }}>
            {adaylar?.map((a) => {
              const zatenUye = (uyeler ?? []).some((u) => u.keycloakId === a.keycloakId);
              return (
                <Box key={a.keycloakId} sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 0.9, borderBottom: '1px solid', borderColor: 'divider' }}>
                  <Avatar sx={{ width: 26, height: 26, fontSize: 11.5, fontWeight: 700 }}>
                    {a.kullaniciAdi.charAt(0).toUpperCase()}
                  </Avatar>
                  <Box sx={{ flex: 1, minWidth: 0 }}>
                    <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{a.kullaniciAdi}</Typography>
                    <Typography noWrap sx={{ fontSize: 11 }} color="text.secondary">{a.ePosta ?? a.adSoyad ?? '—'}</Typography>
                  </Box>
                  <Button size="small" variant="outlined" disabled={mesgul || zatenUye}
                    onClick={() => void uyeIslem(() => rolUyeEkle(secili!, a.keycloakId)).then(() => setUyeAcik(false))}>
                    {zatenUye ? 'Üye' : 'Ekle'}
                  </Button>
                </Box>
              );
            })}
            {adaylar !== null && adaylar.length === 0 && (
              <Typography sx={{ py: 2, fontSize: 12.5, textAlign: 'center' }} color="text.secondary">
                Eşleşen kullanıcı yok (yalnız kendi kiracının kullanıcıları aranır).
              </Typography>
            )}
          </Box>
        </DialogContent>
        <DialogActions><Button onClick={() => setUyeAcik(false)}>Kapat</Button></DialogActions>
      </Dialog>

      {confirmDialog}
    </Box>
  );
}

function UyelerSekmesi({ uyeler, mesgul, ekle, cikar }: {
  uyeler: RolUyesi[] | null; mesgul: boolean; ekle: () => void; cikar: (u: RolUyesi) => void;
}) {
  if (uyeler === null) return <Box sx={{ textAlign: 'center', py: 3 }}><CircularProgress size={20} /></Box>;
  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'flex-end', mb: 1 }}>
        <Button size="small" variant="outlined" startIcon={<AddIcon />} onClick={ekle}>Üye Ekle</Button>
      </Box>
      {uyeler.map((u) => (
        <Box key={u.keycloakId} sx={{ display: 'flex', alignItems: 'center', gap: 1.25, py: 1, borderBottom: '1px solid', borderColor: 'divider' }}>
          <Avatar sx={{ width: 28, height: 28, fontSize: 12, fontWeight: 700 }}>
            {u.kullaniciAdi.charAt(0).toUpperCase()}
          </Avatar>
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography sx={{ fontSize: 13.5, fontWeight: 600 }}>{u.kullaniciAdi}</Typography>
            <Typography noWrap sx={{ fontSize: 11.5 }} color="text.secondary">{u.ePosta ?? u.adSoyad ?? '—'}</Typography>
          </Box>
          <Button size="small" variant="outlined" disabled={mesgul} onClick={() => cikar(u)}>Çıkar</Button>
        </Box>
      ))}
      {uyeler.length === 0 && (
        <Typography sx={{ py: 2.5, fontSize: 13, textAlign: 'center' }} color="text.secondary">Bu rolün üyesi yok.</Typography>
      )}
    </Box>
  );
}

function ErisimSekmesi({ rol, klasorler, rolluKlasorler, rolluRaporlar, klasorErisim }: {
  rol: string; klasorler: FolderDto[]; rolluKlasorler: FolderDto[]; rolluRaporlar: ReportSummary[];
  klasorErisim: (k: FolderDto, ekle: boolean) => void;
}) {
  const eklenebilir = klasorler.filter((k) => !(k.erisimRolleri ?? []).includes(rol));
  return (
    <Box>
      <Paper variant="outlined" sx={{ p: 1.25, borderRadius: smallRadius, bgcolor: 'var(--kds-dolgu)', mb: 1.5 }}>
        <Typography sx={{ fontSize: 12 }} color="text.secondary">
          Bu role açık içerik. Klasör erişimi klasördeki <b>özel olmayan</b> tüm raporları açar; rapor
          erişimi rapor kaydedilirken "Belirli roller" seçimiyle verilir — iki taraf aynı kaydı görür.
        </Typography>
      </Paper>

      <Typography sx={{ fontSize: 12.5, fontWeight: 700, mb: 0.5 }}>Klasörler (buradan yönetilir)</Typography>
      {rolluKlasorler.map((k) => (
        <Box key={k.id} sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 0.9, borderBottom: '1px solid', borderColor: 'divider' }}>
          <Chip size="small" label="Klasör" sx={{ height: 20, fontSize: 10.5, fontWeight: 700 }} />
          <Typography sx={{ fontSize: 13, fontWeight: 600, flex: 1 }}>{k.ad} · {k.raporSayisi} rapor</Typography>
          <Button size="small" variant="outlined" onClick={() => klasorErisim(k, false)}>Kaldır</Button>
        </Box>
      ))}
      {rolluKlasorler.length === 0 && (
        <Typography sx={{ fontSize: 12, py: 0.75 }} color="text.secondary">Bu role açık klasör yok.</Typography>
      )}
      {eklenebilir.length > 0 && (
        <Box sx={{ display: 'flex', gap: 0.75, flexWrap: 'wrap', mt: 1 }}>
          {eklenebilir.map((k) => (
            <Chip key={k.id} size="small" variant="outlined" label={`+ ${k.ad}`} onClick={() => klasorErisim(k, true)}
              sx={{ fontSize: 11.5, fontWeight: 600 }} />
          ))}
        </Box>
      )}

      <Typography sx={{ fontSize: 12.5, fontWeight: 700, mt: 2, mb: 0.5 }}>
        Raporlar (rapor paylaşımından)
      </Typography>
      {rolluRaporlar.map((r) => (
        <Box key={r.id} sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 0.9, borderBottom: '1px solid', borderColor: 'divider' }}>
          <Chip size="small" label="Rapor" color="secondary" variant="outlined" sx={{ height: 20, fontSize: 10.5, fontWeight: 700 }} />
          <Typography sx={{ fontSize: 13, fontWeight: 600, flex: 1 }}>{r.ad}</Typography>
          <Typography sx={{ fontSize: 11.5 }} color="text.secondary">{r.klasorAd ?? 'Klasörsüz'}</Typography>
        </Box>
      ))}
      {rolluRaporlar.length === 0 && (
        <Typography sx={{ fontSize: 12, py: 0.75 }} color="text.secondary">
          Bu role doğrudan paylaşılmış rapor yok (rapor kaydederken "Belirli roller"den verilir).
        </Typography>
      )}
    </Box>
  );
}

function RlsSekmesi({ kurallar }: { kurallar: SatirKurali[] }) {
  return (
    <Box>
      <Paper variant="outlined" sx={{ p: 1.25, borderRadius: smallRadius, bgcolor: 'var(--kds-dolgu)', mb: 1.5 }}>
        <Typography sx={{ fontSize: 12 }} color="text.secondary">
          Bu rolü kapsayan satır-düzeyi güvenlik kuralları — Satır Güvenliği ekranından yönetilir;
          burada salt görünüm.
        </Typography>
      </Paper>
      {kurallar.map((k) => (
        <Box key={k.id} sx={{ py: 0.9, borderBottom: '1px solid', borderColor: 'divider' }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700 }}>{k.ad}</Typography>
          <Typography sx={{ fontSize: 11.5, fontFamily: 'Consolas, monospace' }} color="text.secondary">
            {k.tabloAdi}.{k.kolon} — kaynak: {k.degerKaynagi}{k.deger ? ` (${k.deger})` : ""}
          </Typography>
        </Box>
      ))}
      {kurallar.length === 0 && (
        <Typography sx={{ fontSize: 12.5, py: 1 }} color="text.secondary">Bu rolü kapsayan kural yok.</Typography>
      )}
    </Box>
  );
}
