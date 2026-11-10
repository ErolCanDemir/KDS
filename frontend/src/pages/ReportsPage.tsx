import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import Divider from '@mui/material/Divider';
import Alert from '@mui/material/Alert';
import Tooltip from '@mui/material/Tooltip';
import AddIcon from '@mui/icons-material/Add';
import StarIcon from '@mui/icons-material/Star';
import StarBorderIcon from '@mui/icons-material/StarBorder';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import FolderOutlinedIcon from '@mui/icons-material/FolderOutlined';
import PeopleAltOutlinedIcon from '@mui/icons-material/PeopleAltOutlined';
import ListAltIcon from '@mui/icons-material/ListAlt';
import DesignServicesOutlinedIcon from '@mui/icons-material/DesignServicesOutlined';
import { useSession } from '../identity/sessionContext';
import { kadiYukleniyor, kadiOner } from '../kadi/kadiKanal';
import {
  getReports, deleteReport, setReportFavorite, getFolders, addFolder, deleteFolder,
  type ReportSummary, type FolderDto,
} from '../api/reports';
import { vizDisplay } from '../components/reportVisual';
import { LoadingCurtain } from '../components/Loading';
import { useConfirm } from '../components/ConfirmDialog';
import { PageHeader, bandButtonWhite } from '../components/PageHeader';
import { boxRadius, smallRadius } from '../theme';
import { kadiHata } from '../kadi/kadiKanal';

type Grup = 'tumu' | 'favoriler' | 'paylasilan' | 'klasorler' | { klasorId: string };

/** M36: klasörler EN SON AÇILANA göre sıralanır. "Açılma" kullanıcıya/tarayıcıya ait bir bilgi
 * (sunucuda tutulmaz) → yerel depoda klasör kimliği başına zaman damgası. Hiç açılmamışlar
 * altta, kendi aralarında ada göre. */
const SON_ACILAN_ANAHTAR = 'kds.klasor.sonAcilan';

function sonAcilanlariOku(): Record<string, number> {
  try { return JSON.parse(localStorage.getItem(SON_ACILAN_ANAHTAR) ?? '{}') as Record<string, number>; }
  catch { return {}; }
}

