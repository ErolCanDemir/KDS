import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import Chip from '@mui/material/Chip';
import Tooltip from '@mui/material/Tooltip';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import InsightsIcon from '@mui/icons-material/Insights';
import { PageHeader } from '../components/PageHeader';
import { LoadingCurtain } from '../components/Loading';
import { boxRadius } from '../theme';
import { getKullanim, type Kullanim } from '../api/kullanim';
import { kadiYukleniyor, kadiHata } from '../kadi/kadiKanal';

const DONEMLER = [7, 30, 90] as const;

const sure = (ms: number) => (ms >= 1000 ? `${(ms / 1000).toFixed(1)} sn` : `${ms} ms`);
const tarih = (s: string) => new Date(s).toLocaleDateString('tr-TR', { day: '2-digit', month: 'short', year: '2-digit' });

/** Özet kartı — tek bakışta okunacak sayı; ikincil satır sayının ne demek olduğunu söyler. */
function Kart({ etiket, deger, alt, renk }: { etiket: string; deger: string; alt?: string; renk?: string }) {
  return (
    <Paper variant="outlined" sx={{ borderRadius: boxRadius, px: 1.75, py: 1.4, minWidth: 0 }}>
      <Typography noWrap sx={{ fontSize: 11.5 }} color="text.secondary">{etiket}</Typography>
      <Typography sx={{ fontSize: 22, fontWeight: 700, lineHeight: 1.2, color: renk }}>{deger}</Typography>
      {alt && <Typography noWrap sx={{ fontSize: 11 }} color="text.disabled">{alt}</Typography>}
    </Paper>
  );
}

/** S41 — Yönetim → Kullanım: "neyi kullanıyoruz, ne yavaş?".
 *
 * Kaynak denetim kayıtlarıdır (S5) — yeni bir ölçüm altyapısı kurulmadı. "Yavaş" eşiği burada
 * TANIMLANMAZ, Sistem Ayarları'ndan okunur ve ekranda yalnız gösterilir; aynı eşik sonuç
 * ekranındaki "Sorun ne?" düğmesini de açar (S35'te limitlerde verilen kararın aynısı). */
