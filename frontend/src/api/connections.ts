import { apiGet, apiSend } from './client';

export type DataMode = 'Canli' | 'IceAlim';

/** API'den dönen bağlantı özeti (parola dönmez; durum son testten türetilir). */
export type Connection = {
  id: string;
  ad: string;
  sunucu: string;
  veritabani: string;
  kullaniciAdi: string | null;
  mod: DataMode;
  sonTestBasarili: boolean | null;
  sonTestMs: number | null;
  sonTestTarihi: string | null;
  /** M22b: İçe Alım modunda depo hedefi (Canlı bağlantı id'si; null = KDS deposu otomatik). */
  depoBaglantiId: string | null;
  /** S45: dolu = kaynağa müşteri ağındaki AJAN bağlanır (sunucu doğrudan bağlanmaz). */
  ajanId: string | null;
};

/** Ekle/güncelle form değerleri. */
export type ConnectionInput = {
  ad: string;
  sunucu: string;
  veritabani: string;
  kullaniciAdi: string;
  sifre: string;
  mod: DataMode;
  /** M22b: yalnız İçe Alım modunda gönderilir ('' = varsayılan KDS deposu). */
  depoBaglantiId?: string | null;
  /** S45: yalnız İçe Alım modunda; '' = ajansız (sunucu doğrudan bağlanır). */
  ajanId?: string | null;
};

export type TestResult = { basarili: boolean; ms: number | null; mesaj: string | null };

export const getConnections = () => apiGet<Connection[]>('/api/baglantilar');
export const addConnection = (g: ConnectionInput) => apiSend<Connection>('/api/baglantilar', 'POST', g);
export const updateConnection = (id: string, g: ConnectionInput) =>
  apiSend<Connection>(`/api/baglantilar/${id}`, 'PUT', { id, ...g });
export const deleteConnection = (id: string) => apiSend<void>(`/api/baglantilar/${id}`, 'DELETE');
export const testConnectionForm = (g: Pick<ConnectionInput, 'sunucu' | 'veritabani' | 'kullaniciAdi' | 'sifre'>) =>
  apiSend<TestResult>('/api/baglantilar/test', 'POST', g);
export const testConnectionById = (id: string) => apiSend<TestResult>(`/api/baglantilar/${id}/test`, 'POST');
