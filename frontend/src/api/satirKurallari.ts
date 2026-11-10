import { apiGet, apiSend } from './client';
import type { ComparisonOp } from './queries';

/** Satır-düzeyi güvenlik (RLS) kuralı: bir harita TABLOSUNU kullanıcı/rol bazında daraltır.
 * Kural sorgu ÜRETİMİNDE uygulanır — kullanıcı kaldıramaz, filtresiyle atlatamaz. */
export type KuralDegerKaynagi = 'SabitDeger' | 'TokenIddiasi' | 'KullaniciAdi' | 'EPosta';

export type SatirKurali = {
  id: string;
  veriHaritasiId: string; veriHaritasiAdi: string;
  haritaTabloId: string; tabloAdi: string;
  ad: string; kolon: string; op: ComparisonOp;
  degerKaynagi: KuralDegerKaynagi; deger: string | null;
  roller: string | null; aktif: boolean;
};

export type SatirKuraliGirdi = {
  id?: string | null;
  veriHaritasiId: string; haritaTabloId: string;
  ad: string; kolon: string; op: ComparisonOp;
  degerKaynagi: KuralDegerKaynagi; deger: string | null;
  roller: string | null; aktif: boolean;
};

export const getSatirKurallari = (haritaId?: string) =>
  apiGet<SatirKurali[]>(`/api/satir-kurallari${haritaId ? `?haritaId=${haritaId}` : ''}`);
export const saveSatirKurali = (k: SatirKuraliGirdi) =>
  apiSend<SatirKurali>('/api/satir-kurallari', 'POST', k);
export const deleteSatirKurali = (id: string) =>
  apiSend<void>(`/api/satir-kurallari/${id}`, 'DELETE');
