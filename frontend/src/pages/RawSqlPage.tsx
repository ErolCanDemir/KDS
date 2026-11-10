import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useSearchParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import Alert from '@mui/material/Alert';
import Dialog from '@mui/material/Dialog';
import DialogContent from '@mui/material/DialogContent';
import Radio from '@mui/material/Radio';
import RadioGroup from '@mui/material/RadioGroup';
import FormControlLabel from '@mui/material/FormControlLabel';
import CircularProgress from '@mui/material/CircularProgress';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import ShieldOutlinedIcon from '@mui/icons-material/ShieldOutlined';
import CodeIcon from '@mui/icons-material/Code';
import { PageHeader, whiteBox, bandButton, bandButtonWhite } from '../components/PageHeader';
import SaveOutlinedIcon from '@mui/icons-material/SaveOutlined';
import CloseIcon from '@mui/icons-material/Close';
import { getConnections, type Connection } from '../api/connections';
import { runRawSql, diagnoseRawSql, type QueryRunResult } from '../api/queries';
import { sqlParametreleriBul, sqlParametreleriHazirla, type SqlParamGirdi } from './sqlParams';
import {
  getReport, addReport, updateReport, getFolders, addFolder,
  type FolderDto, type VisualizationType, type ReportSharing,
} from '../api/reports';
import { SqlEditor } from '../components/SqlEditor';
import { ResultTable } from '../components/ResultTable';
import { SorunNeButonu, useYavasEsik } from '../components/SorunNe';
import { LoadingCurtain, RunningIndicator } from '../components/Loading';
import { vizDisplay } from '../components/reportVisual';
import { boxRadius, smallRadius } from '../theme';
import { kadiYukleniyor, kadiHata, kadiBasarili } from '../kadi/kadiKanal';

const ornekSql = 'SELECT TOP (100) *\nFROM dbo.Baglantilar';

/** Ham SQL ekranı (S9) — ileri kullanıcı için. Tasarımcıyla AYNI tek kapıdan geçer:
 * SELECT-only bekçisi + satır sınırı + zaman aşımı + denetim kaydı. Yazılan sorgu
 * "Rapor Olarak Kaydet" ile görsel raporlarla aynı menüye (Raporlarım) kaydedilir. */
