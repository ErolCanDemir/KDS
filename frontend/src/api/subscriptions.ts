import { apiGet, apiSend } from './client';

/** Ek biçimi (Kds.Export.ExportFormat adıyla birebir). */
export type AbonelikBicim = 'Pdf' | 'Xlsx' | 'Csv';

/** Liste kartı: özet + son gönderim durumu. */
export type SubscriptionSummary = {
  id: string;
  ad: string;
  raporId: string;
  raporAd: string | null;
  bicim: string;
  aliciSayisi: number;
  zamanlama: string | null;
  aktif: boolean;
  sonGonderimTarihi: string | null;
  sonAliciSayisi: number | null;
  sonSatirSayisi: number | null;
  sonDurum: string | null; // "Basarili" | "Hata"
  sonHata: string | null;
};

/** Editör için tam içerik. */
export type SubscriptionDetail = {
  id: string;
  ad: string;
  raporId: string;
  bicim: string;
  alicilar: string[];
  zamanlama: string | null;
  aktif: boolean;
  parametreDegerleri?: Record<string, unknown> | null; // M24: parametreli raporun kayıtlı değerleri
};

export type SaveSubscriptionInput = {
  ad: string;
  raporId: string;
  bicim: string;
  alicilar: string[];
  zamanlama: string | null;
  aktif: boolean;
  parametreDegerleri?: Record<string, unknown> | null; // M24 ("$bugun" ucu gönderim gününe çözülür)
};

/** `epostaGonderildi` false = ek üretildi ama SMTP ayarlı olmadığı için yalnız günlüğe yazıldı. */
export type SubscriptionRunResult = {
  aliciSayisi: number; satirSayisi: number; saniyeSure: number; dosyaAdi: string; epostaGonderildi: boolean;
};

export const getSubscriptions = () => apiGet<SubscriptionSummary[]>('/api/abonelikler');
export const getSubscription = (id: string) => apiGet<SubscriptionDetail>(`/api/abonelikler/${id}`);
export const addSubscription = (g: SaveSubscriptionInput) =>
  apiSend<SubscriptionSummary>('/api/abonelikler', 'POST', { id: null, ...g });
export const updateSubscription = (id: string, g: SaveSubscriptionInput) =>
  apiSend<SubscriptionSummary>(`/api/abonelikler/${id}`, 'PUT', { id, ...g });
export const deleteSubscription = (id: string) => apiSend<void>(`/api/abonelikler/${id}`, 'DELETE');
/** Aboneliği ŞİMDİ gönderir (elle tetik). */
export const runSubscription = (id: string) => apiSend<SubscriptionRunResult>(`/api/abonelikler/${id}/gonder`, 'POST');
