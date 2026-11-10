import { apiGet, apiGetText, apiSend } from './client';

/** Gösterim biçimi (backend MetrikBicimi ile birebir). */
export type MetrikBicimi = 'Sayi' | 'Para' | 'Yuzde';

export type MetricSummary = {
  id: string;
  ad: string;
  veriHaritasiId: string;
  haritaAd: string | null;
  ifade: string;
  bicim: MetrikBicimi;
  aciklama: string | null;
  sahipAd: string | null;
  aktif: boolean;
  /** Kaç kayıtlı rapor kullanıyor — silme onayı bunun üstünden konuşur. */
  kullananRaporSayisi: number;
};

export type SaveMetricInput = {
  ad: string;
  veriHaritasiId: string;
  ifade: string;
  bicim: MetrikBicimi;
  aciklama: string | null;
  aktif: boolean;
};

export type MetricMeasureResult = { deger: number | null; bicim: MetrikBicimi; sureMs: number };

export const getMetrics = () => apiGet<MetricSummary[]>('/api/metrikler');
export const addMetric = (g: SaveMetricInput) => apiSend<MetricSummary>('/api/metrikler', 'POST', { id: null, ...g });
export const updateMetric = (id: string, g: SaveMetricInput) =>
  apiSend<MetricSummary>(`/api/metrikler/${id}`, 'PUT', { id, ...g });
export const deleteMetric = (id: string) => apiSend<void>(`/api/metrikler/${id}`, 'DELETE');
/** Silme onayı için: metriği kullanan raporların adları. */
export const getMetricUsage = (id: string) => apiGet<string[]>(`/api/metrikler/${id}/kullanim`);
/** Güncel değeri ölçer — normal koşum hattından geçer (RLS/sınır/denetim uygulanır). */
export const measureMetric = (id: string) => apiSend<MetricMeasureResult>(`/api/metrikler/${id}/olc`, 'POST');

// ——— S30 · Sürüm geçmişi ———

/** Bir sürüm = tanımın ESKİ bir hâli; güncel hâl metriğin kendisidir. */
export type MetricVersionSummary = {
  surumNo: number;
  ad: string;
  ifade: string;
  bicim: MetrikBicimi;
  aciklama: string | null;
  /** Bu hâli tarihe gömen güncellemeyi yapan kişi. */
  degistirenAd: string | null;
  tarih: string;
};

export const getMetricVersions = (id: string) =>
  apiGet<MetricVersionSummary[]>(`/api/metrikler/${id}/surumler`);
/** Sürümü geri yükler — formül yeniden doğrulanır, dönüş de sürüm üretir (geçmiş kaybolmaz). */
export const restoreMetricVersion = (id: string, surumNo: number) =>
  apiSend<MetricSummary>(`/api/metrikler/${id}/surumler/${surumNo}/don`, 'POST');

// ——— S30 · Dışa / içe aktarma ———

/** Taşınabilir tanım: harita ADI ile — GUID kiracıya özel, ad hedef ortamda aynı kurulabilir. */
export type MetricExportItem = {
  haritaAd: string;
  ad: string;
  ifade: string;
  bicim: MetrikBicimi;
  aciklama: string | null;
};

export type MetrikIceAlmaSonucu = 'Eklendi' | 'Guncellendi' | 'Atlandi' | 'Hata';
export type MetricImportItemResult = { ad: string; sonuc: MetrikIceAlmaSonucu; mesaj: string | null };

export const exportMetrics = (haritaId?: string) =>
  apiGet<MetricExportItem[]>(`/api/metrikler/disa${haritaId ? `?haritaId=${haritaId}` : ''}`);
export const importMetrics = (ogeler: MetricExportItem[], cakismadaGuncelle: boolean) =>
  apiSend<MetricImportItemResult[]>('/api/metrikler/ice', 'POST', { ogeler, cakismadaGuncelle });

/** S47 — formül düzenleyicinin fonksiyon ipucu (kaynak: backend'deki ifade bekçisinin yanı). */
export type MetricFunctionInfo = { ad: string; ornek: string; aciklama: string };
export type MetricFunctionGroup = { grup: string; fonksiyonlar: MetricFunctionInfo[] };
export const getMetricFunctions = () => apiGet<MetricFunctionGroup[]>('/api/metrikler/fonksiyonlar');

/** S46 — OSI (Apache Ossie): semantik katman satıcıdan bağımsız YAML biçiminde. */
export const exportMetricsOsi = (haritaId?: string) =>
  apiGetText(`/api/metrikler/disa-osi${haritaId ? `?haritaId=${haritaId}` : ''}`);
export const importMetricsOsi = (yaml: string, cakismadaGuncelle: boolean) =>
  apiSend<MetricImportItemResult[]>('/api/metrikler/ice-osi', 'POST', { yaml, cakismadaGuncelle });

/** Değeri biçime göre yazar: 4.812.350 ₺ · 1.234 · %12,5 */
export function metrikDegerYaz(deger: number | null, bicim: MetrikBicimi): string {
  if (deger === null) return '—';
  if (bicim === 'Para') return `${deger.toLocaleString('tr-TR', { maximumFractionDigits: 2 })} ₺`;
  if (bicim === 'Yuzde') return `%${deger.toLocaleString('tr-TR', { maximumFractionDigits: 2 })}`;
  return deger.toLocaleString('tr-TR', { maximumFractionDigits: 4 });
}
