import { useEffect, useRef, useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import Chip from '@mui/material/Chip';
import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import BusinessIcon from '@mui/icons-material/Business';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlineOutlined';
import { PageHeader, bandButtonWhite } from '../components/PageHeader';
import { LoadingCurtain } from '../components/Loading';
import { boxRadius } from '../theme';
import { getKurulus, saveKurulus, type Kurulus } from '../api/kurulus';
import { kadiYukleniyor, kadiHata, kadiBasarili } from '../kadi/kadiKanal';

/** Logo için üst sınır: komut doğrulamasıyla (300.000 karakter data: URI) uyumlu olsun diye
 * dosya boyutundan ölçülür — base64 ham dosyayı ~%33 büyütür. */
const LOGO_EN_COK_BAYT = 220 * 1024;

const VARSAYILAN_RENK = '#0D8B8B';

/** Kaynak rozetinin dili Sistem Ayarları ekranıyla AYNI tutulur — aynı kavram iki ekranda iki
 * farklı kelimeyle anlatılmasın. */
const kaynakEtiketi: Record<string, string> = {
  kiraci: 'bu kuruluşa özel',
  kurulum: 'kurulum geneli',
  varsayilan: 'varsayılan',
};

/** S35 — Yönetim → Kuruluş Ayarları: kiracının kendi kimliği (ad, kiracı kimliği, plan) ve
 * markası (renk + logo).
 *
 * Sorgu limitleri burada DÜZENLENMEZ, yalnız yürürlükteki değerleriyle gösterilir ve Sistem
 * Ayarları'na bağlanır: aynı eşiği iki ekranda düzenletmek, birinin diğerini sessizce geçersiz
 * kılmasıyla biter. */
export function KurulusAyarlariPage() {
  const [kurulus, setKurulus] = useState<Kurulus | null>(null);
  const [ad, setAd] = useState('');
  const [plan, setPlan] = useState('');
  const [renk, setRenk] = useState('');
  const [logo, setLogo] = useState<string | null>(null);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [kaydediyor, setKaydediyor] = useState(false);
  const [hata, setHata] = useState<string | null>(null);
  const [basari, setBasari] = useState<string | null>(null);
  const dosyaRef = useRef<HTMLInputElement>(null);

  const doldur = (k: Kurulus) => {
    setKurulus(k);
    setAd(k.ad);
    setPlan(k.plan ?? '');
    setRenk(k.markaRengi ?? '');
    setLogo(k.logoDataUri);
  };

  useEffect(() => {
    getKurulus()
      .then(doldur)
      .catch((e) => { setHata(e instanceof Error ? e.message : 'Kuruluş bilgisi alınamadı.'); kadiHata(); })
      .finally(() => setYukleniyor(false));
  }, []);

  const degisti = kurulus !== null && (
    ad !== kurulus.ad
    || plan !== (kurulus.plan ?? '')
    || renk !== (kurulus.markaRengi ?? '')
    || logo !== kurulus.logoDataUri
  );

  const kaydet = async () => {
    setKaydediyor(true); setHata(null); setBasari(null);
    const kadiBitir = kadiYukleniyor('Kuruluş bilgilerini kaydediyorum…');
    try {
      const sonuc = await saveKurulus({
        ad: ad.trim(),
        plan: plan.trim() || null,
        markaRengi: renk.trim() || null,
        logoDataUri: logo,
      });
      doldur(sonuc);
      setBasari('Kuruluş ayarları güncellendi.');
      kadiBasarili();
    } catch (e) {
      setHata(e instanceof Error ? e.message : 'Kaydedilemedi.');
      kadiHata();
    } finally {
      kadiBitir();
      setKaydediyor(false);
    }
  };

  const logoSec = (dosya: File | undefined) => {
    if (!dosya) return;
    if (!dosya.type.startsWith('image/')) { setHata('Logo bir görsel dosyası olmalı.'); return; }
    if (dosya.size > LOGO_EN_COK_BAYT) {
      // Sunucu da doğruluyor; burada erken uyarmak, 220 KB'lık dosyayı boşuna yüklemekten iyi.
      setHata(`Logo çok büyük (${Math.round(dosya.size / 1024)} KB) — 220 KB'ın altında bir görsel seçin.`);
      return;
    }
    const okuyucu = new FileReader();
    okuyucu.onload = () => { setLogo(String(okuyucu.result)); setHata(null); };
    okuyucu.onerror = () => setHata('Dosya okunamadı.');
    okuyucu.readAsDataURL(dosya);
  };

  if (yukleniyor) return <LoadingCurtain mesaj="Kuruluş bilgilerini getiriyorum…" />;

  return (
    <Box>
      <PageHeader ikon={<BusinessIcon />} baslik="Kuruluş Ayarları"
        aciklama="Kuruluşunuzun adı, planı ve markası. Sorgu limitleri Sistem Ayarları'nda yönetilir.">
        <Button variant="contained" onClick={() => void kaydet()} disabled={!degisti || kaydediyor || !ad.trim()}
          sx={bandButtonWhite}>
          Kaydet
        </Button>
      </PageHeader>

      {hata && <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => setHata(null)}>{hata}</Alert>}
      {basari && <Alert severity="success" sx={{ mb: 1.5 }} onClose={() => setBasari(null)}>{basari}</Alert>}

      {/* Kimlik */}
      <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 2, mb: 1.5 }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1.25 }} color="primary">Kimlik</Typography>
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 1.5 }}>
          <TextField label="Kuruluş adı" size="small" value={ad} onChange={(e) => setAd(e.target.value)}
            required error={!ad.trim()} helperText={!ad.trim() ? 'Kuruluş adı boş olamaz.' : ' '} />
          <TextField label="Plan / lisans" size="small" value={plan} onChange={(e) => setPlan(e.target.value)}
            placeholder="ör. Kurumsal"
            helperText="Şimdilik bilgi amaçlıdır; kota uygulamaz." />
        </Box>

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap', mt: 0.5 }}>
          <Typography sx={{ fontSize: 12.5 }} color="text.secondary">Kiracı kimliği:</Typography>
          <Box component="code" sx={{ fontSize: 12.5, bgcolor: 'var(--kds-zemin2)', px: 1, py: 0.4, borderRadius: 1 }}>
            {kurulus?.kiraciId}
          </Box>
          <Tooltip title="Kopyala">
            <IconButton size="small" onClick={() => void navigator.clipboard?.writeText(kurulus?.kiraciId ?? '')}>
              <ContentCopyIcon sx={{ fontSize: 15 }} />
            </IconButton>
          </Tooltip>
          <Chip size="small" label={kurulus?.durum} color={kurulus?.durum === 'Aktif' ? 'success' : 'default'}
            variant="outlined" sx={{ height: 20, fontSize: 11 }} />
        </Box>
        <Typography sx={{ fontSize: 11.5, mt: 0.75 }} color="text.disabled">
          Kimlik sağlayıcıdaki <b>tenant_id</b> talebi bu değerle birebir aynı olmalıdır — kullanıcılar
          aksi hâlde giriş yapsa bile kendi kuruluşlarının verisini göremez.
        </Typography>
      </Paper>

      {/* Marka */}
      <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 2, mb: 1.5 }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700 }} color="primary">Marka</Typography>
        <Typography sx={{ fontSize: 12, mb: 1.25 }} color="text.secondary">
          Kuruluşun logosu ve vurgu rengi. Gömülü analitik için beyaz-etiket ayarları ayrı bir
          ekrandadır (Gömme &amp; Marka).
        </Typography>

        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'flex-start' }}>
          <Box>
            <Typography sx={{ fontSize: 12, mb: 0.75 }} color="text.secondary">Logo</Typography>
            <Box sx={{
              width: 168, height: 84, border: '1px dashed', borderColor: 'divider', borderRadius: boxRadius,
              display: 'flex', alignItems: 'center', justifyContent: 'center', overflow: 'hidden',
              bgcolor: 'var(--kds-zemin2)',
            }}>
              {logo
                ? <Box component="img" src={logo} alt="Kuruluş logosu"
                    sx={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain' }} />
                : <Typography sx={{ fontSize: 11.5 }} color="text.disabled">logo yok</Typography>}
            </Box>
            <Box sx={{ display: 'flex', gap: 0.5, mt: 0.75 }}>
              <Button size="small" variant="outlined" onClick={() => dosyaRef.current?.click()}>
                {logo ? 'Değiştir' : 'Yükle'}
              </Button>
              {logo && (
                <Tooltip title="Logoyu kaldır">
                  <IconButton size="small" onClick={() => setLogo(null)}>
                    <DeleteOutlineIcon sx={{ fontSize: 17 }} />
                  </IconButton>
                </Tooltip>
              )}
              <input ref={dosyaRef} type="file" accept="image/*" hidden
                onChange={(e) => { logoSec(e.target.files?.[0]); e.target.value = ''; }} />
            </Box>
            <Typography sx={{ fontSize: 11, mt: 0.5 }} color="text.disabled">PNG/SVG, en çok 220 KB</Typography>
          </Box>

          <Box>
            <Typography sx={{ fontSize: 12, mb: 0.75 }} color="text.secondary">Vurgu rengi</Typography>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Box component="input" type="color" value={renk || VARSAYILAN_RENK}
                onChange={(e: React.ChangeEvent<HTMLInputElement>) => setRenk(e.target.value.toUpperCase())}
                sx={{ width: 46, height: 34, p: 0, border: '1px solid', borderColor: 'divider',
                  borderRadius: '8px', bgcolor: 'transparent', cursor: 'pointer' }} />
              <TextField size="small" value={renk} onChange={(e) => setRenk(e.target.value.toUpperCase())}
                placeholder={VARSAYILAN_RENK} sx={{ width: 132 }}
                slotProps={{ htmlInput: { maxLength: 7, style: { fontFamily: 'monospace' } } }} />
              {renk && (
                <Button size="small" onClick={() => setRenk('')}>Temizle</Button>
              )}
            </Box>
            <Typography sx={{ fontSize: 11, mt: 0.5 }} color="text.disabled">
              Boş bırakılırsa ürünün kendi rengi kullanılır.
            </Typography>
          </Box>
        </Box>
      </Paper>

      {/* Limitler — SALT GÖRÜNTÜ */}
      <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 2 }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700 }} color="primary">Yürürlükteki limitler</Typography>
        <Typography sx={{ fontSize: 12, mb: 1.25 }} color="text.secondary">
          Bu değerler <b>Sistem Ayarları</b>'nda tanımlıdır; burada yalnız gösterilir. Bir kuruluşa
          özel değer vermek de oradan yapılır.
        </Typography>
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(3, 1fr)' }, gap: 1.25 }}>
          {kurulus?.limitler.map((l) => (
            <Box key={l.anahtar} sx={{
              border: '1px solid', borderColor: 'divider', borderRadius: boxRadius, px: 1.5, py: 1.1,
            }}>
              <Typography sx={{ fontSize: 11.5 }} color="text.secondary">{l.etiket}</Typography>
              <Typography sx={{ fontSize: 19, fontWeight: 700, lineHeight: 1.25 }}>
                {Number(l.deger).toLocaleString('tr-TR')}
              </Typography>
              <Chip size="small" variant="outlined" label={kaynakEtiketi[l.kaynak] ?? l.kaynak}
                sx={{ height: 18, fontSize: 10.5, mt: 0.4 }} />
            </Box>
          ))}
        </Box>
        <Button component={RouterLink} to="/yonetim/ayarlar" size="small" sx={{ mt: 1.25 }}>
          Sistem Ayarları'na git
        </Button>
      </Paper>
    </Box>
  );
}
