import { useEffect, useMemo, useRef, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import RefreshIcon from '@mui/icons-material/Refresh';
import DesignServicesOutlinedIcon from '@mui/icons-material/DesignServicesOutlined';
import { useSession } from '../identity/sessionContext';
import { getReport, runReport, type ReportDetail, type VisualizationType } from '../api/reports';
import type { QueryRunResult, ParametreDegerleri } from '../api/queries';
import { ParametrePanel, ilkParametreDegerleri } from '../components/ParametrePanel';
import { cacheRead, cacheWrite } from '../api/reportCache';
import { EChart } from '../components/EChart';
import { ResultTable } from '../components/ResultTable';
import { vizDisplay, chartPalette } from '../components/reportVisual';
import { LoadingCurtain, RunningIndicator } from '../components/Loading';
import { PageHeader, bandButton, bandButtonWhite } from '../components/PageHeader';
import { ExportMenu } from '../components/ExportMenu';
import { exportReport } from '../api/export';
import { boxRadius } from '../theme';
import type { EChartsCoreOption } from 'echarts/core';
import { kadiYukleniyor, kadiHata, kadiBasarili } from '../kadi/kadiKanal';

const turSirasi: VisualizationType[] = ['Tablo', 'Cubuk', 'Cizgi', 'Pasta', 'SayiKarti'];

export function ReportViewPage() {
  const { id } = useParams<{ id: string }>();
  const git = useNavigate();
  // M30: panodan gelindiyse geri tuşu panoya döner (state.donus); doğrudan gelişte raporlara.
  const donus = (useLocation().state as { donus?: string } | null)?.donus ?? '/raporlar';
  const oturum = useSession();
  const yetkili = oturum.roller.includes('admin') || oturum.roller.includes('gelistirici');

  const [rapor, setRapor] = useState<ReportDetail | null>(null);
  const [sonuc, setSonuc] = useState<QueryRunResult | null>(null);
  const [tur, setTur] = useState<VisualizationType>('Tablo');
  const [yukleniyor, setYukleniyor] = useState(true);
  const [calisiyor, setCalisiyor] = useState(false);
  const [hata, setHata] = useState<string | null>(null);
  const [sonZaman, setSonZaman] = useState<string | null>(null); // önbellekteki sonucun zamanı
  const [parametreDegerleri, setParametreDegerleri] = useState<ParametreDegerleri>({});

  // Rapor kimliğiyle koşulur (kod incelemesi K1): /api/raporlar/{id}/calistir TÜM rollere açık,
  // görünürlük backend'de — musteri de paylaşılan raporu çalıştırabilir.
  // O8: sayfadan ayrılınca uçuştaki koşum iptal edilir (unmounted setState önlenir).
  const iptalRef = useRef<AbortController | null>(null);
  useEffect(() => () => iptalRef.current?.abort(), []);
  const raporKos = (d: ReportDetail, signal?: AbortSignal) => runReport(d.id, parametreDegerleri, signal);

  // D6: StrictMode dev'de bu effect'i 2× koşturur — yalnız okuma (getReport + önbellek) yaptığı
  // için idempotenttir; koşum içermez (sorgu yalnız Yenile/Çalıştır ile).
  // Açılışta sorgu KOŞULMAZ (ağır rapor ekranı kilitleyip zaman aşımına düşebiliyordu):
  // ekran hemen açılır, SON çalıştırmanın sonucu (bu tarayıcıda) önbellekten gelir;
  // güncel veri istenirse "Yenile". Önbellek localStorage'ta rapor başına tutulur.
  useEffect(() => {
    (async () => {
      if (!id) return;
      try {
        const d = await getReport(id);
        setRapor(d);
        setTur(d.gorsellestirme);
        setParametreDegerleri(ilkParametreDegerleri(d.model?.parametreler ?? []));
        const c = cacheRead(id); // O3: LRU'lu önbellek modülü
        if (c) {
          setSonuc(c.sonuc);
          setSonZaman(c.zaman);
        }
      } catch (e) { setHata((e as Error).message); kadiHata(); }
      finally { setYukleniyor(false); }
    })();
  }, [id]);

  async function refresh() {
    if (!rapor) return;
    setCalisiyor(true); setHata(null);
    iptalRef.current?.abort();
    const iptal = new AbortController();
    iptalRef.current = iptal;
    try {
      const s = await raporKos(rapor, iptal.signal);
      setSonuc(s);
      const zaman = new Date().toLocaleString('tr-TR');
      setSonZaman(zaman);
      cacheWrite(rapor.id, { sonuc: s, zaman }); // O3: LRU + kota geri-kazanımı modülde
      kadiBasarili('Raporu tamamladım — sonuç burada.');
    }
    catch (e) {
      if ((e as Error).name === 'AbortError') return; // sayfa terk edildi — sessiz
      setHata((e as Error).message); kadiHata();
    }
    finally { setCalisiyor(false); }
  }

  // Kolon sınıflaması: alan sırası SELECT sırasıdır → sonuç kolonlarıyla birebir.
  // SQL raporunda model yok → boyut/ölçü tamamen değerlerden (sayisalIdx) çıkarılır.
  const { boyutIdx, olcuIdx } = useMemo(() => {
    const b: number[] = [], o: number[] = [];
    rapor?.model?.alanlar.forEach((a, i) => (a.toplama === 'Yok' ? b : o).push(i));
    return { boyutIdx: b, olcuIdx: o };
  }, [rapor]);

  // GERÇEKTEN sayısal kolonlar (değerlere bakarak): GUID/metin kolonlar Number()'da NaN olup
  // grafiği sessizce boş bırakıyordu — ölçü adayı yalnız bunlardan seçilir.
  const sayisalIdx = useMemo(() => {
    if (!sonuc) return [] as number[];
    return sonuc.kolonlar.map((_, i) => i).filter((i) => {
      let dolu = 0;
      for (const r of sonuc.satirlar) {
        const v = r[i];
        if (v === null || v === undefined) continue;
        dolu++;
        if (typeof v !== 'number') return false;
      }
      return dolu > 0;
    });
  }, [sonuc]);

  const grafikSecenek = useMemo<EChartsCoreOption | null>(() => {
    if (!sonuc || sonuc.kolonlar.length === 0) return null;
    if (tur !== 'Cubuk' && tur !== 'Cizgi' && tur !== 'Pasta') return null;
    // SQL raporunda model (dolayısıyla boyut listesi) yok → ilk sayısal-olmayan kolon boyut olur.
    const bIdx = boyutIdx.find((i) => !sayisalIdx.includes(i)) ?? boyutIdx[0]
      ?? sonuc.kolonlar.findIndex((_, i) => !sayisalIdx.includes(i));
    // Ölçü adayları: modeldeki toplamalı alanlardan sayısal olanlar; yoksa herhangi bir sayısal kolon.
    const oIdxler = (olcuIdx.some((i) => sayisalIdx.includes(i))
      ? olcuIdx.filter((i) => sayisalIdx.includes(i))
      : sayisalIdx.filter((i) => i !== bIdx));
    if (oIdxler.length === 0) return null; // sayısal ölçü yok → grafik yerine yönlendirme göster
    const etiketler = sonuc.satirlar.map((r, i) => (bIdx >= 0 ? String(r[bIdx] ?? '—') : `#${i + 1}`));

    if (tur === 'Pasta') {
      const oIdx = oIdxler[0] ?? 0;
      return {
        color: chartPalette,
        tooltip: { trigger: 'item' },
        legend: { bottom: 0, type: 'scroll' },
        series: [{
          type: 'pie', radius: ['38%', '68%'],
          itemStyle: { borderRadius: 5, borderColor: '#fff', borderWidth: 2 },
          data: sonuc.satirlar.map((r, i) => ({ name: etiketler[i], value: Number(r[oIdx] ?? 0) })),
        }],
      };
    }
    return {
      color: chartPalette,
      tooltip: { trigger: 'axis' },
      legend: oIdxler.length > 1 ? { bottom: 0 } : undefined,
      grid: { left: 48, right: 20, top: 24, bottom: oIdxler.length > 1 ? 42 : 28 },
      xAxis: { type: 'category', data: etiketler, axisLabel: { fontSize: 11 } },
      yAxis: { type: 'value', axisLabel: { fontSize: 11 } },
      series: oIdxler.map((oi) => ({
        name: sonuc.kolonlar[oi]?.ad,
        type: tur === 'Cubuk' ? 'bar' : 'line',
        smooth: tur === 'Cizgi',
        barMaxWidth: 42,
        data: sonuc.satirlar.map((r) => Number(r[oi] ?? 0)),
      })),
    };
  }, [sonuc, tur, boyutIdx, olcuIdx, sayisalIdx]);

  if (yukleniyor) {
    return <LoadingCurtain />;
  }
  if (!rapor) {
    return <Alert severity="error">{hata ?? 'Rapor bulunamadı.'}</Alert>;
  }

  return (
    <Box>
      <PageHeader
        sol={<IconButton onClick={() => git(donus)} sx={{ color: '#fff', ml: -1 }}><ArrowBackIcon /></IconButton>}
        ikon={vizDisplay[tur].ikon}
        baslik={rapor.ad}
        aciklama={<>
          {rapor.benimMi ? 'benim raporum' : (rapor.sahipAd ?? '—')}{rapor.paylasim === 'Ekip' ? ' · ekiple paylaşılan' : ' · özel'}
          {sonuc ? ` · ${sonuc.satirlar.length} satır · ${sonuc.sureMs} ms` : ''}
          {sonZaman ? ` · son çalıştırma: ${sonZaman}` : ''}
        </>}>
        {/* tür değiştirici — oturumluk; kalıcı tercih raporla kaydedilir (tasarımcıdan) */}
        <Box sx={{ display: 'flex', gap: 0.4 }}>
          {turSirasi.map((t) => {
            const g = vizDisplay[t];
            const secili = tur === t;
            return (
              <Tooltip key={t} title={g.ad}>
                {/* S25 sonrası şerit İÇERİK alanında — banda göre beyaz stiller açık modda
                    görünmezdi; tema token'larıyla iki modda da okunur */}
                <Box onClick={() => setTur(t)}
                  sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', width: 30, height: 30, borderRadius: '7px', cursor: 'pointer',
                    border: '1px solid', borderColor: secili ? 'secondary.main' : 'divider',
                    bgcolor: secili ? 'var(--kds-tealdolgu)' : 'var(--kds-zemin2)',
                    color: secili ? g.renk : 'text.secondary',
                    '&:hover': { borderColor: 'secondary.main' } }}>
                  {g.ikon}
                </Box>
              </Tooltip>
            );
          })}
        </Box>
        <Button variant="outlined" startIcon={calisiyor ? <CircularProgress size={14} color="inherit" /> : <RefreshIcon />}
          onClick={() => void refresh()} disabled={calisiyor} sx={bandButton}>Yenile</Button>
        <ExportMenu variant="outlined" sx={bandButton} disabled={!sonuc || calisiyor}
          onDisaAktar={async (bicim) => {
            const kadiBitir = kadiYukleniyor('Raporu dışa aktarıyorum…');
            try { await exportReport(rapor.id, bicim); } catch (e) { setHata((e as Error).message); kadiHata(); } finally { kadiBitir(); }
          }} />
        {yetkili && (rapor.hamSql !== null
          ? <Button variant="contained" startIcon={<DesignServicesOutlinedIcon />} onClick={() => git(`/sql?rapor=${rapor.id}`)} sx={bandButtonWhite}>SQL Editöründe Aç</Button>
          : <Button variant="contained" startIcon={<DesignServicesOutlinedIcon />} onClick={() => git(`/tasarimci?rapor=${rapor.id}`)} sx={bandButtonWhite}>Tasarımcıda Aç</Button>)}
      </PageHeader>

      {hata && <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => setHata(null)}>{hata}</Alert>}

      {(rapor.model?.parametreler?.length ?? 0) > 0 && (
        <Paper variant="outlined" sx={{ borderRadius: boxRadius, p: 1.75, mb: 1.5, display: 'flex', alignItems: 'flex-end', gap: 2, flexWrap: 'wrap' }}>
          <ParametrePanel parametreler={rapor.model!.parametreler!} degerler={parametreDegerleri} onChange={setParametreDegerleri} />
          <Button variant="contained" size="small" startIcon={calisiyor ? <CircularProgress size={14} color="inherit" /> : <PlayArrowIcon />}
            onClick={() => void refresh()} disabled={calisiyor}>Uygula</Button>
        </Paper>
      )}

      <Paper variant="outlined" sx={{ borderRadius: boxRadius, overflow: 'hidden' }}>
        {!sonuc ? (
          <Box sx={{ p: 4, textAlign: 'center', color: 'text.secondary' }}>
            {calisiyor ? <CircularProgress size={22} /> : (
              <>
                <Typography sx={{ fontSize: 13.5, fontWeight: 600, mb: 0.5 }}>Bu rapor bu tarayıcıda henüz çalıştırılmadı.</Typography>
                <Typography sx={{ fontSize: 12.5, mb: 2 }} color="text.disabled">Sonuç bir dahaki açılış için saklanır; güncellemek istediğinde <b>Yenile</b> yeter.</Typography>
                <Button variant="contained" startIcon={<PlayArrowIcon />} onClick={() => void refresh()}>Çalıştır</Button>
              </>
            )}
          </Box>
        ) : tur === 'Tablo' ? (
          <ResultTable sonuc={sonuc} />
        ) : tur === 'SayiKarti' ? (
          sayisalIdx.length > 0
            ? <SayiKarti sonuc={sonuc} olcuIdx={olcuIdx.find((i) => sayisalIdx.includes(i)) ?? sayisalIdx[0]} />
            : <GrafikYok yetkili={yetkili} raporId={rapor.id} git={git} />
        ) : grafikSecenek ? (
          <Box sx={{ p: 1.5 }}><EChart secenek={grafikSecenek} /></Box>
        ) : (
          <GrafikYok yetkili={yetkili} raporId={rapor.id} git={git} />
        )}
      </Paper>
      {calisiyor && <RunningIndicator mesaj="Raporu çalıştırıyorum…" />}
    </Box>
  );
}

