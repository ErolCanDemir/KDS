import type { ReactNode } from 'react';
import TableRowsIcon from '@mui/icons-material/TableRows';
import BarChartIcon from '@mui/icons-material/BarChart';
import ShowChartIcon from '@mui/icons-material/ShowChart';
import PieChartOutlinedIcon from '@mui/icons-material/PieChartOutlined';
import Filter1Icon from '@mui/icons-material/Filter1';
import type { EChartsCoreOption } from 'echarts/core';
import type { VisualizationType } from '../api/reports';
import type { QueryModel, QueryRunResult } from '../api/queries';

/** Görselleştirme türlerinin ekran kimliği (ad + renk + ikon) — liste, görüntüleyici, pano ve
 * tasarımcı save diyaloğu paylaşır. */
export const vizDisplay: Record<VisualizationType, { ad: string; renk: string; arka: string; ikon: ReactNode }> = {
  Tablo: { ad: 'Tablo', renk: '#5b6875', arka: 'var(--kds-dolgu)', ikon: <TableRowsIcon sx={{ fontSize: 15 }} /> },
  Cubuk: { ad: 'Çubuk', renk: '#0e6f7a', arka: 'var(--kds-tealdolgu)', ikon: <BarChartIcon sx={{ fontSize: 15 }} /> },
  Cizgi: { ad: 'Çizgi', renk: '#6d4bd8', arka: '#efeafd', ikon: <ShowChartIcon sx={{ fontSize: 15 }} /> },
  Pasta: { ad: 'Pasta', renk: '#c8862a', arka: 'var(--kds-amberdolgu)', ikon: <PieChartOutlinedIcon sx={{ fontSize: 15 }} /> },
  SayiKarti: { ad: 'Sayı Kartı', renk: '#e0576b', arka: '#fdeaee', ikon: <Filter1Icon sx={{ fontSize: 15 }} /> },
};

/** Ürün paleti — ECharts serilerinde sırayla kullanılır (theme kimliğiyle uyumlu).
 * DİKKAT: ECharts HAM renk ister — tema yolu ('success.main' gibi) burada geçersizdir. */
export const chartPalette = ['#0e6f7a', '#16aebd', '#f5a524', '#6d4bd8', '#e0576b', '#1a9d6b'];

/** S26 — grafik açılış animasyonu (login botlarıyla aynı dil): kısa, tek seferlik, saygılı. */
const azHareket = typeof window !== 'undefined'
  && window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
const chartAnimasyon = azHareket
  ? ({ animation: false } as const)
  : ({
      animation: true,
      animationDuration: 700,
      animationEasing: 'cubicOut',
      animationDelay: (i: number) => i * 40,   // öğeler sırayla gelir (stagger)
    } as const);

/** GERÇEKTEN sayısal kolonlar (değerlere bakarak): GUID/metin kolonlar Number()'da NaN olup
 * grafiği sessizce boş bırakıyordu — ölçü adayı yalnız bunlardan seçilir. */
export function numericColumnIndices(sonuc: QueryRunResult): number[] {
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
}

/** Bir sorgu sonucu + görselleştirme türünden ECharts seçeneği kurar (Çubuk/Çizgi/Pasta).
 * Model verilirse boyut/ölçü SELECT sırasından, verilmezse değerlerden çıkarılır. Grafik uygun
 * değilse (tablo/sayı kartı ya da sayısal ölçü yok) null döner. Görüntüleyici + pano paylaşır. */
export function reportChartOption(
  sonuc: QueryRunResult,
  tur: VisualizationType,
  model: QueryModel | null = null,
): EChartsCoreOption | null {
  if (sonuc.kolonlar.length === 0) return null;
  if (tur !== 'Cubuk' && tur !== 'Cizgi' && tur !== 'Pasta') return null;

  const sayisalIdx = numericColumnIndices(sonuc);
  const boyutIdx: number[] = [], olcuIdx: number[] = [];
  model?.alanlar.forEach((a, i) => (a.toplama === 'Yok' ? boyutIdx : olcuIdx).push(i));

  const bIdx = boyutIdx.find((i) => !sayisalIdx.includes(i)) ?? boyutIdx[0]
    ?? sonuc.kolonlar.findIndex((_, i) => !sayisalIdx.includes(i));
  const oIdxler = (olcuIdx.some((i) => sayisalIdx.includes(i))
    ? olcuIdx.filter((i) => sayisalIdx.includes(i))
    : sayisalIdx.filter((i) => i !== bIdx));
  if (oIdxler.length === 0) return null;

  const etiketler = sonuc.satirlar.map((r, i) => (bIdx >= 0 ? String(r[bIdx] ?? '—') : `#${i + 1}`));

  if (tur === 'Pasta') {
    const oIdx = oIdxler[0] ?? 0;
    return {
      ...chartAnimasyon,
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
    ...chartAnimasyon,
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
}

/** Sayı kartı için: ilk sayısal kolonun toplamı + o kolonun adı (yoksa satır sayısı). */
export function scoreCardValue(sonuc: QueryRunResult): { deger: string; etiket: string } {
  const sayisalIdx = numericColumnIndices(sonuc);
  if (sayisalIdx.length === 0 || sonuc.satirlar.length === 0)
    return { deger: sonuc.satirlar.length.toLocaleString('tr-TR'), etiket: 'satır' };
  const idx = sayisalIdx[0];
  const toplam = sonuc.satirlar.reduce((t, r) => t + Number(r[idx] ?? 0), 0);
  return { deger: toplam.toLocaleString('tr-TR'), etiket: sonuc.kolonlar[idx]?.ad ?? '' };
}
