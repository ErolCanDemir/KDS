import { apiGet, apiSend } from './client';

/** S42 — Gömme anahtarları. Anahtar dizesi YALNIZ üretim yanıtında döner; listede bir daha
 * gösterilmez (parola deseni — saklanan sır, sızabilecek ikinci kopyadır). */
export type EmbedToken = {
  id: string;
  ad: string;
  hedefTur: string;
  hedefAd: string | null;
  kosanKullanici: string | null;
  izinliAlanlar: string[];
  gecerlilikBitis: string;
  sonKullanim: string | null;
  /** gecerli | suresi-dolmus | iptal */
  durum: string;
};

export type EmbedTokenUret = {
  ad: string;
  raporId: string | null;
  panoId: string | null;
  kosanKullaniciId: string;
  /** RLS'in çalışması için zorunlu; admin verilemez. */
  roller: string[];
  izinliAlanlar: string[];
  gecerlilikGun: number;
};

export type EmbedTokenSonuc = { id: string; anahtar: string; gomKodu: string };

export const getGommeAnahtarlari = () => apiGet<EmbedToken[]>('/api/gomme');

export const uretGommeAnahtari = (istek: EmbedTokenUret) =>
  apiSend<EmbedTokenSonuc>('/api/gomme', 'POST', istek);

export const iptalGommeAnahtari = (id: string) =>
  apiSend<void>(`/api/gomme/${id}/iptal`, 'POST', {});
