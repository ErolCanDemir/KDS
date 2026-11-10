import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import CircularProgress from '@mui/material/CircularProgress';
import Tooltip from '@mui/material/Tooltip';
import DashboardIcon from '@mui/icons-material/Dashboard';
import AddIcon from '@mui/icons-material/Add';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import PeopleAltOutlinedIcon from '@mui/icons-material/PeopleAltOutlined';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import AssessmentOutlinedIcon from '@mui/icons-material/AssessmentOutlined';
import HomeIcon from '@mui/icons-material/Home';
import HomeOutlinedIcon from '@mui/icons-material/HomeOutlined';
import { PageHeader, bandButtonWhite } from '../components/PageHeader';
import { DashboardEditor } from '../components/DashboardEditor';
import { useConfirm } from '../components/ConfirmDialog';
import { useSession } from '../identity/sessionContext';
import { kadiOner } from '../kadi/kadiKanal';
import { getDashboards, getDashboard, deleteDashboard, setHomeDashboard, type DashboardSummary, type DashboardDetail } from '../api/dashboards';
import { boxRadius } from '../theme';
import { kadiHata, kadiYukleniyor } from '../kadi/kadiKanal';

/** Panolar galerisi (S11): kullanıcının görebildiği panoları kart olarak listeler; oluştur/aç/
 * düzenle/sil. Kart açılınca pano görüntüleyicisine (/panolar/:id) gider. */
