import { useEffect, useMemo, useState, useSyncExternalStore } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Radio from '@mui/material/Radio';
import RadioGroup from '@mui/material/RadioGroup';
import FormControlLabel from '@mui/material/FormControlLabel';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import Divider from '@mui/material/Divider';
import Tooltip from '@mui/material/Tooltip';
import TuneOutlinedIcon from '@mui/icons-material/TuneOutlined';
import RestartAltIcon from '@mui/icons-material/RestartAlt';
import { PageHeader, bandButtonWhite } from '../components/PageHeader';
import { LoadingCurtain } from '../components/Loading';
import { boxRadius } from '../theme';
import { getAyarlar, saveAyarlar, onyuzAyarlariniTazele, type Ayar } from '../api/ayarlar';
import { kadiYukleniyor, kadiHata, kadiBasarili , kadiSeviye, kadiSeviyeAbone, kadiSeviyeAyarla, type KadiSeviye } from '../kadi/kadiKanal';

/** S16/A3 — Yönetim → Sistem Ayarları: koda gömülü olan işletme değerleri (sorgu zaman aşımı,
 * satır limitleri, tanı eşikleri) buradan yönetilir. Değer varsayılana eşitlenirse kayıt silinir
 * (yani "varsayılan" durumu saklanmaz) — ekranda kaynak rozeti bunu gösterir. */