/** Sayısal ölçü olmayan raporda grafik yerine yol gösterir (boş/NaN grafik çizmeyiz). */
function GrafikYok({ yetkili, raporId, git }: { yetkili: boolean; raporId: string; git: (yol: string) => void }) {
  return (
    <Box sx={{ p: 4, textAlign: 'center' }}>
      <Typography sx={{ fontSize: 13.5, fontWeight: 600 }} color="text.secondary">
        Grafik için <b>sayısal bir ölçü</b> gerekli — bu raporun tüm alanları metin/kimlik.
      </Typography>
      <Typography sx={{ fontSize: 12.5, mt: 0.75 }} color="text.disabled">
        Tasarımcının 2. adımında bir alana <b>Topla / Say / Ortalama</b> verin ya da <b>Tablo</b> görünümünü kullanın.
      </Typography>
      {yetkili && (
        <Button size="small" variant="outlined" sx={{ mt: 2 }} startIcon={<DesignServicesOutlinedIcon />}
          onClick={() => git(`/tasarimci?rapor=${raporId}`)}>
          Tasarımcıda Düzenle
        </Button>
      )}
    </Box>
  );
}

function SayiKarti({ sonuc, olcuIdx }: { sonuc: QueryRunResult; olcuIdx: number }) {
  const deger = sonuc.satirlar[0]?.[olcuIdx];
  const sayi = typeof deger === 'number' ? deger.toLocaleString('tr-TR') : String(deger ?? '—');
  return (
    <Box sx={{ py: 6, textAlign: 'center' }}>
      <Typography sx={{ fontSize: 52, fontWeight: 800, letterSpacing: '-1px', color: 'secondary.main', lineHeight: 1 }}>{sayi}</Typography>
      <Typography sx={{ fontSize: 13, mt: 1 }} color="text.secondary">{sonuc.kolonlar[olcuIdx]?.ad ?? ''}</Typography>
      {sonuc.satirlar.length > 1 && (
        <Typography sx={{ fontSize: 11 }} color="text.disabled">ilk satırın değeri gösteriliyor ({sonuc.satirlar.length} satır)</Typography>
      )}
    </Box>
  );
}
