import { apiGet, apiSend } from './client';

/** Seçilen kaynak tablo. kolonlar null → tüm kolonlar (bulk); dolu → yalnız seçilenler (curated).
 * artimliKolon (S44): doluysa tablo ARTIMLI yüklenir — her koşuda yalnız bu kolonu son değerden
 * büyük/eşit satırlar çekilip PK üzerinden birleştirilir; null → tam yükleme. */
export type IngestTable = { sema: string; tablo: string; kolonlar: string[] | null; artimliKolon?: string | null };

/** Liste kartı: özet + son senkron durumu. */
export type IngestJobSummary = {
  id: string;
  ad: string;
  baglantiId: string;
  baglantiAd: string | null;
  tabloSayisi: number;
  zamanlama: string | null;
  aktif: boolean;
  sonSenkronTarihi: string | null;
  sonSenkronSaniye: number | null;
  sonSatirSayisi: number | null;
  sonDurum: string | null; // "Basarili" | "Hata"
  sonHata: string | null;
  /** S44 — tablolardan kaçında artımlı kolon seçili. */
  artimliTabloSayisi: number;
};

/** Editör için tam içerik. */
export type IngestJobDetail = {
  id: string;
  ad: string;
  baglantiId: string;
  tablolar: IngestTable[];
  zamanlama: string | null;
  aktif: boolean;
};

export type SaveIngestJobInput = {
  ad: string;
  baglantiId: string;
  tablolar: IngestTable[];
  zamanlama: string | null;
  aktif: boolean;
};

export type IngestRunResult = {
  tabloSayisi: number;
  toplamSatir: number;
  saniyeSure: number;
  /** S44 — kaç tablo artımlı (delta) yolla yüklendi; kalanı tam yüklemedir. */
  artimliTabloSayisi: number;
  /** S45 — kaynak ajanlı: iş sunucuda koşmadı, müşteri ağındaki ajan bir sonraki yoklamada koşacak. */
  ajanaDevredildi: boolean;
};

export const getIngestJobs = () => apiGet<IngestJobSummary[]>('/api/ice-alim');
export const getIngestJob = (id: string) => apiGet<IngestJobDetail>(`/api/ice-alim/${id}`);
export const addIngestJob = (g: SaveIngestJobInput) =>
  apiSend<IngestJobSummary>('/api/ice-alim', 'POST', { id: null, ...g });
export const updateIngestJob = (id: string, g: SaveIngestJobInput) =>
  apiSend<IngestJobSummary>(`/api/ice-alim/${id}`, 'PUT', { id, ...g });
export const deleteIngestJob = (id: string) => apiSend<void>(`/api/ice-alim/${id}`, 'DELETE');
/** S44 — tamYukleme=true: artımlı su seviyeleri sıfırlanır, her tablo baştan yüklenir
 * (artımlının taşımadığı SİLİNMİŞ satırlar böyle temizlenir). */
export const runIngestJob = (id: string, tamYukleme = false) =>
  apiSend<IngestRunResult>(`/api/ice-alim/${id}/calistir${tamYukleme ? '?tamYukleme=true' : ''}`, 'POST');
