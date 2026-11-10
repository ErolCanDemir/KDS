import { apiGet, apiSend } from './client';
import type { ReportSharing } from './reports';

/** Pano galerisi kartı (API'den dönen özet). */
export type DashboardSummary = {
  id: string;
  ad: string;
  paylasim: ReportSharing;
  paylasimRolleri?: string[] | null;
  raporSayisi: number;
  sahipAd: string | null;
  benimMi: boolean;
  guncellemeTarihi: string | null;
  /** S43 — kullanıcının ana sayfasında bu pano mu açılıyor. */
  anaSayfamMi: boolean;
};

/** Widget yerleşimi (react-grid-layout birimi): rapor başına konum/boyut. */
export type DashboardWidget = { raporId: string; x: number; y: number; w: number; h: number };

/** Pano tam içeriği (görüntüleyici/editör). raporlar = sıralı rapor id listesi; yerlesim = widget
 * konumları (boşsa görüntüleyici varsayılan ızgara uygular). */
export type DashboardDetail = {
  id: string;
  ad: string;
  paylasim: ReportSharing;
  paylasimRolleri?: string[] | null;
  raporlar: string[];
  yerlesim: DashboardWidget[];
  sahipAd: string | null;
  benimMi: boolean;
  /** S43 — kullanıcının ana sayfasında bu pano mu açılıyor. */
  anaSayfamMi: boolean;
};

/** Kaydet form değerleri (yeni/güncelle). yerlesim verilmezse mevcut yerleşim korunur. */
export type SaveDashboardInput = {
  ad: string;
  paylasim: ReportSharing;
  raporlar: string[];
  yerlesim?: DashboardWidget[];
  /** S27 — paylasim='Roller' iken zorunlu: görebilecek rol adları. */
  paylasimRolleri?: string[] | null;
};

export const getDashboards = () => apiGet<DashboardSummary[]>('/api/panolar');
export const getDashboard = (id: string) => apiGet<DashboardDetail>(`/api/panolar/${id}`);
export const addDashboard = (d: SaveDashboardInput) => apiSend<DashboardSummary>('/api/panolar', 'POST', d);
export const updateDashboard = (id: string, d: SaveDashboardInput) =>
  apiSend<DashboardSummary>(`/api/panolar/${id}`, 'PUT', { ...d, id });
export const deleteDashboard = (id: string) => apiSend<void>(`/api/panolar/${id}`, 'DELETE');

/** S43 — ana sayfada açılacak pano. panoId null → seçim yok (ya da seçilen pano silinmiş/görünmez
 * oldu); ana sayfa o durumda karşılama görünümünü çizer. Çıplak değer yerine sarmalayıcı: null'da
 * sunucu gövdesiz 204 üretiyordu ve JSON ayrıştırıcı boş gövdede patlıyordu. */
export const getHomeDashboard = () => apiGet<{ panoId: string | null }>('/api/panolar/ana-sayfam');
/** panoId null → seçim kaldırılır. */
export const setHomeDashboard = (panoId: string | null) =>
  apiSend<void>(`/api/panolar/ana-sayfam${panoId ? `?panoId=${panoId}` : ''}`, 'PUT');
