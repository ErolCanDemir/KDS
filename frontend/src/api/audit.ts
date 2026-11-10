import { apiGet } from './client';

export type AuditRecord = {
  id: string;
  zaman: string;
  kullaniciAd: string | null;
  baglantiAd: string | null;
  sql: string;
  parametreler: string | null;
  basarili: boolean;
  satirSayisi: number | null;
  sureMs: number;
  hata: string | null;
  raporAd: string | null; // M26: kayıtlı rapor koşumuysa raporun adı
};

export type AuditPage = { toplam: number; kayitlar: AuditRecord[] };

export type AuditFilter = {
  sayfa?: number;
  sayfaBoyut?: number;
  basarili?: boolean | null;
  arama?: string;
};

export function getAudit(f: AuditFilter): Promise<AuditPage> {
  const p = new URLSearchParams();
  p.set('sayfa', String(f.sayfa ?? 0));
  p.set('sayfaBoyut', String(f.sayfaBoyut ?? 50));
  if (f.basarili !== undefined && f.basarili !== null) p.set('basarili', String(f.basarili));
  if (f.arama?.trim()) p.set('arama', f.arama.trim());
  return apiGet<AuditPage>(`/api/denetim?${p.toString()}`);
}
