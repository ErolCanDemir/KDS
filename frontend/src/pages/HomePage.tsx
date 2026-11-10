import { useEffect, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import RefreshIcon from '@mui/icons-material/Refresh';
import OpenInFullIcon from '@mui/icons-material/OpenInFull';
import HomeIcon from '@mui/icons-material/Home';
import DashboardIcon from '@mui/icons-material/Dashboard';
import DashboardCustomizeOutlinedIcon from '@mui/icons-material/DashboardCustomizeOutlined';
import { PageHeader, bandButton, bandButtonWhite } from '../components/PageHeader';
import { PanoGovdesi } from '../components/PanoGovdesi';
import { usePanoVerisi } from '../components/panoVerisi';
import { LoadingCurtain } from '../components/Loading';
import { useSession } from '../identity/sessionContext';
import { apiGet } from '../api/client';
import { getDashboards, getHomeDashboard, setHomeDashboard, type DashboardSummary } from '../api/dashboards';
import { boxRadius } from '../theme';
import { kadiHata } from '../kadi/kadiKanal';

type SistemDurumu = { surum: string; sunucuZamani: string };

/* S43 — ANA SAYFAYI KULLANICI TASARLAR (kullanıcı isteği 26 Ağu 2026: "temp olarak duruyor hala,
 * kullanıcılar kendi anasayfasını tasarlayabilsin"). Buradaki eski KPI/grafik/tablo blokları
 * TEMSİLÎ sayılardı ve kaldırıldı.
 *
 * Seçilen yol (kullanıcı onaylı): ayrı bir ana-sayfa tasarım motoru YAPILMADI — kullanıcı panosunu
 * Panolar'da tasarlar, birini "ana sayfam" işaretler, ana sayfa onu çizer. İkinci bir sürükle-bırak
 * düzenleyici, panonunkiyle sürekli ayrışacak ikinci bir bakım yükü demekti.
 *
 * Seçim yoksa karşılama görünümü çıkar: görünen panolar gerçek listeden gelir, tek tıkla seçilir. */

export function HomePage() {
  const [durum, setDurum] = useState<SistemDurumu | null>(null);
  const [durumHatasi, setDurumHatasi] = useState<string | null>(null);
  const [panoId, setPanoId] = useState<string | null>(null);
  const [secimYuklendi, setSecimYuklendi] = useState(false);
  const [hata, setHata] = useState<string | null>(null);

  useEffect(() => {
    apiGet<SistemDurumu>('/api/durum').then(setDurum).catch((h: Error) => { setDurumHatasi(h.message); kadiHata(); });
  }, []);

  useEffect(() => {
    getHomeDashboard()
      .then((s) => { setPanoId(s.panoId); setSecimYuklendi(true); })
      .catch((e: Error) => { setHata(e.message); kadiHata(); setSecimYuklendi(true); });
  }, []);

  const durumCipi = (
    <>
      {durum && <Chip size="small" icon={<CheckCircleIcon color="success" />} label={`API canlı · v${durum.surum}`} sx={{ fontWeight: 700 }} />}
      {durumHatasi && <Chip size="small" icon={<ErrorIcon color="error" />} label="API'ye ulaşılamadı" sx={{ fontWeight: 700 }} />}
    </>
  );

  if (!secimYuklendi) return <LoadingCurtain />;

  return (
    <Box>
      {hata && <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => setHata(null)}>{hata}</Alert>}
      {panoId
        ? <AnaSayfaPanosu panoId={panoId} durumCipi={durumCipi} kaldirildi={() => setPanoId(null)} onHata={setHata} />
        : <PanoSecimi durumCipi={durumCipi} secildi={setPanoId} onHata={setHata} />}
    </Box>
  );
}

/** Seçili panonun ana sayfadaki hâli: pano görüntüleyicinin ta kendisi (aynı kanca + aynı gövde),
 * yalnız yerleşim düzenleme yok — düzenleme panonun kendi ekranında yapılır. */
function AnaSayfaPanosu({ panoId, durumCipi, kaldirildi, onHata }: {
  panoId: string;
  durumCipi: ReactNode;
  kaldirildi: () => void;
  onHata: (m: string) => void;
}) {
  const git = useNavigate();
  const v = usePanoVerisi(panoId);

  async function kaldir() {
    try {
      await setHomeDashboard(null);
      kaldirildi();
    } catch (e) {
      onHata(e instanceof Error ? e.message : 'Ana sayfa panosu kaldırılamadı.');
      kadiHata();
    }
  }

  if (v.yukleniyor) return <LoadingCurtain />;
  // Pano bu arada silinmiş/görünmez olduysa sunucu zaten null döndürürdü; buraya düşmek ağ hatasıdır.
  if (!v.pano) return <Alert severity="error">{v.hata ?? 'Ana sayfa panosu yüklenemedi.'}</Alert>;

  return (
    <>
      <PageHeader
        sol={
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
            <HomeIcon fontSize="small" color="primary" />
            <Typography sx={{ fontSize: 13.5, fontWeight: 700 }} noWrap>{v.pano.ad}</Typography>
            <Typography sx={{ fontSize: 11.5 }} color="text.secondary" noWrap>
              ana sayfan · {v.panoRaporlari.length} rapor
              {v.eksik > 0 ? ` · ${v.eksik} rapor artık yok` : ''}
            </Typography>
          </Box>
        }>
        {durumCipi}
        <Button variant="outlined" startIcon={<RefreshIcon />} onClick={v.yenile} sx={bandButton}>Yenile</Button>
        <Button variant="outlined" onClick={() => void kaldir()} sx={bandButton}>Ana sayfamdan kaldır</Button>
        <Button variant="contained" startIcon={<OpenInFullIcon />} onClick={() => git(`/panolar/${panoId}`)} sx={bandButtonWhite}>Panoyu aç</Button>
      </PageHeader>

      {v.hata && <Alert severity="error" sx={{ mb: 1.5 }}>{v.hata}</Alert>}

      <PanoGovdesi
        v={v}
        bosMesaj="Panoyu açıp rapor ekleyebilirsin."
        ac={(raporId) => git(`/raporlar/${raporId}`, { state: { donus: '/' } })}
      />
    </>
  );
}

