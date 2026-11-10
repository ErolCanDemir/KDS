import { useEffect, useState } from 'react';
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Collapse from '@mui/material/Collapse';
import IconButton from '@mui/material/IconButton';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import ExpandLess from '@mui/icons-material/ExpandLess';
import ExpandMore from '@mui/icons-material/ExpandMore';
import LogoutIcon from '@mui/icons-material/Logout';
import DarkModeOutlinedIcon from '@mui/icons-material/DarkModeOutlined';
import LightModeOutlinedIcon from '@mui/icons-material/LightModeOutlined';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from 'react-oidc-context';
import { kimlikKapali } from '../identity/identityConfig';
import { useSession } from '../identity/sessionContext';
import { getMarka, type Marka } from '../api/kurulus';
import { menu, type MenuGrubu, type MenuOgesi } from './menu';
import { bantStili, rayGradyani, type BantStili } from '../theme';
import { useTemaModu } from '../temaContext';
import { KadiYoldas } from '../kadi/KadiYoldas';

const rayGenislik = 252;

const rayLink = {
  color: 'rgba(255,255,255,.82)',
  borderRadius: 2,
  mb: 0.3,
  '& .MuiListItemIcon-root': { color: 'inherit', minWidth: 34 },
  '&:hover': { bgcolor: 'rgba(255,255,255,.09)' },
  '&.Mui-selected': { bgcolor: 'rgba(255,255,255,.16)', color: '#fff', fontWeight: 600 },
  '&.Mui-selected:hover': { bgcolor: 'rgba(255,255,255,.2)' },
} as const;

/** Rotadan aktif menü öğesini bulur (S25 global bant): alt öğeler dahil en uzun yol-öneki kazanır —
 * /panolar/:id "Panolar"a, /raporlar/:id "Raporlar"a düşer. */
function aktifOge(yol: string): { ad: string; aciklama?: string; ikon?: MenuOgesi['ikon'] } {
  let enIyi: { ad: string; aciklama?: string; ikon?: MenuOgesi['ikon'] } | null = null;
  let enIyiUzunluk = -1;
  for (const grup of menu) {
    for (const oge of grup.ogeler) {
      const adaylar = [{ yol: oge.yol, ad: oge.ad, aciklama: oge.aciklama, ikon: oge.ikon },
        ...(oge.alt ?? []).map((a) => ({ yol: a.yol, ad: a.ad, aciklama: a.aciklama, ikon: oge.ikon }))];
      for (const a of adaylar) {
        const eslesir = a.yol === '/' ? yol === '/' : yol === a.yol || yol.startsWith(a.yol + '/');
        if (eslesir && a.yol.length > enIyiUzunluk) {
          enIyi = a;
          enIyiUzunluk = a.yol.length;
        }
      }
    }
  }
  return enIyi ?? { ad: 'LST-KDS' };
}