export function SettingsPage() {
  const [ayarlar, setAyarlar] = useState<Ayar[]>([]);
  const [taslak, setTaslak] = useState<Record<string, string>>({});
  const [yukleniyor, setYukleniyor] = useState(true);
  const [kaydediyor, setKaydediyor] = useState(false);
  const [hata, setHata] = useState<string | null>(null);
  const [basari, setBasari] = useState<string | null>(null);
  const seviye = useSyncExternalStore(kadiSeviyeAbone, kadiSeviye);

  async function yukle() {
    setHata(null);
    try {
      const liste = await getAyarlar();
      setAyarlar(liste);
      setTaslak(Object.fromEntries(liste.map((a) => [a.anahtar, a.deger])));
    } catch (e) { setHata((e as Error).message); kadiHata(); }
    finally { setYukleniyor(false); }
  }
  useEffect(() => { void yukle(); }, []);

  const gruplar = useMemo(() => {
    const s = new Map<string, Ayar[]>();
    for (const a of ayarlar) s.set(a.grup, [...(s.get(a.grup) ?? []), a]);
    return [...s.entries()];
  }, [ayarlar]);

  const degisenler = ayarlar.filter((a) => (taslak[a.anahtar] ?? a.deger) !== a.deger);

  async function kaydet() {
    const kadiBitir = kadiYukleniyor('Ayarları kaydediyorum…');
    setKaydediyor(true); setHata(null); setBasari(null);
    try {
      const sonuc = await saveAyarlar(Object.fromEntries(degisenler.map((a) => [a.anahtar, taslak[a.anahtar]])));
      onyuzAyarlariniTazele();
      setBasari(sonuc.degisen > 0 ? `${sonuc.degisen} ayar güncellendi.` : 'Değişiklik yok.'); kadiBasarili();
      await yukle();
    } catch (e) { setHata((e as Error).message); kadiHata(); }
    finally { kadiBitir(); setKaydediyor(false); }
  }

  if (yukleniyor) return <LoadingCurtain />;

  return (
    <Box>
      <PageHeader ikon={<TuneOutlinedIcon />} baslik="Sistem Ayarları"
        aciklama="Sorgu limitleri ve tanı eşikleri — kod değişmeden, kurulum genelinde geçerli."
        rozetler={[{ sayi: ayarlar.length, ad: 'ayar' }, { sayi: degisenler.length, ad: 'değişiklik' }]}>
        <Button variant="contained" onClick={() => void kaydet()} disabled={degisenler.length === 0 || kaydediyor}
          sx={bandButtonWhite}>
          Kaydet
        </Button>
      </PageHeader>

      {/* S29/7 — Kadi ayarları: BU TARAYICIYA özel (sunucu ayarı değil) */}
      <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 2, mb: 1.5 }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700 }} color="primary">Kadi (asistan)</Typography>
        <Typography sx={{ fontSize: 12, mb: 1 }} color="text.secondary">
          Konuşkanlık bu tarayıcıya özeldir. <b>Sessiz:</b> yalnız iş sahneleri kalır (uyarı nöbeti
          yine çalışır) · <b>Konuşkan:</b> her ekranda ipucu fısıldar · <b>Kapalı:</b> Kadi hiç görünmez.
        </Typography>
        <RadioGroup row value={seviye} onChange={(e) => kadiSeviyeAyarla(e.target.value as KadiSeviye)}>
          <FormControlLabel value="kapali" control={<Radio size="small" />} label="Kapalı" />
          <FormControlLabel value="sessiz" control={<Radio size="small" />} label="Sessiz" />
          <FormControlLabel value="normal" control={<Radio size="small" />} label="Normal" />
          <FormControlLabel value="konuskan" control={<Radio size="small" />} label="Konuşkan" />
        </RadioGroup>
      </Paper>

      {hata && <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => setHata(null)}>{hata}</Alert>}
      {basari && <Alert severity="success" sx={{ mb: 1.5 }} onClose={() => setBasari(null)}>{basari}</Alert>}

      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        {gruplar.map(([grup, satirlar]) => (
          <Paper key={grup} variant="outlined" sx={{ borderRadius: boxRadius, overflow: 'hidden' }}>
            <Box sx={{ px: 2, py: 1.1, bgcolor: 'var(--kds-zemin2)', borderBottom: '1px solid', borderColor: 'divider' }}>
              <Typography sx={{ fontSize: 13, fontWeight: 700 }} color="primary">{grup}</Typography>
            </Box>
            <Box sx={{ p: 2, display: 'flex', flexDirection: 'column', gap: 2 }}>
              {satirlar.map((a, i) => (
                <Box key={a.anahtar}>
                  {i > 0 && <Divider sx={{ mb: 2 }} />}
                  <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 220px' }, gap: 2, alignItems: 'start' }}>
                    <Box>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.8 }}>
                        <Typography sx={{ fontSize: 13.5, fontWeight: 600 }}>{a.etiket}</Typography>
                        <Box component="span" sx={{
                          fontSize: 10, fontWeight: 700, textTransform: 'uppercase', letterSpacing: 0.3,
                          px: 0.8, py: '1px', borderRadius: 999,
                          bgcolor: a.kaynak === 'varsayılan' ? 'var(--kds-dolgu)' : 'var(--kds-tealdolgu)',
                          color: a.kaynak === 'varsayılan' ? 'text.secondary' : 'secondary.main',
                        }}>{a.kaynak}</Box>
                      </Box>
                      <Typography sx={{ fontSize: 12.5, mt: 0.3 }} color="text.secondary">{a.aciklama}</Typography>
                      <Typography sx={{ fontSize: 11, mt: 0.3 }} color="text.disabled">
                        {a.anahtar} · varsayılan: {a.varsayilan}
                      </Typography>
                    </Box>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                      <TextField size="small" fullWidth
                        type={a.tip === 'Sayi' ? 'number' : a.tip === 'Sir' ? 'password' : 'text'}
                        value={taslak[a.anahtar] ?? ''}
                        placeholder={a.tip === 'Sir' && a.dolu ? '•••••• (girili — değiştirmek için yaz)' : ''}
                        onChange={(e) => setTaslak((t) => ({ ...t, [a.anahtar]: e.target.value }))} />
                      <Tooltip title="Varsayılana döndür">
                        <span>
                          <Button size="small" sx={{ minWidth: 34, px: 0.5 }}
                            disabled={(taslak[a.anahtar] ?? '') === a.varsayilan}
                            onClick={() => setTaslak((t) => ({ ...t, [a.anahtar]: a.varsayilan }))}>
                            <RestartAltIcon sx={{ fontSize: 18 }} />
                          </Button>
                        </span>
                      </Tooltip>
                    </Box>
                  </Box>
                </Box>
              ))}
            </Box>
          </Paper>
        ))}
      </Box>
    </Box>
  );
}