export function ReportsPage({ baslangic = 'tumu' }: { baslangic?: 'tumu' | 'favoriler' | 'paylasilan' | 'klasorler' }) {
  const git = useNavigate();
  const { askConfirm, confirmDialog } = useConfirm();
  const oturum = useSession();
  const yetkili = oturum.roller.includes('admin') || oturum.roller.includes('gelistirici');

  const [raporlar, setRaporlar] = useState<ReportSummary[]>([]);
  const [klasorler, setKlasorler] = useState<FolderDto[]>([]);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [hata, setHata] = useState<string | null>(null);
  const [grup, setGrup] = useState<Grup>(baslangic);

  // Sol menü alt rotaları aynı bileşene gelir — rota değişince grup da değişmeli (test dönüşü #10).
  // M31: "Klasörlerim" artık ilk klasöre sapmıyor; kendi görünümü var (klasör kartları) —
  // "tüm raporlarım ile klasörlerim aynı ekranı veriyor" şikayetinin kökü buydu (klasör yoksa
  // eski davranış hiçbir şeyi değiştirmiyordu). Ray tıklamaları da URL'ye yazar (menü vurgusu eşleşir).
  useEffect(() => { setGrup(baslangic); }, [baslangic]);

  // M36: klasör açıldıkça sırası tazelenir (en son açılan en üstte).
  const [sonAcilan, setSonAcilan] = useState<Record<string, number>>(() => sonAcilanlariOku());
  const klasoruAc = (klasorId: string) => {
    const yeni = { ...sonAcilanlariOku(), [klasorId]: Date.now() };
    setSonAcilan(yeni);
    try { localStorage.setItem(SON_ACILAN_ANAHTAR, JSON.stringify(yeni)); } catch { /* kota/gizli mod: sıralama sadece bu oturumda */ }
    setGrup({ klasorId });
  };
  const sirkaliKlasorler = useMemo(
    () => [...klasorler].sort((a, b) =>
      (sonAcilan[b.id] ?? 0) - (sonAcilan[a.id] ?? 0) || a.ad.localeCompare(b.ad, 'tr')),
    [klasorler, sonAcilan]);

  const [arama, setArama] = useState('');

  // S29/2 devamı: hiç rapor yoksa Kadi Tasarımcıya köprü kurar (oturumda bir kez)
  useEffect(() => {
    if (yukleniyor || raporlar.length > 0 || !yetkili) return;
    const anahtar = 'kds.kadiBos:raporlar';
    if (sessionStorage.getItem(anahtar)) return;
    sessionStorage.setItem(anahtar, '1');
    kadiOner('Henüz rapor yok — ilkini Sorgu Tasarımcısı ile kuralım mı?', null,
      [{ etiket: 'Tasarımcıya git', rota: '/tasarimci' }]);
  }, [yukleniyor, raporlar.length, yetkili]);
  const [yeniKlasorAcik, setYeniKlasorAcik] = useState(false);
  const [yeniKlasorAd, setYeniKlasorAd] = useState('');

  async function refresh() {
    setHata(null);
    try {
      const [r, k] = await Promise.all([getReports(), getFolders()]);
      setRaporlar(r);
      setKlasorler(k);
    } catch (e) { setHata((e as Error).message); kadiHata(); }
    finally { setYukleniyor(false); }
  }
  useEffect(() => { void refresh(); }, []);

  const suzulmus = raporlar.filter((r) => {
    if (arama && !r.ad.toLowerCase().includes(arama.toLowerCase())) return false;
    if (grup === 'tumu' || grup === 'klasorler') return true; // 'klasorler' listeyi değil kartları çizer
    if (grup === 'favoriler') return r.favoriMi;
    if (grup === 'paylasilan') return r.paylasim === 'Ekip' && !r.benimMi;
    return r.klasorId === grup.klasorId;
  });

  async function toggleFavorite(r: ReportSummary) {
    try {
      await setReportFavorite(r.id, !r.favoriMi);
      setRaporlar((p) => p.map((x) => (x.id === r.id ? { ...x, favoriMi: !r.favoriMi } : x)));
    } catch (e) { setHata((e as Error).message); kadiHata(); }
  }
  async function sil(r: ReportSummary) {
    if (!(await askConfirm(`"${r.ad}" raporu silinsin mi?`, { tehlikeli: true, onayYazisi: 'Sil' }))) return;
    try { await deleteReport(r.id); await refresh(); } catch (e) { setHata((e as Error).message); kadiHata(); }
  }
  async function createFolder() {
    if (!yeniKlasorAd.trim()) return;
    const kadiBitir = kadiYukleniyor('Klasörü oluşturuyorum…');
    try {
      await addFolder(yeniKlasorAd.trim());
      setYeniKlasorAd(''); setYeniKlasorAcik(false);
      await refresh();
    } catch (e) { setHata((e as Error).message); kadiHata(); }
    finally { kadiBitir(); }
  }
  async function onDeleteFolder(k: FolderDto) {
    if (!(await askConfirm(`"${k.ad}" klasörü silinsin mi? Raporlar silinmez, klasörsüze düşer.`, { tehlikeli: true, onayYazisi: 'Sil' }))) return;
    try { await deleteFolder(k.id); if (typeof grup === 'object' && grup.klasorId === k.id) setGrup('tumu'); await refresh(); }
    catch (e) { setHata((e as Error).message); kadiHata(); }
  }

  const seciliMi = (g: Grup) =>
    typeof grup === 'object' && typeof g === 'object' ? grup.klasorId === g.klasorId : grup === g;

  if (yukleniyor) {
    return <LoadingCurtain />;
  }

  return (
    <Box>
      <PageHeader ikon={<ListAltIcon />} baslik="Raporlar"
        aciklama="Kayıtlı raporların — aç, çalıştır, yıldızla; tasarımcıda ya da SQL ile düzenle."
        rozetler={[
          { sayi: raporlar.length, ad: 'rapor' },
          { sayi: raporlar.filter((r) => r.favoriMi).length, ad: 'favori' },
          { sayi: klasorler.length, ad: 'klasör' },
        ]}>
        {yetkili && (
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => git('/tasarimci')} sx={bandButtonWhite}>
            Yeni Rapor
          </Button>
        )}
      </PageHeader>

      {hata && <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => setHata(null)}>{hata}</Alert>}

      <Paper variant="outlined" sx={{ borderRadius: boxRadius, display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '196px 1fr' }, overflow: 'hidden' }}>
        {/* sol ray */}
        <Box sx={{ borderRight: { sm: '1px solid' }, borderColor: { sm: 'divider' }, p: 1, bgcolor: 'var(--kds-zemin2)' }}>
          {/* M31: sabit görünümler URL'ye yazar — sol menü vurgusu ekranla eşleşir */}
          <RayOge ikon={<ListAltIcon sx={{ fontSize: 16 }} />} ad="Tüm Raporlar" sayi={raporlar.length} secili={seciliMi('tumu')} tikla={() => git('/raporlar')} />
          <RayOge ikon={<StarIcon sx={{ fontSize: 16, color: 'warning.main' }} />} ad="Favorilerim" sayi={raporlar.filter((r) => r.favoriMi).length} secili={seciliMi('favoriler')} tikla={() => git('/raporlar/favoriler')} />
          <RayOge ikon={<PeopleAltOutlinedIcon sx={{ fontSize: 16 }} />} ad="Ekiple paylaşılan" sayi={raporlar.filter((r) => r.paylasim === 'Ekip' && !r.benimMi).length} secili={seciliMi('paylasilan')} tikla={() => git('/raporlar/paylasilan')} />
          <RayOge ikon={<FolderOutlinedIcon sx={{ fontSize: 16 }} />} ad="Klasörlerim" sayi={klasorler.length} secili={seciliMi('klasorler')} tikla={() => git('/raporlar/klasorler')} />
          <Divider sx={{ my: 1 }} />
          <Typography sx={{ fontSize: 10.5, fontWeight: 800, textTransform: 'uppercase', letterSpacing: 0.4, color: 'text.secondary', px: 1.25, mb: 0.5 }}>Klasörler</Typography>
          {sirkaliKlasorler.map((k) => (
            <Box key={k.id} sx={{ position: 'relative', '&:hover .ksil': { opacity: 1 } }}>
              <RayOge ikon={<FolderOutlinedIcon sx={{ fontSize: 16 }} />} ad={k.ad} sayi={k.raporSayisi} secili={seciliMi({ klasorId: k.id })} tikla={() => klasoruAc(k.id)} />
              {yetkili && (
                <IconButton className="ksil" size="small" onClick={() => void onDeleteFolder(k)}
                  sx={{ position: 'absolute', right: 26, top: 3, p: 0.3, opacity: 0, transition: 'opacity .12s' }}>
                  <DeleteOutlineIcon sx={{ fontSize: 14 }} />
                </IconButton>
              )}
            </Box>
          ))}
          {yetkili && (yeniKlasorAcik ? (
            <Box sx={{ display: 'flex', gap: 0.5, px: 1, py: 0.5 }}>
              <TextField size="small" autoFocus value={yeniKlasorAd} placeholder="Klasör adı"
                onChange={(e) => setYeniKlasorAd(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter') void createFolder(); if (e.key === 'Escape') setYeniKlasorAcik(false); }} />
              <Button size="small" variant="contained" onClick={() => void createFolder()}>＋</Button>
            </Box>
          ) : (
            <Box onClick={() => setYeniKlasorAcik(true)} sx={{ display: 'flex', alignItems: 'center', gap: 0.8, px: 1.25, py: 0.6, borderRadius: smallRadius, cursor: 'pointer', color: 'secondary.main', fontSize: 12.5, fontWeight: 600, '&:hover': { bgcolor: 'var(--kds-dolgu)' } }}>
              <AddIcon sx={{ fontSize: 15 }} /> Yeni klasör
            </Box>
          ))}
        </Box>

        {/* içerik: 'klasorler' görünümünde klasör kartları, diğerlerinde rapor listesi (M31) */}
        {grup === 'klasorler' ? (
          <Box sx={{ p: 1.5 }}>
            {klasorler.length === 0 ? (
              <Box sx={{ py: 4, textAlign: 'center', color: 'text.secondary' }}>
                <Typography sx={{ fontSize: 13 }}>
                  Henüz klasörün yok{yetkili ? <> — soldaki <b>Yeni klasör</b> ile oluştur; raporları kaydederken klasör seçebilirsin.</> : '.'}
                </Typography>
              </Box>
            ) : (
              <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))', gap: 1.25 }}>
                {sirkaliKlasorler.map((k) => (
                  <Paper key={k.id} variant="outlined" onClick={() => klasoruAc(k.id)}
                    sx={{ borderRadius: boxRadius, p: 1.5, display: 'flex', alignItems: 'center', gap: 1.1, cursor: 'pointer', '&:hover': { borderColor: 'secondary.main', bgcolor: 'var(--kds-zemin2)' } }}>
                    <Box sx={{ width: 34, height: 34, borderRadius: '8px', bgcolor: 'var(--kds-dolgu)', color: 'secondary.main', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
                      <FolderOutlinedIcon sx={{ fontSize: 20 }} />
                    </Box>
                    <Box sx={{ minWidth: 0 }}>
                      <Typography sx={{ fontSize: 13, fontWeight: 700 }} noWrap title={k.ad}>{k.ad}</Typography>
                      <Typography sx={{ fontSize: 11 }} color="text.disabled">{k.raporSayisi} rapor</Typography>
                    </Box>
                  </Paper>
                ))}
              </Box>
            )}
          </Box>
        ) : (
        <Box sx={{ p: 1.5 }}>
          <Box sx={{ display: 'flex', gap: 1, mb: 1.25 }}>
            <TextField size="small" placeholder="Rapor ara…" value={arama} onChange={(e) => setArama(e.target.value)} sx={{ maxWidth: 250, flexGrow: 1 }} />
          </Box>
          {suzulmus.length === 0 ? (
            <Box sx={{ py: 4, textAlign: 'center', color: 'text.secondary' }}>
              <Typography sx={{ fontSize: 13 }}>
                {raporlar.length === 0
                  ? <>Henüz rapor yok{yetkili ? <> — <b>Sorgu Tasarımcısı</b>'nda kur, "Rapor Olarak Kaydet" de.</> : '.'}</>
                  : 'Bu görünümde rapor yok.'}
              </Typography>
            </Box>
          ) : (
            <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(225px, 1fr))', gap: 1.25 }}>
              {suzulmus.map((r) => {
                const t = vizDisplay[r.gorsellestirme];
                return (
                  <Paper key={r.id} variant="outlined" sx={{ borderRadius: boxRadius, overflow: 'hidden' }}>
                    <Box sx={{ height: 4, bgcolor: t.renk }} />
                    <Box sx={{ p: 1.25 }}>
                      <Box sx={{ display: 'flex', gap: 0.9, alignItems: 'flex-start' }}>
                        <Box sx={{ width: 28, height: 28, borderRadius: '7px', bgcolor: t.arka, color: t.renk, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>{t.ikon}</Box>
                        <Box sx={{ minWidth: 0, flexGrow: 1 }}>
                          <Typography sx={{ fontSize: 13, fontWeight: 700, lineHeight: 1.25 }} noWrap title={r.ad}>{r.ad}</Typography>
                          <Typography sx={{ fontSize: 10.5 }} color="text.disabled" noWrap>
                            {r.klasorAd ?? 'Klasörsüz'} · {r.benimMi ? 'ben' : (r.sahipAd ?? '—')}{r.paylasim === 'Ekip' ? ' · 👥' : ''}
                          </Typography>
                        </Box>
                        <IconButton size="small" onClick={() => void toggleFavorite(r)} sx={{ p: 0.3 }}>
                          {r.favoriMi ? <StarIcon sx={{ fontSize: 17, color: 'warning.main' }} /> : <StarBorderIcon sx={{ fontSize: 17, color: 'text.disabled' }} />}
                        </IconButton>
                      </Box>
                      <Box sx={{ display: 'flex', gap: 0.6, mt: 1.1 }}>
                        <Button size="small" variant="outlined" fullWidth onClick={() => git(`/raporlar/${r.id}`)}>Aç</Button>
                        {yetkili && (
                          <Tooltip title="Tasarımcıda aç">
                            <IconButton size="small" onClick={() => git(`/tasarimci?rapor=${r.id}`)}><DesignServicesOutlinedIcon sx={{ fontSize: 17 }} /></IconButton>
                          </Tooltip>
                        )}
                        {yetkili && (r.benimMi || oturum.roller.includes('admin')) && (
                          <Tooltip title="Sil"><IconButton size="small" onClick={() => void sil(r)}><DeleteOutlineIcon sx={{ fontSize: 17 }} /></IconButton></Tooltip>
                        )}
                      </Box>
                    </Box>
                  </Paper>
                );
              })}
            </Box>
          )}
        </Box>
        )}
      </Paper>
      {confirmDialog}
    </Box>
  );
}

function RayOge({ ikon, ad, sayi, secili, tikla }: { ikon: ReactNode; ad: string; sayi: number; secili: boolean; tikla: () => void }) {
  return (
    <Box onClick={tikla}
      sx={{ display: 'flex', alignItems: 'center', gap: 0.8, px: 1.25, py: 0.6, borderRadius: smallRadius, cursor: 'pointer', fontSize: 12.5,
        color: secili ? 'secondary.main' : 'text.secondary', fontWeight: secili ? 700 : 500,
        bgcolor: secili ? 'var(--kds-tealdolgu)' : 'transparent', '&:hover': { bgcolor: secili ? 'var(--kds-tealdolgu)' : '#eef3f7' } }}>
      {ikon}
      <Box component="span" sx={{ flexGrow: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{ad}</Box>
      <Box component="span" sx={{ fontSize: 10.5, fontWeight: 700, bgcolor: 'var(--kds-dolgu)', border: '1px solid', borderColor: 'divider', borderRadius: 999, px: 0.8 }}>{sayi}</Box>
    </Box>
  );
}