export function AppShell() {
  const konum = useLocation();
  const oturum = useSession();
  const { mod } = useTemaModu();
  const [marka, setMarka] = useState<Marka | null>(null);

  /* S35 — kuruluş markası: logo varsa ürün adı yerine o çizilir, vurgu rengi verilmişse
   * --kds-marka değişkenine yazılır. Hata YUTULUR: marka bir süstür, alınamadığında kabuk
   * ürünün kendi kimliğiyle çizilmeye devam eder. */
  useEffect(() => {
    getMarka().then(setMarka).catch(() => setMarka(null));
  }, []);

  useEffect(() => {
    const kok = document.documentElement;
    if (marka?.markaRengi) {
      kok.style.setProperty('--kds-marka', marka.markaRengi);
    } else {
      kok.style.removeProperty('--kds-marka');
    }
  }, [marka?.markaRengi]);

  const gorunur = (roller?: string[]) => !roller || kimlikKapali || roller.some((r) => oturum.roller.includes(r));

  return (
    <Box sx={{ display: 'flex', height: '100vh' }}>
      <Box
        component="aside"
        sx={{ width: rayGenislik, flexShrink: 0, background: rayGradyani(mod), color: '#fff', display: 'flex', flexDirection: 'column', p: 1.5 }}
      >
        {marka?.logoDataUri ? (
          // Logo yüklenmişse ürün adı yerine kuruluşun logosu çizilir; başlık metni erişilebilirlik
          // için alt metninde yaşar.
          <Box component="img" src={marka.logoDataUri} alt={marka.ad}
            sx={{ maxWidth: rayGenislik - 36, maxHeight: 40, objectFit: 'contain', m: '6px 10px 14px' }} />
        ) : (
          <Typography sx={{ fontWeight: 800, letterSpacing: 1, fontSize: 22, m: '6px 10px 14px', whiteSpace: 'nowrap' }}>
            LST-KDS
          </Typography>
        )}
        <Box
          component="nav"
          sx={{
            flexGrow: 1,
            overflowY: 'auto',
            minHeight: 0,
            pr: 0.4,
            '&::-webkit-scrollbar': { width: 6 },
            '&::-webkit-scrollbar-track': { background: 'transparent' },
            '&::-webkit-scrollbar-thumb': { backgroundColor: 'rgba(255,255,255,.22)', borderRadius: 8 },
            '&::-webkit-scrollbar-thumb:hover': { backgroundColor: 'rgba(255,255,255,.42)' },
            scrollbarWidth: 'thin',
            scrollbarColor: 'rgba(255,255,255,.24) transparent',
          }}
        >
          {menu.map((grup) => {
            const ogeler = grup.ogeler.filter((o) => gorunur(o.roller));
            if (!ogeler.length) return null;
            return <Grup key={grup.baslik} grup={grup} ogeler={ogeler} yol={konum.pathname} />;
          })}
        </Box>
      </Box>

      <Box sx={{ flexGrow: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
        <UstBant yol={konum.pathname} />
        <Box component="main" sx={{ flexGrow: 1, minHeight: 0, overflow: 'auto', bgcolor: 'background.default' }}>
          <Box sx={{ p: 2.25 }}>
            <Outlet />
          </Box>
        </Box>
      </Box>
      {/* S28: Kadi bot yoldaş — sağ altta nöbette; ekran cümleleri + sayfa sahneleri kadiKanal'dan */}
      <KadiYoldas />
    </Box>
  );
}

/** S25 — GLOBAL üst bant (21 Ağu mockup yapısı): aktif ekranın ikonu + adı + açıklaması solda,
 * mod/çıkış/avatar sağda. Sayfa başlıkları artık burada — PageHeader yalnız aksiyon şerididir. */
function UstBant({ yol }: { yol: string }) {
  const { mod } = useTemaModu();
  const aktif = aktifOge(yol);
  const stil = bantStili(mod);
  return (
    <Box sx={{
      background: stil.zemin, color: stil.metin, flexShrink: 0, borderBottom: stil.altCizgi,
      display: 'flex', alignItems: 'center', gap: 1.5, px: 2.25, py: 1.3,
    }}>
      {aktif.ikon && (
        <Box sx={{
          width: 38, height: 38, borderRadius: '10px', bgcolor: stil.ikonZemin,
          display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0,
          '& svg': { fontSize: 22 },
        }}>
          {aktif.ikon}
        </Box>
      )}
      <Box sx={{ minWidth: 0 }}>
        <Typography variant="h5" component="h1" noWrap sx={{ fontWeight: 800, fontSize: 17, lineHeight: 1.15, letterSpacing: '-0.2px' }}>
          {aktif.ad}
        </Typography>
        {aktif.aciklama && (
          <Typography noWrap sx={{ fontSize: 11.5, color: stil.metinSolgun }}>{aktif.aciklama}</Typography>
        )}
      </Box>
      <Box sx={{ flexGrow: 1 }} />
      <ModDugmesi stil={stil} />
      <KullaniciBloku stil={stil} />
    </Box>
  );
}

function Grup({ grup, ogeler, yol }: { grup: MenuGrubu; ogeler: MenuOgesi[]; yol: string }) {
  return (
    <Box sx={{ mb: 0.5 }}>
      <Typography sx={{ fontSize: 10.5, letterSpacing: 1.4, textTransform: 'uppercase', color: 'rgba(255,255,255,.5)', fontWeight: 700, m: '12px 12px 4px' }}>
        {grup.baslik}
      </Typography>
      <List disablePadding>
        {ogeler.map((oge) => (
          <MenuOge key={oge.yol} oge={oge} yol={yol} />
        ))}
      </List>
    </Box>
  );
}

function MenuOge({ oge, yol }: { oge: MenuOgesi; yol: string }) {
  const altAktif = oge.alt?.some((a) => a.yol === yol) ?? false;
  const [acik, setAcik] = useState(altAktif);

  if (!oge.alt) {
    return (
      <Tooltip title={oge.aciklama ?? ''} placement="right" arrow enterDelay={500}>
        <ListItemButton component={Link} to={oge.yol} selected={yol === oge.yol} sx={rayLink}>
          <ListItemIcon>{oge.ikon}</ListItemIcon>
          <ListItemText primary={oge.ad} />
        </ListItemButton>
      </Tooltip>
    );
  }

  return (
    <>
      <Tooltip title={oge.aciklama ?? ''} placement="right" arrow enterDelay={500}>
        <ListItemButton onClick={() => setAcik((v) => !v)} selected={altAktif && !acik} sx={rayLink}>
          <ListItemIcon>{oge.ikon}</ListItemIcon>
          <ListItemText primary={oge.ad} />
          {acik ? <ExpandLess /> : <ExpandMore />}
        </ListItemButton>
      </Tooltip>
      <Collapse in={acik} unmountOnExit>
        <Box sx={{ ml: 2.4, pl: 1.2, borderLeft: '1.5px solid rgba(255,255,255,.18)' }}>
          <List disablePadding>
            {oge.alt.map((a) => (
              <Tooltip key={a.yol} title={a.aciklama ?? ''} placement="right" arrow enterDelay={500}>
                <ListItemButton component={Link} to={a.yol} selected={yol === a.yol} sx={{ ...rayLink, py: 0.55 }}>
                  <ListItemText primary={a.ad} slotProps={{ primary: { sx: { fontSize: 13.5 } } }} />
                </ListItemButton>
              </Tooltip>
            ))}
          </List>
        </Box>
      </Collapse>
    </>
  );
}

/** Açık/koyu geçişi — bantta (mockup'taki ay/güneş düğmesinin eşi); login temasındakiyle aynı anahtar. */
function ModDugmesi({ stil }: { stil: BantStili }) {
  const { mod, degistir } = useTemaModu();
  const koyu = mod === 'koyu';
  return (
    <Tooltip title={koyu ? 'Açık tema' : 'Koyu tema'}>
      <IconButton size="small" onClick={degistir} sx={{ color: stil.metinSolgun }}>
        {koyu ? <LightModeOutlinedIcon fontSize="small" /> : <DarkModeOutlinedIcon fontSize="small" />}
      </IconButton>
    </Tooltip>
  );
}

/** Kullanıcı — bantın sağında (mockup yapısı): ad + roller + çıkış + avatar. */
function KullaniciBloku({ stil }: { stil: BantStili }) {
  if (kimlikKapali) {
    return <Typography sx={{ fontSize: 11.5, color: stil.metinSolgun }}>Kimlik kapalı</Typography>;
  }
  return <KimlikliKullanici stil={stil} />;
}

function KimlikliKullanici({ stil }: { stil: BantStili }) {
  const kimlik = useAuth();
  const oturum = useSession();
  const ad = oturum.ad ?? kimlik.user?.profile.preferred_username ?? '…';

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
      <Tooltip title="Çıkış">
        <IconButton size="small" onClick={() => void kimlik.signoutRedirect()} sx={{ color: stil.metinSolgun }}>
          <LogoutIcon fontSize="small" />
        </IconButton>
      </Tooltip>
      <Tooltip title={`${ad} · ${oturum.roller.join(' · ') || '—'}`}>
        <Avatar sx={{ width: 30, height: 30, bgcolor: stil.avatarZemin, color: stil.avatarMetin, fontSize: 13, fontWeight: 700 }}>
          {ad.charAt(0).toUpperCase()}
        </Avatar>
      </Tooltip>
    </Box>
  );
}
