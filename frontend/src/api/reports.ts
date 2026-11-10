import { apiGet, apiSend } from './client';
import type { QueryModel, ParametreDegerleri } from './queries';

export type VisualizationType = 'Tablo' | 'Cubuk' | 'Cizgi' | 'Pasta' | 'SayiKarti';
export type ReportSharing = 'Ozel' | 'Ekip' | 'Roller';

export type ReportSummary = {
  id: string; ad: string; klasorId: string | null; klasorAd: string | null; paylasimRolleri?: string[] | null;
  gorsellestirme: VisualizationType; paylasim: ReportSharing;
  sahipAd: string | null; benimMi: boolean; favoriMi: boolean; guncellemeTarihi: string | null;
};
// İki tasarım türü: model raporu (haritaId+model dolu) ya da SQL raporu (hamSql+baglantiId dolu).
export type ReportDetail = {
  id: string; ad: string; klasorId: string | null; haritaId: string | null; paylasimRolleri?: string[] | null;
  model: QueryModel | null; hamSql: string | null; baglantiId: string | null;
  gorsellestirme: VisualizationType; paylasim: ReportSharing;
  sahipAd: string | null; benimMi: boolean;
};
export type FolderDto = { id: string; ad: string; raporSayisi: number; erisimRolleri?: string[] | null };
export type SaveReportInput = {
  id?: string | null; ad: string; klasorId: string | null;
  model: QueryModel | null; gorsellestirme: VisualizationType; paylasim: ReportSharing;
  hamSql?: string | null; baglantiId?: string | null;
  /** S27 — paylasim='Roller' iken zorunlu: görebilecek rol adları. */
  paylasimRolleri?: string[] | null;
};

export const getReports = () => apiGet<ReportSummary[]>('/api/raporlar');
/** Raporu kimliğiyle çalıştırır — TÜM roller (görünürlük backend'de); musteri paylaşılanı koşabilir.
 * Parametreli raporda çalıştırma-anı değerleri gövdede gider (yoksa {} → parametre varsayılanları). */
export const runReport = (id: string, parametreDegerleri?: ParametreDegerleri, signal?: AbortSignal) =>
  apiSend<import('./queries').QueryRunResult>(`/api/raporlar/${id}/calistir`, 'POST', parametreDegerleri ?? {}, undefined, signal);
export const getReport = (id: string) => apiGet<ReportDetail>(`/api/raporlar/${id}`);
export const addReport = (r: SaveReportInput) => apiSend<ReportSummary>('/api/raporlar', 'POST', r);
export const updateReport = (id: string, r: SaveReportInput) => apiSend<ReportSummary>(`/api/raporlar/${id}`, 'PUT', { ...r, id });
export const deleteReport = (id: string) => apiSend<void>(`/api/raporlar/${id}`, 'DELETE');
export const setReportFavorite = (id: string, favori: boolean) =>
  apiSend<void>(`/api/raporlar/${id}/favori?favori=${favori}`, 'POST');
export const getFolders = () => apiGet<FolderDto[]>('/api/raporlar/klasorler');
export const addFolder = (ad: string) => apiSend<FolderDto>('/api/raporlar/klasorler', 'POST', { ad });
export const deleteFolder = (id: string) => apiSend<void>(`/api/raporlar/klasorler/${id}`, 'DELETE');
/** S27 — klasöre rol erişimi ver/güncelle (boş liste kaldırır; yalnız admin). */
export const setFolderRoles = (id: string, roller: string[]) =>
  apiSend<FolderDto>(`/api/raporlar/klasorler/${id}/roller`, 'PUT', roller);
