import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import DashboardIcon from '@mui/icons-material/Dashboard';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import RefreshIcon from '@mui/icons-material/Refresh';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import DashboardCustomizeOutlinedIcon from '@mui/icons-material/DashboardCustomizeOutlined';
import SaveOutlinedIcon from '@mui/icons-material/SaveOutlined';
import HomeIcon from '@mui/icons-material/Home';
import HomeOutlinedIcon from '@mui/icons-material/HomeOutlined';
import { PageHeader, bandButton, bandButtonWhite } from '../components/PageHeader';
import { DashboardEditor } from '../components/DashboardEditor';
import { PanoGovdesi } from '../components/PanoGovdesi';
import { usePanoVerisi } from '../components/panoVerisi';
import { LoadingCurtain } from '../components/Loading';
import { useSession } from '../identity/sessionContext';
import { setHomeDashboard } from '../api/dashboards';
import { kadiHata } from '../kadi/kadiKanal';

/** Pano görüntüleyici (S11): panonun raporlarını react-grid-layout ızgarasında canlı gösterir;
 * sahibi/admin "Yerleşim" moduyla widget'ları sürükle-bırak taşır/boyutlar ve kaydeder. Yerleşim,
 * rapor listesinden AYRI saklanır (rapor ekle/çıkar konumları bozmaz). Silinen rapor sessizce atlanır.
 * Gövde (filtre şeridi + ızgara) S43'te <PanoGovdesi/>'ne taşındı — ana sayfa da aynısını çiziyor. */
export function DashboardViewPage() {
  const { id } = useParams<{ id: string }>();
  const git = useNavigate();
  const oturum = useSession();
  const v = usePanoVerisi(id);
  const [editor, setEditor] = useState(false);
  // Sunucudaki seçim pano detayında geliyor; kullanıcı düğmeye bastığında yanıtı beklemeden
  // yerel olarak da çevrilir (tek alanlık tercih — geri alınması ucuz, hata olursa geri döner).
  const [anaSayfamEzme, setAnaSayfamEzme] = useState<boolean | null>(null);

  if (v.yukleniyor) return <LoadingCurtain />;
  if (!v.pano) return <Alert severity="error">{v.hata ?? 'Pano bulunamadı.'}</Alert>;

  const pano = v.pano;
  const yetkili = pano.benimMi || oturum.roller.includes('admin');
  const anaSayfamMi = anaSayfamEzme ?? pano.anaSayfamMi;

  async function anaSayfamDegistir() {
    const yeni = !anaSayfamMi;
    setAnaSayfamEzme(yeni);
    try {
      await setHomeDashboard(yeni ? pano.id : null);
    } catch (e) {
      setAnaSayfamEzme(!yeni);
      v.setHata(e instanceof Error ? e.message : 'Ana sayfa panosu ayarlanamadı.');
      kadiHata();
    }
  }

  return (
    <Box>
      {/* Şerit S25'te renkli banttan nötr zemine taşındı; geri okunun beyaz rengi o taşımadan
          kalmıştı ve açık temada görünmüyordu — zemin-nötr renge çekildi. */}
      <PageHeader
        sol={<IconButton onClick={() => git('/panolar')} sx={{ ml: -1 }}><ArrowBackIcon /></IconButton>}
        ikon={<DashboardIcon />}
        baslik={pano.ad}
        aciklama={<>
          {pano.benimMi ? 'benim panom' : (pano.sahipAd ?? '—')}{pano.paylasim === 'Ekip' ? ' · ekiple paylaşılan' : ' · özel'}
          {` · ${v.panoRaporlari.length} rapor`}{v.eksik > 0 ? ` · ${v.eksik} rapor artık yok` : ''}
          {anaSayfamMi ? ' · ana sayfam' : ''}
          {v.duzenle ? ' · yerleşim düzenleniyor — sürükle / köşeden boyutlandır' : ''}
        </>}>
        {v.duzenle ? (
          <>
            <Button variant="outlined" onClick={() => v.setDuzenle(false)} sx={bandButton} disabled={v.kaydediyor}>Vazgeç</Button>
            <Button variant="contained" startIcon={<SaveOutlinedIcon />} onClick={() => void v.yerlesimKaydet()} sx={bandButtonWhite} disabled={v.kaydediyor}>Kaydet</Button>
          </>
        ) : (
          <>
            {/* S43: ana sayfa seçimi OKUMA tarafı — pano oluşturamayan müşteri de kendi açılışını seçebilir. */}
            <Tooltip title={anaSayfamMi ? 'Ana sayfamdan kaldır' : 'Ana sayfam yap'}>
              <IconButton onClick={() => void anaSayfamDegistir()} color={anaSayfamMi ? 'primary' : 'default'}>
                {anaSayfamMi ? <HomeIcon /> : <HomeOutlinedIcon />}
              </IconButton>
            </Tooltip>
            <Button variant="outlined" startIcon={<RefreshIcon />} onClick={v.yenile} sx={bandButton}>Yenile</Button>
            {yetkili && v.panoRaporlari.length > 0 && (
              <Button variant="outlined" startIcon={<DashboardCustomizeOutlinedIcon />} onClick={() => v.setDuzenle(true)} sx={bandButton}>Yerleşim</Button>
            )}
            {yetkili && <Button variant="contained" startIcon={<EditOutlinedIcon />} onClick={() => setEditor(true)} sx={bandButtonWhite}>Düzenle</Button>}
          </>
        )}
      </PageHeader>

      {v.hata && <Alert severity="error" sx={{ mb: 1.5 }}>{v.hata}</Alert>}

      <PanoGovdesi
        v={v}
        bosMesaj={yetkili ? '“Düzenle” ile rapor ekle.' : 'Panonun sahibi henüz rapor eklememiş.'}
        /* M30: rapora panodan gidildiği state ile taşınır — görüntüleyicinin geri tuşu panoya döner */
        ac={(raporId) => git(`/raporlar/${raporId}`, { state: { donus: `/panolar/${id}` } })}
      />

      {editor && (
        <DashboardEditor duzenlenen={pano} close={() => setEditor(false)} kaydedildi={() => { setEditor(false); v.yukle(); }} />
      )}
    </Box>
  );
}
