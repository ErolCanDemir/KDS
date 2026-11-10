import { apiDownload } from './client';

export type ExportFormat = 'Csv' | 'Xlsx' | 'Pdf';

/** Raporu sunucuda yeniden çalıştırıp (tek kapı + denetim) sonucu dosya olarak indirir.
 * M10: Dışa aktarım yalnız KAYITLI raporda — dosya adı ve içerik başlığı rapor adıdır
 * (editör yüzeylerindeki dışa aktarım kaldırıldı). */
export const exportReport = (id: string, bicim: ExportFormat) =>
  apiDownload(`/api/raporlar/${id}/disa-aktar?bicim=${bicim}`, 'GET');
