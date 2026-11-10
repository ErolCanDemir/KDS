import type { AskQuestionResult } from '../../api/soru';
import type { VisualizationType } from '../../api/reports';

/** Sohbetteki bir satır. Kadi cevabı SONUCU TAŞIR ama sohbette yalnız özetini gösterir —
 * tablo/grafik tuvalde çizilir (dar sütunda veri okunmuyor; tasarım araştırması bulgusu). */
export type Mesaj =
  | { id: string; rol: 'ben'; metin: string }
  | { id: string; rol: 'hata'; metin: string }
  | {
      id: string;
      rol: 'kadi';
      /** Tek cümlelik cevap: "3 servis buldum; en yüksek 1.969.910." */
      ozet: string;
      /** Bu cevabı doğuran soru (tuval başlığında görünür). */
      sorulan: string;
      cevap: AskQuestionResult;
      /** Kullanıcının bu cevap için seçtiği görünüm (AI önerisiyle başlar, oturumluk). */
      tur: VisualizationType;
      satir: number;
      ceviriMs: number;
      sorguMs: number;
    };

/** Takip çipi: tek tıkla düzeltme. `metin` düzeltme isteği olarak modele gider. */
export type TakipOnerisi = { etiket: string; metin: string };
