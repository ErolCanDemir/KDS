import { apiGet } from './client';

/** S41 — Yönetim → Kullanım. Kaynak: denetim kayıtları (S5); yeni ölçüm altyapısı yok.
 *
 * Kapsam sınırı: denetim kaydı SORGU KOŞUMUdur, "pano açıldı" diye bir olay yoktur — pano
 * kullanımı ancak kartlarındaki raporlar üzerinden dolaylı okunur. */
export type UsageOzet = {
  toplamKosum: number;
  basarisizKosum: number;
  yavasKosum: number;
  /** Sistem Ayarları'ndaki "yavaş sorgu" eşiği — ekran bunu TANIMLAMAZ, gösterir. */
  yavasEsikMs: number;
  ortalamaSureMs: number;
  enUzunSureMs: number;
  aktifKullanici: number;
};

export type YavasKosum = {
  id: string;
  zaman: string;
  sureMs: number;
  satirSayisi: number | null;
  raporAd: string | null;
  kullanici: string | null;
  sqlOnizleme: string;
};

export type RaporKullanimi = {
  raporId: string;
  ad: string;
  kosumSayisi: number;
  ortalamaSureMs: number;
  sonKosum: string;
  basarisizSayisi: number;
};

export type OluRapor = { id: string; ad: string; olusturmaTarihi: string };

export type Kullanim = {
  gunSayisi: number;
  ozet: UsageOzet;
  yavasKosumlar: YavasKosum[];
  raporKullanimlari: RaporKullanimi[];
  oluRaporlar: OluRapor[];
};

export const getKullanim = (gun: number) => apiGet<Kullanim>(`/api/kullanim?gun=${gun}`);
