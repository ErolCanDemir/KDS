import { apiGet, apiSend } from './client';

/** S27 Roles & Yetkiler — roller Keycloak realm rolleridir; burada açılan rol girişte token'a
 * yazılır. Üye değişikliği kullanıcının SONRAKİ girişinde geçerli olur (ekran bunu söyler). */
export type RolOzeti = {
  ad: string;
  aciklama: string | null;
  /** İlk 500 üye sayılır (rozet için yeterli). */
  uyeSayisi: number;
  /** admin/gelistirici/musteri — silinemez. */
  sistem: boolean;
};

export type RolUyesi = {
  keycloakId: string;
  kullaniciAdi: string;
  ePosta: string | null;
  adSoyad: string | null;
};

export type RolYonetimDurumu = { hazir: boolean; sebep: string | null };

export const getRoller = () => apiGet<RolOzeti[]>('/api/roller');
export const rolOlustur = (ad: string, aciklama: string | null) =>
  apiSend<RolOzeti>('/api/roller', 'POST', { ad, aciklama });
export const rolSil = (ad: string) => apiSend<void>(`/api/roller/${encodeURIComponent(ad)}`, 'DELETE');
export const getRolUyeleri = (ad: string) =>
  apiGet<RolUyesi[]>(`/api/roller/${encodeURIComponent(ad)}/uyeler`);
export const rolUyeEkle = (ad: string, keycloakId: string) =>
  apiSend<void>(`/api/roller/${encodeURIComponent(ad)}/uyeler/${keycloakId}`, 'POST');
export const rolUyeCikar = (ad: string, keycloakId: string) =>
  apiSend<void>(`/api/roller/${encodeURIComponent(ad)}/uyeler/${keycloakId}`, 'DELETE');
/** Üye ekleme adayları — yalnız kendi kiracının kullanıcıları. */
export const kullaniciAra = (arama: string) =>
  apiGet<RolUyesi[]>(`/api/roller/kullanicilar?arama=${encodeURIComponent(arama)}`);
export const rolDurum = () => apiGet<RolYonetimDurumu>('/api/roller/durum');
