import { apiGet, apiSend } from './client';

/** S35 — Kuruluş Ayarları. Kiracının kendi kimliği ve markası.
 *
 * Limitler burada TANIMLANMAZ: Sistem Ayarları'nda tanımlıdır ve zaten kiracı bazında
 * ezilebiliyor. Bu uç onları yalnız yürürlükteki değerleriyle ÖZETLER (kaynak rozetiyle),
 * böylece ekran "benim kuruluşum için geçerli limit nedir" sorusunu ikinci bir düzenleme
 * yeri açmadan cevaplar. */
export type LimitOzeti = {
  anahtar: string;
  etiket: string;
  deger: string;
  /** kiraci | kurulum | varsayilan */
  kaynak: string;
};

export type Kurulus = {
  /** Keycloak token'ındaki tenant_id ile birebir aynı olmalı. */
  kiraciId: string;
  ad: string;
  durum: string;
  plan: string | null;
  markaRengi: string | null;
  logoDataUri: string | null;
  olusturmaTarihi: string;
  limitler: LimitOzeti[];
};

export type KurulusGuncelle = {
  ad: string;
  plan: string | null;
  markaRengi: string | null;
  logoDataUri: string | null;
};

/** Uygulama kabuğunun çizdiği marka — TÜM roller çağırabilir (kuruluş ayarları yalnız Admin'e
 * açık, ama logo herkesin ekranında görünmeli). */
export type Marka = { ad: string; markaRengi: string | null; logoDataUri: string | null };

export const getMarka = () => apiGet<Marka>('/api/kurulus/marka');

export const getKurulus = () => apiGet<Kurulus>('/api/kurulus');

export const saveKurulus = (istek: KurulusGuncelle) =>
  apiSend<Kurulus>('/api/kurulus', 'PUT', istek);
