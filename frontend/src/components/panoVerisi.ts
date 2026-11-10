import { useCallback, useEffect, useState } from 'react';
import type { Layout } from 'react-grid-layout';
import { getDashboard, updateDashboard, type DashboardDetail, type DashboardWidget } from '../api/dashboards';
import { getReports, getReport, type ReportSummary } from '../api/reports';
import type { ReportParameter, ParametreDegerleri } from '../api/queries';
import { ilkParametreDegerleri } from './ParametrePanel';
import { kadiYukleniyor, kadiHata } from '../kadi/kadiKanal';

/* S43 — pano yükleme/durum mantığı DashboardViewPage'den BURAYA alındı: aynı pano artık iki yerde
 * çiziliyor (görüntüleyici ve ana sayfa). Mantık sayfada kalsaydı ana sayfa ikinci bir kopyaya
 * muhtaç olurdu; parametre birleştirme ve yerleşim hesabı gibi ince kurallar zamanla ayrışırdı. */

export const SUTUN = 12;
export const SATIR_YUK = 46;
const VARSAYILAN_W = 4;
const VARSAYILAN_H = 6;
export const KENAR_BOSLUK: [number, number] = [14, 14];

/** Kayıtlı yerleşim + yerleşimi olmayan raporlar için varsayılan akış konumu → grid düzeni. */
export function yerlesimHesapla(raporIds: string[], yerlesim: DashboardWidget[]): Layout {
  const harita = new Map(yerlesim.map((w) => [w.raporId, w]));
  let x = 0;
  let y = 0;
  return raporIds.map((rid) => {
    const k = harita.get(rid);
    if (k) return { i: rid, x: k.x, y: k.y, w: k.w, h: k.h };
    const item = { i: rid, x, y, w: VARSAYILAN_W, h: VARSAYILAN_H };
    x += VARSAYILAN_W;
    if (x + VARSAYILAN_W > SUTUN) { x = 0; y += VARSAYILAN_H; }
    return item;
  });
}

export type PanoVerisi = ReturnType<typeof usePanoVerisi>;

/** Panonun tüm görüntüleme durumu: içerik, birleşik filtre şeridi, ızgara yerleşimi ve yerleşim
 * kaydetme. Sayfalar bunu tüketir; çizim <PanoGovdesi/> içindedir. */
export function usePanoVerisi(panoId: string | undefined) {
  const [pano, setPano] = useState<DashboardDetail | null>(null);
  const [raporlar, setRaporlar] = useState<ReportSummary[]>([]);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [hata, setHata] = useState<string | null>(null);
  const [tazeleme, setTazeleme] = useState(0);
  const [layout, setLayout] = useState<Layout>([]);
  const [duzenle, setDuzenle] = useState(false);
  const [kaydediyor, setKaydediyor] = useState(false);
  const [panoParametreler, setPanoParametreler] = useState<ReportParameter[]>([]);
  const [filtreDegerleri, setFiltreDegerleri] = useState<ParametreDegerleri>({});
  const [uygulanan, setUygulanan] = useState<ParametreDegerleri>({});

  const yukle = useCallback(() => {
    if (!panoId) { setYukleniyor(false); return; }
    setYukleniyor(true);
    Promise.all([getDashboard(panoId), getReports()])
      .then(([p, r]) => { setPano(p); setRaporlar(r); setYukleniyor(false); })
      .catch((e) => { setHata(e instanceof Error ? e.message : 'Pano yüklenemedi.'); kadiHata(); setYukleniyor(false); });
  }, [panoId]);
  useEffect(yukle, [yukle]);

  // Panonun (var olan) raporlarının parametrelerini toplayıp birleşik filtre şeridini kurar. Rapor
  // detayları salt-okunur çekilir; parametresiz panoda hiç şerit çıkmaz.
  useEffect(() => {
    if (!pano) return;
    const varOlan = new Set(raporlar.map((r) => r.id));
    const idler = pano.raporlar.filter((rid) => varOlan.has(rid));
    if (idler.length === 0) { setPanoParametreler([]); setFiltreDegerleri({}); setUygulanan({}); return; }
    let iptal = false;
    void Promise.all(idler.map((rid) => getReport(rid).catch(() => null))).then((detaylar) => {
      if (iptal) return;
      const birlesik = new Map<string, ReportParameter>();
      for (const d of detaylar)
        for (const p of d?.model?.parametreler ?? [])
          if (!birlesik.has(p.anahtar)) birlesik.set(p.anahtar, p);
      const liste = [...birlesik.values()];
      const ilk = ilkParametreDegerleri(liste);
      setPanoParametreler(liste);
      setFiltreDegerleri(ilk);
      setUygulanan(ilk);
    });
    return () => { iptal = true; };
  }, [pano, raporlar]);

  // Yükleme/rapor değişiminde yerleşimi (kayıtlı + varsayılan) kur — düzenlerken dıştan sıfırlama yok.
  useEffect(() => {
    if (!pano || duzenle) return;
    const varOlan = new Set(raporlar.map((r) => r.id));
    setLayout(yerlesimHesapla(pano.raporlar.filter((rid) => varOlan.has(rid)), pano.yerlesim));
  }, [pano, raporlar, duzenle]);

  async function yerlesimKaydet() {
    if (!pano) return;
    setKaydediyor(true);
    setHata(null);
    const kadiBitir = kadiYukleniyor('Yerleşimi kaydediyorum…');
    try {
      const yerlesim: DashboardWidget[] = layout.map((l) => ({ raporId: l.i, x: l.x, y: l.y, w: l.w, h: l.h }));
      await updateDashboard(pano.id, { ad: pano.ad, paylasim: pano.paylasim, raporlar: pano.raporlar, yerlesim });
      setPano((p) => (p ? { ...p, yerlesim } : p)); // yerel state → kaydedilen yerleşim korunur
      setDuzenle(false);
    } catch (e) {
      setHata(e instanceof Error ? e.message : 'Yerleşim kaydedilemedi.'); kadiHata();
    } finally {
      kadiBitir();
      setKaydediyor(false);
    }
  }

  const haritali = new Map(raporlar.map((r) => [r.id, r]));
  const panoRaporlari = (pano?.raporlar ?? [])
    .map((rid) => haritali.get(rid))
    .filter((r): r is ReportSummary => !!r);

  return {
    pano, panoRaporlari, yukleniyor, hata, setHata,
    yukle, tazeleme, yenile: () => setTazeleme((t) => t + 1),
    layout, setLayout, duzenle, setDuzenle, kaydediyor, yerlesimKaydet,
    panoParametreler, filtreDegerleri, setFiltreDegerleri, uygulanan,
    uygula: () => { setUygulanan(filtreDegerleri); setTazeleme((t) => t + 1); },
    /** Panoya eklenmiş ama artık var olmayan (silinmiş) rapor sayısı. */
    eksik: (pano?.raporlar.length ?? 0) - panoRaporlari.length,
  };
}