export function KullanimPage() {
  const [gun, setGun] = useState<number>(30);
  const [veri, setVeri] = useState<Kullanim | null>(null);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [hata, setHata] = useState<string | null>(null);

  useEffect(() => {
    let iptal = false;
    setYukleniyor(true);
    const kadiBitir = kadiYukleniyor('Kullanım kayıtlarını topluyorum…', 'kos', 'hafif');
    getKullanim(gun)
      .then((d) => { if (!iptal) { setVeri(d); setHata(null); } })
      .catch((e) => { if (!iptal) { setHata(e instanceof Error ? e.message : 'Kullanım verisi alınamadı.'); kadiHata(); } })
      .finally(() => { kadiBitir(); if (!iptal) setYukleniyor(false); });
    return () => { iptal = true; kadiBitir(); };
  }, [gun]);

  if (yukleniyor && veri === null) return <LoadingCurtain mesaj="Kullanım kayıtlarını topluyorum…" />;

  const o = veri?.ozet;
  const hataOrani = o && o.toplamKosum > 0 ? Math.round((o.basarisizKosum / o.toplamKosum) * 100) : 0;

  return (
    <Box>
      <PageHeader ikon={<InsightsIcon />} baslik="Kullanım"
        aciklama="Hangi rapor ne sıklıkta çalışıyor, ne yavaş — kaynak denetim kayıtları.">
        <ToggleButtonGroup size="small" exclusive value={gun}
          onChange={(_, v: number | null) => { if (v) setGun(v); }}
          sx={{ bgcolor: 'rgba(255,255,255,0.14)', '& .MuiToggleButton-root': { color: '#fff', border: 'none', px: 1.5 },
            '& .Mui-selected': { bgcolor: 'rgba(255,255,255,0.28) !important' } }}>
          {DONEMLER.map((d) => <ToggleButton key={d} value={d}>{d} gün</ToggleButton>)}
        </ToggleButtonGroup>
      </PageHeader>

      {hata && <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => setHata(null)}>{hata}</Alert>}

      {/* ÖZET */}
      <Box sx={{ display: 'grid', gap: 1.25, mb: 1.5,
        gridTemplateColumns: { xs: 'repeat(2, 1fr)', md: 'repeat(5, 1fr)' } }}>
        <Kart etiket="Koşum" deger={(o?.toplamKosum ?? 0).toLocaleString('tr-TR')} alt={`son ${veri?.gunSayisi ?? gun} gün`} />
        <Kart etiket="Ortalama süre" deger={sure(o?.ortalamaSureMs ?? 0)} alt={`en uzun ${sure(o?.enUzunSureMs ?? 0)}`} />
        <Kart etiket="Yavaş koşum" deger={(o?.yavasKosum ?? 0).toLocaleString('tr-TR')}
          alt={`eşik ${sure(o?.yavasEsikMs ?? 0)}`} renk={(o?.yavasKosum ?? 0) > 0 ? 'warning.main' : undefined} />
        <Kart etiket="Başarısız" deger={`%${hataOrani}`} alt={`${(o?.basarisizKosum ?? 0).toLocaleString('tr-TR')} koşum`}
          renk={hataOrani > 0 ? 'error.main' : undefined} />
        <Kart etiket="Kullanıcı" deger={(o?.aktifKullanici ?? 0).toLocaleString('tr-TR')} alt="sorgu çalıştıran" />
      </Box>

      <Box sx={{ display: 'grid', gap: 1.5, gridTemplateColumns: { xs: '1fr', lg: '1fr 1fr' } }}>
        {/* EN ÇOK KULLANILAN RAPORLAR */}
        <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 2 }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700 }} color="primary">En çok çalışan raporlar</Typography>
          <Typography sx={{ fontSize: 12, mb: 1.25 }} color="text.secondary">
            Yalnız rapor üzerinden gelen koşumlar; tasarımcı ve SQL Editörü denemeleri sayılmaz.
          </Typography>
          {veri?.raporKullanimlari.length === 0 && (
            <Typography sx={{ fontSize: 12.5 }} color="text.disabled">Bu dönemde hiç rapor çalışmamış.</Typography>
          )}
          {veri?.raporKullanimlari.map((r) => (
            <Box key={r.raporId} sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 0.75,
              borderBottom: '1px solid', borderColor: 'divider' }}>
              <Box sx={{ minWidth: 0, flexGrow: 1 }}>
                <Typography noWrap sx={{ fontSize: 13, fontWeight: 600 }}>{r.ad}</Typography>
                <Typography noWrap sx={{ fontSize: 11.5 }} color="text.disabled">
                  ort. {sure(r.ortalamaSureMs)} · son {tarih(r.sonKosum)}
                  {r.basarisizSayisi > 0 ? ` · ${r.basarisizSayisi} hata` : ''}
                </Typography>
              </Box>
              <Chip size="small" label={`${r.kosumSayisi}×`} sx={{ height: 22, fontSize: 11.5, fontWeight: 700 }} />
            </Box>
          ))}
        </Paper>

        {/* ÖLÜ RAPORLAR */}
        <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 2 }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700 }} color="primary">Bu dönemde hiç çalışmayanlar</Typography>
          <Typography sx={{ fontSize: 12, mb: 1.25 }} color="text.secondary">
            Silmeden önce bakılacak liste. "Ölü" olmak seçili döneme GÖREdir — dönemi genişletince
            bir kısmı listeden çıkabilir.
          </Typography>
          {veri?.oluRaporlar.length === 0 && (
            <Typography sx={{ fontSize: 12.5 }} color="text.disabled">Bütün raporlar bu dönemde en az bir kez çalışmış.</Typography>
          )}
          {veri?.oluRaporlar.map((r) => (
            <Box key={r.id} sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 0.75,
              borderBottom: '1px solid', borderColor: 'divider' }}>
              <Typography noWrap sx={{ fontSize: 13, flexGrow: 1, minWidth: 0 }}>{r.ad}</Typography>
              <Typography noWrap sx={{ fontSize: 11.5 }} color="text.disabled">{tarih(r.olusturmaTarihi)}'de kuruldu</Typography>
              <Button size="small" component={RouterLink} to={`/raporlar/${r.id}`}>Aç</Button>
            </Box>
          ))}
        </Paper>
      </Box>

      {/* YAVAŞ KOŞUMLAR */}
      <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 2, mt: 1.5 }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700 }} color="primary">En yavaş koşumlar</Typography>
        <Typography sx={{ fontSize: 12, mb: 1.25 }} color="text.secondary">
          Eşik <b>{sure(o?.yavasEsikMs ?? 0)}</b> — Sistem Ayarları'nda tanımlı, burada yalnız gösterilir.
        </Typography>
        {veri?.yavasKosumlar.length === 0 && (
          <Typography sx={{ fontSize: 12.5 }} color="text.disabled">Eşiği aşan koşum yok.</Typography>
        )}
        <Box sx={{ overflowX: 'auto' }}>
          {veri?.yavasKosumlar.map((k) => (
            <Box key={k.id} sx={{ display: 'flex', alignItems: 'center', gap: 1.5, py: 0.75,
              borderBottom: '1px solid', borderColor: 'divider', minWidth: 520 }}>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: 'warning.main', width: 72, flexShrink: 0 }}>
                {sure(k.sureMs)}
              </Typography>
              <Box sx={{ minWidth: 0, flexGrow: 1 }}>
                <Typography noWrap sx={{ fontSize: 12.5, fontWeight: 600 }}>
                  {k.raporAd ?? 'Kayıtsız koşum (tasarımcı / SQL Editörü)'}
                </Typography>
                <Tooltip title={k.sqlOnizleme}>
                  <Typography noWrap sx={{ fontSize: 11, fontFamily: 'ui-monospace, monospace' }} color="text.disabled">
                    {k.sqlOnizleme}
                  </Typography>
                </Tooltip>
              </Box>
              <Typography noWrap sx={{ fontSize: 11.5, flexShrink: 0 }} color="text.secondary">
                {k.satirSayisi !== null ? `${k.satirSayisi.toLocaleString('tr-TR')} satır · ` : ''}
                {k.kullanici ?? '—'} · {tarih(k.zaman)}
              </Typography>
            </Box>
          ))}
        </Box>
        <Button component={RouterLink} to="/yonetim/denetim" size="small" sx={{ mt: 1.25 }}>
          Tam denetim kaydına git
        </Button>
      </Paper>
    </Box>
  );
}
