import { apiGet, apiSend } from './client';

/** S45 — KDS Ajanı: müşteri ağına kurulan, dışarı doğru bağlanan hafif süreç. Kaynak bağlantı
 * dizeleri ajanın makinesindeki ajan.json'da durur; KDS'ye gönderilmez. */
export type Ajan = {
  id: string;
  ad: string;
  sonGorulme: string | null;
  olusturmaTarihi: string;
  durum: 'gecerli' | 'iptal';
  bagliBaglantiSayisi: number;
};

/** Anahtar YALNIZ bu yanıtta döner; saklanmaz (parola deseni). */
export type AjanSonuc = { id: string; anahtar: string; ornekYapilandirma: string };

export const getAjanlar = () => apiGet<Ajan[]>('/api/ajanlar');
export const uretAjan = (ad: string) => apiSend<AjanSonuc>('/api/ajanlar', 'POST', { ad });
export const iptalAjan = (id: string) => apiSend<void>(`/api/ajanlar/${id}/iptal`, 'POST');

/** Yoklama aralığı 60 sn — son 3 dk içinde görüldüyse "çevrimiçi" sayarız. */
export const cevrimici = (a: Ajan) =>
  a.durum === 'gecerli' && !!a.sonGorulme && Date.now() - new Date(a.sonGorulme).getTime() < 3 * 60_000;