export function RawSqlPage() {
  const [arananParam] = useSearchParams();
  // S16/A3: "yavaş sorgu" eşiği Sistem Ayarları'ndan (koda gömülü değil).
  const yavasEsik = useYavasEsik();
  const [baglantilar, setBaglantilar] = useState<Connection[]>([]);
  const [baglantiId, setBaglantiId] = useState('');
  const [sql, setSql] = useState(ornekSql);
  const [limit, setLimit] = useState('1000');
  const [sonuc, setSonuc] = useState<QueryRunResult | null>(null);
  const [mesgul, setMesgul] = useState(false);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [hata, setHata] = useState<string | null>(null);
  const [basari, setBasari] = useState<string | null>(null);

  // Rapor bağlamı: ?rapor=id ile yüklenen mevcut SQL raporu; Kaydet bunu günceller.
  const [raporBilgi, setRaporBilgi] = useState<{ id: string; ad: string; klasorId: string | null; paylasim: ReportSharing; gorsellestirme: VisualizationType } | null>(null);
  const [kaydetAcik, setKaydetAcik] = useState(false);
  const [klasorler, setKlasorler] = useState<FolderDto[]>([]);
  const [kAd, setKAd] = useState('');
  const [kKlasor, setKKlasor] = useState('');       // '' = klasörsüz, '__yeni' = yeni klasör
  const [kYeniKlasor, setKYeniKlasor] = useState('');
  const [kPaylasim, setKPaylasim] = useState<ReportSharing>('Ozel');
  const [kTur, setKTur] = useState<VisualizationType>('Tablo');
  const [kaydediyor, setKaydediyor] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const b = await getConnections();
        setBaglantilar(b);
        const raporId = arananParam.get('rapor');
        if (raporId) {
          const r = await getReport(raporId);
          if (r.hamSql !== null) {
            setSql(r.hamSql);
            setBaglantiId(r.baglantiId ?? '');
            setRaporBilgi({ id: r.id, ad: r.ad, klasorId: r.klasorId, paylasim: r.paylasim, gorsellestirme: r.gorsellestirme });
            return;
          }
        }
        if (b[0]) setBaglantiId(b[0].id);
      } catch (e) { setHata((e as Error).message); kadiHata(); }
      finally { setYukleniyor(false); }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- yalnız ilk yükleme
  }, []);

  // O8: sayfadan ayrılınca uçuştaki koşum iptal edilir.
  const iptalRef = useRef<AbortController | null>(null);
  useEffect(() => () => iptalRef.current?.abort(), []);

  // M11: SQL'deki @parametreler algılanır; değerleri panelden girilir ve GERÇEK SqlParameter
  // olarak bağlanır (metin değiştirme değil — enjeksiyon-güvenli "değeri yerine koyma").
  const paramAdlari = useMemo(() => sqlParametreleriBul(sql), [sql]);
  const [paramGirdileri, setParamGirdileri] = useState<Record<string, SqlParamGirdi | undefined>>({});
  const paramDegistir = (ad: string, d: Partial<SqlParamGirdi>) =>
    setParamGirdileri((p) => ({ ...p, [ad]: { tip: 'Metin', deger: '', ...p[ad], ...d } }));

  async function calistir() {
    if (!baglantiId || !sql.trim() || mesgul) return;
    let parametreler: Record<string, unknown> | null = null;
    if (paramAdlari.length > 0) {
      const hazir = sqlParametreleriHazirla(paramAdlari, paramGirdileri);
      if ('hata' in hazir) { setHata(hazir.hata); kadiHata(); return; }
      parametreler = hazir.parametreler;
    }
    setMesgul(true); setHata(null);
    iptalRef.current?.abort();
    const iptal = new AbortController();
    iptalRef.current = iptal;
    try { setSonuc(await runRawSql(baglantiId, sql, limit.trim() ? Number(limit) : null, parametreler, iptal.signal)); kadiBasarili('Sorguyu tamamladım — sonuç aşağıda.'); }
    // kadiHata iptal filtresinin İÇİNDE: sayfa terk edilince/iptalde Kadi üzülmemeli.
    catch (e) { if ((e as Error).name !== 'AbortError') { setHata((e as Error).message); kadiHata(); } }
    finally { setMesgul(false); }
  }

  /** M33 "Sorun ne?": editördeki SQL'i ÇALIŞTIRMADAN tanılatır (tahmini plan yorumu). */
  async function tanila() {
    let parametreler: Record<string, unknown> | null = null;
    if (paramAdlari.length > 0) {
      const hazir = sqlParametreleriHazirla(paramAdlari, paramGirdileri);
      if ('hata' in hazir) throw new Error(hazir.hata);
      parametreler = hazir.parametreler;
    }
    return diagnoseRawSql(baglantiId, sql, parametreler);
  }

  async function openSave() {
    setKAd(raporBilgi?.ad ?? '');
    setKKlasor(raporBilgi?.klasorId ?? '');
    setKYeniKlasor('');
    setKPaylasim(raporBilgi?.paylasim ?? 'Ozel');
    setKTur(raporBilgi?.gorsellestirme ?? 'Tablo');
    setKaydetAcik(true);
    try { setKlasorler(await getFolders()); } catch { /* klasörsüz devam */ }
  }

  async function saveReport() {
    if (!kAd.trim() || !baglantiId) return;
    setKaydediyor(true); setHata(null);
    const kadiBitir = kadiYukleniyor('Raporu kaydediyorum…');
    try {
      let klasorId: string | null = kKlasor || null;
      if (kKlasor === '__yeni') klasorId = (await addFolder(kYeniKlasor.trim())).id;
      const govde = {
        ad: kAd.trim(), klasorId, model: null, hamSql: sql, baglantiId,
        gorsellestirme: kTur, paylasim: kPaylasim,
      };
      const ozet = raporBilgi ? await updateReport(raporBilgi.id, govde) : await addReport(govde);
      setRaporBilgi({ id: ozet.id, ad: ozet.ad, klasorId: ozet.klasorId, paylasim: ozet.paylasim, gorsellestirme: ozet.gorsellestirme });
      setKaydetAcik(false);
      setBasari(`"${ozet.ad}" kaydedildi — Raporlarım'da.`); kadiBasarili();
    } catch (e) { setHata((e as Error).message); kadiHata(); }
    finally { kadiBitir(); setKaydediyor(false); }
  }

  if (yukleniyor) {
    return <LoadingCurtain />;
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <PageHeader ikon={<CodeIcon />} baslik="SQL Editörü"
        aciklama={<>
          İleri kullanıcı için ham SQL — tasarımcıyla aynı güvenlik kapısından geçer.
          {raporBilgi && <> · <b>{raporBilgi.ad}</b> raporunu düzenliyorsun</>}
        </>}>
        <Typography sx={{ fontSize: 12, fontWeight: 700, opacity: 0.9 }}>Bağlantı</Typography>
        <Select size="small" displayEmpty value={baglantiId} onChange={(e) => setBaglantiId(e.target.value)}
          sx={{ minWidth: 190, ...whiteBox }}>
          <MenuItem value=""><em>Bağlantı seç…</em></MenuItem>
          {baglantilar.map((b) => <MenuItem key={b.id} value={b.id}>{b.ad}</MenuItem>)}
        </Select>
        <TextField size="small" type="number" placeholder="satır limiti" value={limit}
          onChange={(e) => setLimit(e.target.value)} sx={{ width: 110, ...whiteBox }} />
        <Button variant="contained" startIcon={mesgul ? <CircularProgress size={14} color="inherit" /> : <PlayArrowIcon />}
          onClick={() => void calistir()} disabled={!baglantiId || !sql.trim() || mesgul} sx={bandButtonWhite}>
          Çalıştır
        </Button>
        <Button variant="outlined" startIcon={<SaveOutlinedIcon />}
          onClick={() => void openSave()} disabled={!baglantiId || !sql.trim()} sx={bandButton}>
          {raporBilgi ? 'Raporu Güncelle' : 'Rapor Olarak Kaydet'}
        </Button>
      </PageHeader>

      {baglantilar.length === 0 && <Alert severity="info">Önce bir <b>Bağlantı</b> ekleyin.</Alert>}
      {hata && <Alert severity="error" onClose={() => setHata(null)}
        action={hata.includes('zaman aşımı') ? <SorunNeButonu tanila={tanila} /> : undefined}>{hata}</Alert>}
      {basari && <Alert severity="success" onClose={() => setBasari(null)}>{basari}</Alert>}

      <Paper variant="outlined" sx={{ borderRadius: boxRadius, overflow: 'hidden' }}>
        <SqlEditor deger={sql} degisti={setSql} calistir={() => void calistir()} yukseklik={280} />
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.8, px: 1.5, py: 0.7, borderTop: '1px solid', borderColor: 'divider', bgcolor: 'var(--kds-zemin2)' }}>
          <ShieldOutlinedIcon sx={{ fontSize: 14, color: 'secondary.main' }} />
          <Typography sx={{ fontSize: 11.5 }} color="text.secondary">
            Sadece <b>tek bir SELECT</b> sorgusu çalıştırılabilir (INSERT/UPDATE/DELETE engellenir) · sonuç satır limitiyle sınırlıdır · uzun süren sorgu otomatik durdurulur · her çalıştırma denetim kaydına yazılır · <b>Ctrl+Enter</b> = çalıştır · SQL'de <b>@ad</b> yazarsan parametre paneli açılır
          </Typography>
        </Box>
      </Paper>

      {/* M11: SQL'deki @parametreler — değerler çalıştırmada gerçek SqlParameter olarak bağlanır. */}
      {paramAdlari.length > 0 && (
        <Paper variant="outlined" sx={{ borderRadius: boxRadius, px: 1.75, py: 1.25, display: 'flex', flexWrap: 'wrap', gap: 1.75, alignItems: 'flex-end' }}>
          <Typography sx={{ fontSize: 12, fontWeight: 800, color: 'primary.main', alignSelf: 'center' }}>Parametreler</Typography>
          {paramAdlari.map((ad) => {
            const g = paramGirdileri[ad] ?? { tip: 'Metin' as const, deger: '' };
            return (
              <Box key={ad} sx={{ display: 'flex', flexDirection: 'column', gap: 0.35 }}>
                <Typography sx={{ fontSize: 11.5, fontWeight: 700 }} color="secondary.main">@{ad}</Typography>
                <Box sx={{ display: 'flex', gap: 0.6 }}>
                  <Select size="small" value={g.tip} onChange={(e) => paramDegistir(ad, { tip: e.target.value as SqlParamGirdi['tip'] })}
                    sx={{ minWidth: 92, fontSize: 12.5 }}>
                    <MenuItem value="Metin" sx={{ fontSize: 12.5 }}>Metin</MenuItem>
                    <MenuItem value="Sayi" sx={{ fontSize: 12.5 }}>Sayı</MenuItem>
                    <MenuItem value="Tarih" sx={{ fontSize: 12.5 }}>Tarih</MenuItem>
                  </Select>
                  {g.tip === 'Tarih'
                    ? <TextField size="small" type="date" value={g.deger} onChange={(e) => paramDegistir(ad, { deger: e.target.value })} sx={{ width: 160 }} />
                    : <TextField size="small" placeholder="değer" value={g.deger} onChange={(e) => paramDegistir(ad, { deger: e.target.value })} sx={{ width: 160 }} />}
                </Box>
              </Box>
            );
          })}
        </Paper>
      )}

      <Paper variant="outlined" sx={{ borderRadius: boxRadius, overflow: 'hidden' }}>
        {sonuc ? (
          <>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, px: 1.5, py: 0.8, borderBottom: '1px solid', borderColor: 'divider', bgcolor: 'var(--kds-zemin2)' }}>
              <Typography sx={{ fontSize: 12.5, fontWeight: 600 }} color="text.secondary">
                {sonuc.satirlar.length} satır{sonuc.satirSiniriUygulandi ? ' (sınıra ulaşıldı)' : ''}
              </Typography>
              {/* M33: belirgin yavaş koşumda tanı butonu */}
              {sonuc.sureMs > yavasEsik && <SorunNeButonu tanila={tanila} />}
              <Box sx={{ flexGrow: 1 }} />
              {/* M10: Dışa Aktar yalnız KAYITLI raporda (görüntüleyici) — editörde yok. */}
            </Box>
            <ResultTable sonuc={sonuc} />
          </>
        ) : (
          <Box sx={{ p: 3, textAlign: 'center' }}>
            <Typography sx={{ fontSize: 12.5 }} color="text.secondary">Sonuçlar burada görünecek — sorguyu yazıp <b>Çalıştır</b>'a bas.</Typography>
          </Box>
        )}
      </Paper>

      {/* Rapor Olarak Kaydet diyaloğu — tasarımcıdakiyle aynı desen */}
      <Dialog open={kaydetAcik} onClose={() => setKaydetAcik(false)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { borderRadius: boxRadius } } }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, pt: 2 }}>
          <Typography variant="h6" color="primary">{raporBilgi ? 'Raporu Güncelle' : 'Rapor Olarak Kaydet'}</Typography>
          <IconButton size="small" onClick={() => setKaydetAcik(false)}><CloseIcon fontSize="small" /></IconButton>
        </Box>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.75 }}>
          <Box>
            <DialogLabel>Rapor adı</DialogLabel>
            <TextField size="small" fullWidth autoFocus value={kAd} onChange={(e) => setKAd(e.target.value)} placeholder="ör. Marka bazında ciro" />
          </Box>
          <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 1.5 }}>
            <Box>
              <DialogLabel>Klasör</DialogLabel>
              <Select size="small" fullWidth displayEmpty value={kKlasor} onChange={(e) => setKKlasor(e.target.value)}>
                <MenuItem value=""><em>Klasörsüz</em></MenuItem>
                {klasorler.map((k) => <MenuItem key={k.id} value={k.id}>{k.ad}</MenuItem>)}
                <MenuItem value="__yeni">＋ Yeni klasör…</MenuItem>
              </Select>
              {kKlasor === '__yeni' && (
                <TextField size="small" fullWidth sx={{ mt: 0.75 }} placeholder="Yeni klasör adı" value={kYeniKlasor} onChange={(e) => setKYeniKlasor(e.target.value)} />
              )}
            </Box>
            <Box>
              <DialogLabel>Paylaşım</DialogLabel>
              <RadioGroup row value={kPaylasim} onChange={(e) => setKPaylasim(e.target.value as ReportSharing)} sx={{ pt: 0.25 }}>
                <FormControlLabel value="Ozel" control={<Radio size="small" />} label={<Typography sx={{ fontSize: 12.5 }}>Özel</Typography>} />
                <FormControlLabel value="Ekip" control={<Radio size="small" />} label={<Typography sx={{ fontSize: 12.5 }}>Ekiple paylaş</Typography>} />
              </RadioGroup>
            </Box>
          </Box>
          <Box>
            <DialogLabel>Görselleştirme</DialogLabel>
            <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 0.75 }}>
              {(Object.keys(vizDisplay) as VisualizationType[]).map((t) => {
                const g = vizDisplay[t];
                const secili = kTur === t;
                return (
                  <Box key={t} onClick={() => setKTur(t)}
                    sx={{ border: '1.5px solid', borderColor: secili ? 'secondary.main' : 'divider', borderRadius: smallRadius, py: 0.9, textAlign: 'center', cursor: 'pointer', bgcolor: secili ? 'var(--kds-tealdolgu)' : 'var(--kds-zemin2)' }}>
                    <Box sx={{ color: secili ? g.renk : 'text.disabled', display: 'flex', justifyContent: 'center' }}>{g.ikon}</Box>
                    <Typography sx={{ fontSize: 9.5, fontWeight: 700, mt: 0.4, color: secili ? 'secondary.main' : 'text.secondary' }}>{g.ad}</Typography>
                  </Box>
                );
              })}
            </Box>
          </Box>
        </DialogContent>
        <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1, px: 3, pb: 2 }}>
          <Button color="inherit" onClick={() => setKaydetAcik(false)}>İptal</Button>
          <Button variant="contained" onClick={() => void saveReport()}
            disabled={!kAd.trim() || kaydediyor || (kKlasor === '__yeni' && !kYeniKlasor.trim())}
            startIcon={kaydediyor ? <CircularProgress size={14} color="inherit" /> : <SaveOutlinedIcon />}>
            Kaydet
          </Button>
        </Box>
      </Dialog>

      {mesgul && <RunningIndicator />}
    </Box>
  );
}

function DialogLabel({ children }: { children: ReactNode }) {
  return (
    <Typography sx={{ fontSize: 10.5, fontWeight: 800, textTransform: 'uppercase', letterSpacing: 0.4, mb: 0.5 }} color="text.secondary">
      {children}
    </Typography>
  );
}