/** Seçim yokken çıkan karşılama: görünen panolardan biri tek tıkla ana sayfa yapılır. */
function PanoSecimi({ durumCipi, secildi, onHata }: {
  durumCipi: ReactNode;
  secildi: (id: string) => void;
  onHata: (m: string) => void;
}) {
  const git = useNavigate();
  const oturum = useSession();
  const [panolar, setPanolar] = useState<DashboardSummary[] | null>(null);
  const tasarlayabilir = oturum.roller.includes('admin') || oturum.roller.includes('gelistirici');

  useEffect(() => {
    getDashboards()
      .then(setPanolar)
      .catch((e: Error) => { onHata(e.message); kadiHata(); setPanolar([]); });
    // onHata her render'da yeni kimlik alabilir; liste bir kez çekilir.
    // oxlint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function sec(id: string) {
    try {
      await setHomeDashboard(id);
      secildi(id);
    } catch (e) {
      onHata(e instanceof Error ? e.message : 'Ana sayfa panosu ayarlanamadı.');
      kadiHata();
    }
  }

  if (!panolar) return <LoadingCurtain />;

  return (
    <>
      <PageHeader>
        {durumCipi}
        {tasarlayabilir && (
          <Button variant="contained" startIcon={<DashboardCustomizeOutlinedIcon />}
            onClick={() => git('/panolar')} sx={bandButtonWhite}>Pano tasarla</Button>
        )}
      </PageHeader>

      <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 3 }}>
        <Typography sx={{ fontSize: 16, fontWeight: 700 }}>Ana sayfanı sen seç</Typography>
        <Typography sx={{ fontSize: 13, mt: 0.5, maxWidth: '62ch' }} color="text.secondary">
          Tasarladığın panolardan birini ana sayfan yap; her girişte o pano açılsın. Seçimin
          yalnız seni etkiler — aynı panoyu kullanan başkalarının ana sayfası değişmez.
        </Typography>

        {panolar.length === 0 ? (
          <Box sx={{ mt: 2.5, py: 3, textAlign: 'center', border: '1px dashed', borderColor: 'divider', borderRadius: boxRadius }}>
            <Typography sx={{ fontSize: 13, fontWeight: 600 }} color="text.secondary">Henüz görebildiğin bir pano yok.</Typography>
            <Typography sx={{ fontSize: 12.5, mt: 0.5 }} color="text.disabled">
              {tasarlayabilir
                ? 'Panolar ekranından bir pano oluştur, sonra buradan ana sayfan yap.'
                : 'Ekibin seninle bir pano paylaştığında burada görünecek.'}
            </Typography>
          </Box>
        ) : (
          <Box sx={{ mt: 2.5, display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', lg: 'repeat(3, 1fr)' }, gap: 1.5 }}>
            {panolar.map((p, i) => (
              <Paper key={p.id} variant="outlined" sx={{
                p: '14px 16px', borderRadius: boxRadius, display: 'flex', flexDirection: 'column', gap: 0.5,
                animation: `kds-belir .45s cubic-bezier(.22,1,.36,1) ${i * 0.06}s both`,
              }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
                  <DashboardIcon fontSize="small" color="secondary" />
                  <Typography sx={{ fontSize: 13.5, fontWeight: 700 }} noWrap>{p.ad}</Typography>
                </Box>
                <Typography sx={{ fontSize: 11.5 }} color="text.secondary">
                  {p.raporSayisi} rapor · {p.benimMi ? 'benim panom' : (p.sahipAd ?? '—')}
                </Typography>
                <Box sx={{ display: 'flex', gap: 1, mt: 1.25 }}>
                  <Button size="small" variant="contained" startIcon={<HomeIcon />} onClick={() => void sec(p.id)}>
                    Ana sayfam yap
                  </Button>
                  <Button size="small" onClick={() => git(`/panolar/${p.id}`)}>Önizle</Button>
                </Box>
              </Paper>
            ))}
          </Box>
        )}
      </Paper>
    </>
  );
}
