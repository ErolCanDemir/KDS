import { useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import Stepper from '@mui/material/Stepper';
import Step from '@mui/material/Step';
import StepLabel from '@mui/material/StepLabel';
import CircularProgress from '@mui/material/CircularProgress';
import FormControlLabel from '@mui/material/FormControlLabel';
import Checkbox from '@mui/material/Checkbox';
import StorageIcon from '@mui/icons-material/Storage';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import {
  veritabaniTest, kimlikTest, kurulumTamamla,
  type VeritabaniTest, type KimlikTest,
} from '../api/kurulum';
import { boxRadius } from '../theme';
import { LoadingCurtain } from '../components/Loading';

const adimlar = ['Kurulum anahtarı', 'Yönetim veritabanı', 'Kimlik sağlayıcı', 'Kiracı & adresler', 'Kur'];

/** S16/A2 — İlk kurulum sihirbazı. Uygulama yapılandırılmamışken (kurulum modu) açılır:
 * yönetim veritabanı ekrandan girilir, test edilir, şema kurulur ve yapılandırma sunucuda
 * ŞİFRELİ olarak saklanır. Böylece üründe hiçbir bağlantı dizesi kodda/derlemede taşınmaz. */
export function KurulumPage({ anahtarDosyasi }: { anahtarDosyasi?: string | null }) {
  const [adim, setAdim] = useState(0);
  const [anahtar, setAnahtar] = useState('');
  const [sunucu, setSunucu] = useState('');
  const [veritabani, setVeritabani] = useState('KdsMetadata');
  const [kullaniciAdi, setKullaniciAdi] = useState('');
  const [sifre, setSifre] = useState('');
  const [yetkili, setYetkili] = useState('');
  const [istemci, setIstemci] = useState('kds-api');
  const [httpsZorunlu, setHttpsZorunlu] = useState(true);
  const [kiraciAdi, setKiraciAdi] = useState('');
  const [kiraciId, setKiraciId] = useState('');
  const [onyuzAdres, setOnyuzAdres] = useState(window.location.origin);
  const [depoSunucusu, setDepoSunucusu] = useState('');
  const [logBaglantisi, setLogBaglantisi] = useState('');

  const [mesgul, setMesgul] = useState(false);
  const [perde, setPerde] = useState<string | null>(null);
  const [hata, setHata] = useState<string | null>(null);
  const [dbSonuc, setDbSonuc] = useState<VeritabaniTest | null>(null);
  const [kimlikSonuc, setKimlikSonuc] = useState<KimlikTest | null>(null);
  const [bitti, setBitti] = useState<{ kiraciId: string; mesaj: string } | null>(null);

  const veritabaniIstek = { sunucu, veritabani, kullaniciAdi: kullaniciAdi || undefined, sifre: sifre || undefined };
  const kimlikIstek = { yetkili, istemci, httpsZorunlu };

  /** <paramref name="perdeMesaji"/> verilirse iş boyunca tam ekran perde iner: kurulum ekranında
   * Kadi montajlı DEĞİLDİR (uygulama kabuğu henüz yok), o yüzden LoadingCurtain kendi klasik
   * perdesini çizer — uzun süren "kur" adımında kullanıcının forma dokunmasını da engeller.
   * Kısa testlerde (bağlantı/kimlik) perde yok, buton içi spinner yeter. */
  async function calistir(is: () => Promise<void>, perdeMesaji?: string) {
    setMesgul(true); setHata(null); setPerde(perdeMesaji ?? null);
    try { await is(); }
    catch (e) { setHata((e as Error).message); }
    finally { setMesgul(false); setPerde(null); }
  }

  const dbTest = () => calistir(async () => {
    const s = await veritabaniTest(anahtar, veritabaniIstek);
    setDbSonuc(s);
  });

  const kimlikDene = () => calistir(async () => {
    const s = await kimlikTest(anahtar, kimlikIstek);
    setKimlikSonuc(s);
  });

  const kur = () => calistir(async () => {
    const s = await kurulumTamamla(anahtar, {
      veritabani: veritabaniIstek,
      kimlik: yetkili ? kimlikIstek : null,
      kiraciAdi,
      kiraciId: kiraciId || null,
      onyuzAdres: onyuzAdres || null,
      depoSunucusu: depoSunucusu || null,
      logBaglantisi: logBaglantisi || null,
    });
    setBitti({ kiraciId: s.kiraciId, mesaj: s.mesaj });
  }, 'Kurulumu tamamlıyorum — veritabanı şeması kuruluyor…');

  const ileriKapali =
    (adim === 0 && anahtar.trim().length === 0) ||
    (adim === 1 && !(dbSonuc?.basarili)) ||
    (adim === 3 && kiraciAdi.trim().length === 0);

  if (bitti) {
    return (
      <Kabuk>
        <Alert severity="success" icon={<CheckCircleIcon />} sx={{ mb: 2 }}>{bitti.mesaj}</Alert>
        <Typography sx={{ fontSize: 13.5, mb: 1 }}>
          Kiracı kimliği (Keycloak'taki <b>tenant_id</b> talebi bununla aynı olmalı):
        </Typography>
        <Box component="code" sx={{ display: 'block', bgcolor: 'var(--kds-zemin2)', p: 1.2, borderRadius: 1, fontSize: 13, mb: 2 }}>
          {bitti.kiraciId}
        </Box>
        <Typography sx={{ fontSize: 13 }} color="text.secondary">
          Kimlik ayarlarının etkinleşmesi için uygulamayı yeniden başlatın, sonra bu sayfayı yenileyin.
        </Typography>
        <Button variant="contained" sx={{ mt: 2 }} onClick={() => window.location.reload()}>Sayfayı yenile</Button>
      </Kabuk>
    );
  }

  return (
    <Kabuk>
      {perde && <LoadingCurtain mesaj={perde} />}
      <Stepper activeStep={adim} sx={{ mb: 3 }}>
        {adimlar.map((a) => <Step key={a}><StepLabel>{a}</StepLabel></Step>)}
      </Stepper>

      {hata && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setHata(null)}>{hata}</Alert>}

      {adim === 0 && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          <Typography sx={{ fontSize: 13.5 }} color="text.secondary">
            Kurulumu yalnız sunucuya erişebilen kişi yapabilsin diye tek kullanımlık bir anahtar
            üretildi. Anahtar sunucudaki şu dosyada (ve uygulama açılış günlüğünde) yazılı:
          </Typography>
          <Box component="code" sx={{ bgcolor: 'var(--kds-zemin2)', p: 1.2, borderRadius: 1, fontSize: 12.5, wordBreak: 'break-all' }}>
            {anahtarDosyasi ?? 'kurulum-anahtari.txt'}
          </Box>
          <TextField label="Kurulum anahtarı" size="small" value={anahtar} autoFocus
            onChange={(e) => setAnahtar(e.target.value.trim())} />
        </Box>
      )}

      {adim === 1 && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          <Typography sx={{ fontSize: 13.5 }} color="text.secondary">
            KDS'in kendi verisini (bağlantılar, haritalar, raporlar, panolar) tutacağı veritabanı.
            Yoksa kurulum sırasında oluşturulabilir.
          </Typography>
          <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 1.5 }}>
            <TextField label="Sunucu" size="small" value={sunucu} onChange={(e) => setSunucu(e.target.value)}
              placeholder="ör. SQLSRV01 / (localdb)\MSSQLLocalDB" />
            <TextField label="Veritabanı" size="small" value={veritabani} onChange={(e) => setVeritabani(e.target.value)} />
            <TextField label="Kullanıcı (boş = Windows kimliği)" size="small" value={kullaniciAdi}
              onChange={(e) => setKullaniciAdi(e.target.value)} />
            <TextField label="Parola" size="small" type="password" value={sifre} onChange={(e) => setSifre(e.target.value)} />
          </Box>
          <Box>
            <Button variant="outlined" onClick={dbTest} disabled={mesgul || !sunucu || !veritabani}>
              {mesgul ? <CircularProgress size={18} /> : 'Bağlantıyı test et'}
            </Button>
          </Box>
          {dbSonuc && (
            <Alert severity={dbSonuc.basarili ? 'success' : 'error'}>
              {dbSonuc.mesaj}
              {dbSonuc.sunucuSurumu && <Box sx={{ fontSize: 11.5, mt: 0.5, opacity: 0.8 }}>{dbSonuc.sunucuSurumu}</Box>}
            </Alert>
          )}
        </Box>
      )}

      {adim === 2 && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          <Typography sx={{ fontSize: 13.5 }} color="text.secondary">
            Kullanıcı girişini yapan kimlik sağlayıcı (Keycloak). Şimdi boş bırakırsan kurulum
            tamamlanır ama kullanıcılar giriş yapamaz — sonradan Sistem Ayarları'ndan girilir.
          </Typography>
          <TextField label="Yetkili (realm adresi)" size="small" value={yetkili}
            onChange={(e) => setYetkili(e.target.value)} placeholder="https://kimlik.firma.com/realms/kds" />
          <TextField label="İstemci (audience)" size="small" value={istemci} onChange={(e) => setIstemci(e.target.value)} />
          <FormControlLabel control={<Checkbox checked={httpsZorunlu} onChange={(e) => setHttpsZorunlu(e.target.checked)} />}
            label="HTTPS zorunlu (üretimde açık kalmalı)" />
          <Box>
            <Button variant="outlined" onClick={kimlikDene} disabled={mesgul || !yetkili}>
              {mesgul ? <CircularProgress size={18} /> : 'Kimlik sağlayıcıyı test et'}
            </Button>
          </Box>
          {kimlikSonuc && (
            <Alert severity={kimlikSonuc.basarili ? 'success' : 'warning'}>
              {kimlikSonuc.mesaj}{kimlikSonuc.yayinci ? ` (issuer: ${kimlikSonuc.yayinci})` : ''}
            </Alert>
          )}
        </Box>
      )}

      {adim === 3 && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          <TextField label="Kiracı (müşteri) adı" size="small" value={kiraciAdi} onChange={(e) => setKiraciAdi(e.target.value)} />
          <TextField label="Kiracı kimliği (boş = üret)" size="small" value={kiraciId}
            onChange={(e) => setKiraciId(e.target.value)}
            helperText="Keycloak'taki tenant_id talebiyle aynı olmalı; boş bırakırsan üretilir ve sonunda gösterilir." />
          <TextField label="Önyüz adresi (CORS)" size="small" value={onyuzAdres} onChange={(e) => setOnyuzAdres(e.target.value)} />
          <TextField label="İçe alım depo sunucusu (isteğe bağlı)" size="small" value={depoSunucusu}
            onChange={(e) => setDepoSunucusu(e.target.value)}
            helperText="İçe alım verisinin yazılacağı SQL sunucusunun bağlantı dizesi; sonradan Sistem Ayarları'ndan da girilebilir." />
          <TextField label="Olay günlüğü (MongoDB, isteğe bağlı)" size="small" value={logBaglantisi}
            onChange={(e) => setLogBaglantisi(e.target.value)}
            helperText="Boş bırakılırsa olay günlüğü kapalı çalışır (uygulama etkilenmez)." />
        </Box>
      )}

      {adim === 4 && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          <Alert severity="info">
            Kurulum: <b>{veritabani}</b> veritabanı{dbSonuc?.veritabaniVar ? '' : ' (oluşturulacak)'} ·
            kiracı <b>{kiraciAdi}</b> · kimlik <b>{yetkili || 'girilmedi'}</b>
          </Alert>
          <Typography sx={{ fontSize: 13 }} color="text.secondary">
            "Kur" dediğinde şema kurulur, kiracı eklenir ve yapılandırma sunucuda şifreli olarak saklanır.
          </Typography>
          <Box>
            <Button variant="contained" size="large" onClick={kur} disabled={mesgul}>
              {mesgul ? <CircularProgress size={20} /> : 'Kur'}
            </Button>
          </Box>
        </Box>
      )}

      <Box sx={{ display: 'flex', gap: 1, mt: 3 }}>
        <Button disabled={adim === 0 || mesgul} onClick={() => setAdim((a) => a - 1)}>Geri</Button>
        <Box sx={{ flexGrow: 1 }} />
        {adim < adimlar.length - 1 && (
          <Button variant="contained" disabled={ileriKapali || mesgul} onClick={() => setAdim((a) => a + 1)}>İleri</Button>
        )}
      </Box>
    </Kabuk>
  );
}

function Kabuk({ children }: { children: React.ReactNode }) {
  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'var(--kds-dolgu)', p: 2 }}>
      <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 3, width: '100%', maxWidth: 640 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.2, mb: 2.5 }}>
          <StorageIcon color="primary" />
          <Box>
            <Typography variant="h6" color="primary">LST KDS — Kurulum</Typography>
            <Typography sx={{ fontSize: 12.5 }} color="text.secondary">
              Uygulama henüz yapılandırılmamış; birkaç adımda kuralım.
            </Typography>
          </Box>
        </Box>
        {children}
      </Paper>
    </Box>
  );
}
