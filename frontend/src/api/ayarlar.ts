import { apiGet, apiSend } from './client';

/** S16/A3 — Sistem Ayarları. Değer + nereden geldiği (kiracı / kurulum / varsayılan). */
export type Ayar = {
  anahtar: string; grup: string; etiket: string; tip: string; aciklama: string;
  varsayilan: string; deger: string; kaynak: string;
  /** Sır tipinde: değer girili mi (değerin kendisi API'den ASLA dönmez). */
  dolu: boolean;
};

export const getAyarlar = () => apiGet<Ayar[]>('/api/ayarlar');

export const saveAyarlar = (degerler: Record<string, string>) =>
  apiSend<{ degisen: number }>('/api/ayarlar', 'PUT', degerler);

/** Önyüzün davranış eşikleri — derlemeye gömülmesin diye sunucudan gelir. */
export type OnyuzAyarlari = { yavasEsikMs: number };

let onbellek: Promise<OnyuzAyarlari> | null = null;

/** Oturum boyunca bir kez okunur; hata durumunda ürün varsayılanına düşer (ekran çalışmaya devam eder). */
export function onyuzAyarlariAl(): Promise<OnyuzAyarlari> {
  onbellek ??= apiGet<OnyuzAyarlari>('/api/ayarlar/onyuz').catch(() => ({ yavasEsikMs: 10000 }));
  return onbellek;
}

/** Ayar kaydedildiğinde önbelleği düşür (yeni eşik hemen geçerli olsun). */
export function onyuzAyarlariniTazele() { onbellek = null; }