export function DashboardsPage() {
  const git = useNavigate();
  const oturum = useSession();
  const yetkili = oturum.roller.includes('admin') || oturum.roller.includes('gelistirici');
  const { askConfirm, confirmDialog } = useConfirm();

  const [panolar, setPanolar] = useState<DashboardSummary[] | null>(null);
  const [hata, setHata] = useState<string | null>(null);
  const [editor, setEditor] = useState<DashboardDetail | 'yeni' | null>(null);

  // S29/2 devamı: boş durumda Kadi yol gösterir (oturumda bir kez; musteri oluşturamaz — susar)
  useEffect(() => {
    if (panolar === null || panolar.length > 0 || !yetkili) return;
    const anahtar = 'kds.kadiBos:panolar';
    if (sessionStorage.getItem(anahtar)) return;
    sessionStorage.setItem(anahtar, '1');
    kadiOner('Henüz pano yok — raporlarını bir panoda toplayalım mı?',
      document.querySelector('[data-kadi-hedef="yeni-pano"]'));
  }, [panolar, yetkili]);

  const load = () => {
    setPanolar(null);
    getDashboards().then(setPanolar)
      .catch((e) => { setHata(e instanceof Error ? e.message : 'Panolar yüklenemedi.'); kadiHata(); });
  };
  useEffect(load, []);

  async function sil(p: DashboardSummary) {
    if (!(await askConfirm(`"${p.ad}" panosu silinsin mi? İçindeki raporlar silinmez.`, { tehlikeli: true, onayYazisi: 'Sil' }))) return;
    const kadiBitir = kadiYukleniyor('Panoyu siliyorum…');
    try { await deleteDashboard(p.id); load(); }
    catch (e) { setHata(e instanceof Error ? e.message : 'Silinemedi.'); kadiHata(); }
    finally { kadiBitir(); }
  }
  /** S43 — ana sayfa panosu; tek seçim olduğu için başka kartın işareti de düşer (liste tazelenir).
   * Rol kısıtı YOK: pano oluşturamayan müşteri de gördüğü panoyu açılışı yapabilir. */
  async function anaSayfamDegistir(p: DashboardSummary) {
    try {
      await setHomeDashboard(p.anaSayfamMi ? null : p.id);
      setPanolar((liste) => liste?.map((x) => ({ ...x, anaSayfamMi: !p.anaSayfamMi && x.id === p.id })) ?? liste);
    } catch (e) {
      setHata(e instanceof Error ? e.message : 'Ana sayfa panosu ayarlanamadı.'); kadiHata();
    }
  }

  async function openEdit(p: DashboardSummary) {
    const kadiBitir = kadiYukleniyor('Panoyu açıyorum…');
    try { setEditor(await getDashboard(p.id)); }
    catch (e) { setHata(e instanceof Error ? e.message : 'Pano açılamadı.'); kadiHata(); }
    finally { kadiBitir(); }
  }

  return (
    <Box>
      <PageHeader ikon={<DashboardIcon />} baslik="Panolar"
        aciklama="Adlandırılmış rapor panoların — oluştur, aç, ekiple paylaş.">
        {yetkili && (
          <Button data-kadi-hedef="yeni-pano" variant="contained" startIcon={<AddIcon />} onClick={() => setEditor('yeni')} sx={bandButtonWhite}>
            Yeni Pano
          </Button>
        )}
      </PageHeader>

      {hata && <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => setHata(null)}>{hata}</Alert>}

      {panolar === null ? (
        <Box sx={{ p: 6, textAlign: 'center' }}><CircularProgress size={26} /></Box>
      ) : panolar.length === 0 ? (
        <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 5, textAlign: 'center', color: 'text.secondary' }}>
          <Typography sx={{ fontSize: 14, fontWeight: 600, mb: 0.5 }}>Henüz pano yok.</Typography>
          <Typography sx={{ fontSize: 12.5 }} color="text.disabled">
            {yetkili ? '“Yeni Pano” ile raporlarını bir araya getir.' : 'Ekiple paylaşılan bir pano oluşturulunca burada görünür.'}
          </Typography>
        </Paper>
      ) : (
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: 2 }}>
          {panolar.map((p) => {
            const benim = p.benimMi || oturum.roller.includes('admin');
            return (
              <Paper key={p.id} variant="outlined"
                sx={{ borderRadius: boxRadius, p: 2, display: 'flex', flexDirection: 'column', gap: 1.2,
                  cursor: 'pointer', transition: 'box-shadow .15s, border-color .15s',
                  '&:hover': { borderColor: 'primary.main', boxShadow: '0 4px 16px rgba(15,45,78,.10)' },
                  '&:hover .panoIslem': { opacity: 1 } }}
                onClick={() => git(`/panolar/${p.id}`)}>
                <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1 }}>
                  <Box sx={{ width: 34, height: 34, borderRadius: '9px', display: 'flex', alignItems: 'center',
                    justifyContent: 'center', background: 'linear-gradient(135deg,#0f2d4e,#0e6f7a)', color: '#fff', flexShrink: 0 }}>
                    <DashboardIcon sx={{ fontSize: 18 }} />
                  </Box>
                  <Box sx={{ minWidth: 0, flexGrow: 1 }}>
                    <Typography noWrap sx={{ fontSize: 14, fontWeight: 700, color: 'text.primary' }}>{p.ad}</Typography>
                    <Typography noWrap sx={{ fontSize: 11.5, color: 'text.disabled' }}>
                      {p.benimMi ? 'benim panom' : (p.sahipAd ?? '—')}
                    </Typography>
                  </Box>
                  {/* Ana sayfa işareti SEÇİLİYKEN hep görünür (durum göstergesi), seçili değilken
                      diğer işlemler gibi üzerine gelinince çıkar. */}
                  <Box className="panoIslem" sx={{ display: 'flex', gap: 0.2, opacity: p.anaSayfamMi ? 1 : 0, transition: 'opacity .15s' }}>
                    <Tooltip title={p.anaSayfamMi ? 'Ana sayfamdan kaldır' : 'Ana sayfam yap'}>
                      <IconButton size="small" color={p.anaSayfamMi ? 'primary' : 'default'}
                        onClick={(e) => { e.stopPropagation(); void anaSayfamDegistir(p); }}>
                        {p.anaSayfamMi ? <HomeIcon sx={{ fontSize: 17 }} /> : <HomeOutlinedIcon sx={{ fontSize: 17 }} />}
                      </IconButton>
                    </Tooltip>
                    {benim && (
                      <>
                        <Tooltip title="Düzenle"><IconButton size="small" onClick={(e) => { e.stopPropagation(); void openEdit(p); }}><EditOutlinedIcon sx={{ fontSize: 17 }} /></IconButton></Tooltip>
                        <Tooltip title="Sil"><IconButton size="small" onClick={(e) => { e.stopPropagation(); void sil(p); }} sx={{ '&:hover': { color: 'error.main' } }}><DeleteOutlineIcon sx={{ fontSize: 17 }} /></IconButton></Tooltip>
                      </>
                    )}
                  </Box>
                </Box>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, mt: 'auto' }}>
                  <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.4, fontSize: 12, color: 'text.secondary' }}>
                    <AssessmentOutlinedIcon sx={{ fontSize: 15 }} /> {p.raporSayisi} rapor
                  </Box>
                  <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.4, fontSize: 11.5,
                    color: p.paylasim === 'Ekip' ? '#0e6f7a' : 'text.disabled' }}>
                    {p.paylasim === 'Ekip' ? <PeopleAltOutlinedIcon sx={{ fontSize: 14 }} /> : <LockOutlinedIcon sx={{ fontSize: 14 }} />}
                    {p.paylasim === 'Ekip' ? 'Ekip' : 'Özel'}
                  </Box>
                </Box>
              </Paper>
            );
          })}
        </Box>
      )}

      {editor !== null && (
        <DashboardEditor
          duzenlenen={editor === 'yeni' ? null : editor}
          close={() => setEditor(null)}
          kaydedildi={() => { setEditor(null); load(); }} />
      )}
      {confirmDialog}
    </Box>
  );
}
