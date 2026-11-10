import { apiGet, apiSend } from './client';

/** Satırları tek değere indirgeme biçimi (backend OzetTuru ile birebir). */
export type OzetTuru = 'SatirSayisi' | 'Toplam' | 'Ortalama' | 'EnBuyuk' | 'EnKucuk' | 'IlkSatir';

export type UyariOp = 'Buyuk' | 'BuyukEsit' | 'Kucuk' | 'KucukEsit' | 'Esit' | 'EsitDegil';

export type UyariDurumu = 'HicKosmadi' | 'Sakin' | 'Tetiklendi' | 'Hata';

export type AlertSummary = {
  id: string;
  ad: string;
  /** S30: uyarı ya bir RAPORU ya bir METRİĞİ izler — tam biri dolu. */
  raporId: string | null;
  raporAd: string | null;
  metrikId: string | null;
  metrikAd: string | null;
  kolon: string | null;
  ozet: OzetTuru;
  op: UyariOp;
  esik: number;
  aliciSayisi: number;
  zamanlama: string | null;
  aktif: boolean;
  surekliBildir: boolean;
  durum: UyariDurumu;
  sonDeger: number | null;
  sonKontrolTarihi: string | null;
  sonBildirimTarihi: string | null;
  sonHata: string | null;
  /** Müşteri yalnız kendi uyarılarını görür; admin/geliştirici için başkasınınkiler de listelenir. */
  benimMi: boolean;
};

export type AlertDetail = {
  id: string;
  ad: string;
  raporId: string | null;
  metrikId: string | null;
  kolon: string | null;
  ozet: OzetTuru;
  op: UyariOp;
  esik: number;
  alicilar: string[];
  zamanlama: string | null;
  aktif: boolean;
  surekliBildir: boolean;
  parametreDegerleri?: Record<string, unknown> | null;
};

export type SaveAlertInput = Omit<AlertDetail, 'id'>;

/**
 * `bildirimGonderildi` false = ya durum değişmedi (susulması gerekiyordu) ya da SMTP ayarlı değil.
 * Ekran bunu VARSAYMAZ, buradan okur — sabit metin, ayar değişince yalan söylemeye başlıyor.
 */
export type AlertRunResult = {
  deger: number | null;
  tetiklendi: boolean;
  durumDegisti: boolean;
  bildirimGonderildi: boolean;
  aliciSayisi: number;
  satirSayisi: number;
  saniyeSure: number;
};

export const getAlerts = () => apiGet<AlertSummary[]>('/api/uyarilar');
export const getAlert = (id: string) => apiGet<AlertDetail>(`/api/uyarilar/${id}`);
export const addAlert = (g: SaveAlertInput) => apiSend<AlertSummary>('/api/uyarilar', 'POST', { id: null, ...g });
export const updateAlert = (id: string, g: SaveAlertInput) =>
  apiSend<AlertSummary>(`/api/uyarilar/${id}`, 'PUT', { id, ...g });
export const deleteAlert = (id: string) => apiSend<void>(`/api/uyarilar/${id}`, 'DELETE');
/** "Şimdi Dene": ölçer, durumu yazar ama e-posta ATMAZ. */
export const tryAlert = (id: string) => apiSend<AlertRunResult>(`/api/uyarilar/${id}/dene`, 'POST');
