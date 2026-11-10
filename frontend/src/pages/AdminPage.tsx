import { useEffect, useState } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Typography from '@mui/material/Typography';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import BlockIcon from '@mui/icons-material/Block';
import AdminPanelSettingsOutlinedIcon from '@mui/icons-material/AdminPanelSettingsOutlined';
import { PageHeader } from '../components/PageHeader';
import { apiGet } from '../api/client';
import { useSession } from '../identity/sessionContext';
import { kimlikKapali } from '../identity/identityConfig';
import { kadiHata } from '../kadi/kadiKanal';

/** S3 kimlik/rol kanıt ekranı: oturum kimliği + rol zorlaması + tenant kullanıcı listesi
 * (JIT provisioning + global query filter kanıtı). */
export function AdminPage() {
  const oturum = useSession();

  return (
    <Box>
      <PageHeader ikon={<AdminPanelSettingsOutlinedIcon />} baslik="Yönetim"
        aciklama="Oturum kimliği, tenant kullanıcıları ve rol zorlaması denetimi." />
      <Stack spacing={2} sx={{ maxWidth: 760 }}>
      <Paper variant="outlined" sx={{ p: 2.5 }}>
        <Typography variant="h6" color="primary" gutterBottom>
          Oturum kimliği
        </Typography>
        {kimlikKapali ? (
          <Alert severity="warning">Kimlik kapalı (VITE_KIMLIK_KAPALI=true) — Keycloak ile açın.</Alert>
        ) : oturum.yukleniyor ? (
          <Typography variant="body2" color="text.secondary">Yükleniyor…</Typography>
        ) : oturum.hata ? (
          <Alert severity="error">{oturum.hata}</Alert>
        ) : (
          <Stack spacing={1.5}>
            <Satir etiket="Kullanıcı" deger={oturum.ad ?? '—'} />
            <Box>
              <Typography variant="caption" color="text.secondary">Roles</Typography>
              <Box sx={{ mt: 0.5, display: 'flex', gap: 0.75, flexWrap: 'wrap' }}>
                {oturum.roller.length
                  ? oturum.roller.map((r) => <Chip key={r} size="small" color="secondary" label={r} />)
                  : <Typography variant="body2">—</Typography>}
              </Box>
            </Box>
            <Satir etiket="Tenant" deger={oturum.tenantId ?? '—'} />
          </Stack>
        )}
      </Paper>

      <TenantKullanicilari />
      <RolDenetimi />
      </Stack>
    </Box>
  );
}

function Satir({ etiket, deger }: { etiket: string; deger: string }) {
  return (
    <Box>
      <Typography variant="caption" color="text.secondary">{etiket}</Typography>
      <Typography variant="body1">{deger}</Typography>
    </Box>
  );
}

type KullaniciOzeti = {
  kullaniciAdi: string;
  ePosta?: string;
  adSoyad?: string;
  sonGirisTarihi?: string;
};

/** Mevcut tenant'ın kullanıcıları — global query filter yalnız kendi tenant'ını döndürür. */
function TenantKullanicilari() {
  const [liste, setListe] = useState<KullaniciOzeti[] | null>(null);
  const [hata, setHata] = useState<string | null>(null);

  useEffect(() => {
    if (kimlikKapali) return;
    apiGet<KullaniciOzeti[]>('/api/kullanicilar')
      .then(setListe)
      .catch((h: Error) => { setHata(h.message); kadiHata(); });
  }, []);

  return (
    <Paper variant="outlined" sx={{ p: 2.5 }}>
      <Typography variant="h6" color="primary" gutterBottom>
        Tenant kullanıcıları
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
        Bu tenant'ta giriş yapmış kullanıcılar — ilk girişte otomatik oluşturulur (JIT),
        yalnız kendi tenant'ınız görünür (global query filter).
      </Typography>
      {kimlikKapali && <Alert severity="warning">Kimlik kapalı.</Alert>}
      {hata && <Alert severity="error">{hata}</Alert>}
      {!liste && !hata && !kimlikKapali && <Typography variant="body2">Yükleniyor…</Typography>}
      {liste && (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Kullanıcı</TableCell>
              <TableCell>E-Posta</TableCell>
              <TableCell>Son giriş</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {liste.map((k) => (
              <TableRow key={k.kullaniciAdi}>
                <TableCell>{k.kullaniciAdi}</TableCell>
                <TableCell>{k.ePosta ?? '—'}</TableCell>
                <TableCell>{k.sonGirisTarihi ? new Date(k.sonGirisTarihi).toLocaleString('tr-TR') : '—'}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </Paper>
  );
}

/** Rol korumalı ucu çağırıp sonucu gösterir: admin 200, diğer roller 403. */
function RolDenetimi() {
  const [durum, setDurum] = useState<'yukleniyor' | 'izinli' | 'yasak' | 'hata'>('yukleniyor');
  const [ayrinti, setAyrinti] = useState('');

  useEffect(() => {
    if (kimlikKapali) return;
    apiGet<{ mesaj: string }>('/api/durum/admin-alani')
      .then((y) => {
        setDurum('izinli');
        setAyrinti(y.mesaj);
      })
      .catch((h: Error) => {
        setDurum(h.message.includes('403') ? 'yasak' : 'hata');
        setAyrinti(h.message);
      });
  }, []);

  return (
    <Paper variant="outlined" sx={{ p: 2.5 }}>
      <Typography variant="h6" color="primary" gutterBottom>
        Rol zorlaması denetimi
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
        <code>/api/durum/admin-alani</code> yalnız <b>admin</b> rolüne açıktır.
      </Typography>
      {kimlikKapali && <Alert severity="warning">Kimlik kapalı.</Alert>}
      {durum === 'yukleniyor' && !kimlikKapali && <Typography variant="body2">Deneniyor…</Typography>}
      {durum === 'izinli' && <Chip icon={<CheckCircleIcon />} color="success" variant="outlined" label={`Erişim var — ${ayrinti}`} />}
      {durum === 'yasak' && <Chip icon={<BlockIcon />} color="warning" variant="outlined" label="403 — bu rol admin alanına giremez (beklenen)" />}
      {durum === 'hata' && <Alert severity="error">{ayrinti}</Alert>}
    </Paper>
  );
}
