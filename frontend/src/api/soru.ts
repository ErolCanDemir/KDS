import { apiGet, apiSend } from './client';
import type { QueryModel, QueryRunResult } from './queries';

/** Soru Sor (S23 · NLQ) cevabı — sonuçla birlikte ŞEFFAFLIK alanları: "nasıl hesaplandı"
 * maddeleri, modele giden istemin TAMAMI (Show Prompt kuralı) ve modelin ham çıktısı. */
export type AskQuestionResult = {
  sonuc: QueryRunResult;
  /** AI'nın kurduğu sorgu modeli — "Rapor olarak kaydet" bunu normal rapora çevirir. */
  model: QueryModel;
  nasilHesaplandi: string[];
  gidenIstem: string;
  modelCiktisi: string;
  ceviriSureMs: number;
  /** S30: cevabın en iyi görünümü — model önerir, öneri geçersizse sunucu modele bakıp seçer. */
  oneriGorsellestirme: string;
  /** S31: sorgunun yapılandırılmış hâli — durum rayı ve atıf satırı bunu basar. */
  durum: SorguDurumu | null;
};

/** Cevabın neye dayandığının tek bakışta görünür hâli (durum rayı + atıf satırı). */
export type SorguDurumu = {
  haritaAd: string;
  tablolar: string[];
  metrikler: MetrikAtfi[];
  cipler: DurumCipi[];
};

export type MetrikAtfi = { id: string; ad: string; sonDegisiklik: string | null };

/** tur: metrik | kirilim | hesap | filtre | siralama | limit */
export type DurumCipi = { tur: string; etiket: string; kaldirilabilir: boolean };

export type NlqStatus = { hazir: boolean; model: string | null; sebep: string | null };

/** Sorar. Yerel model CPU'da uzun düşünebilir (60-120 sn olağan) — çağıran, bekleme durumunu
 * "yerel model düşünüyor" diye göstermeli; AbortSignal ile vazgeçilebilir.
 * S30 çok turlu düzeltme: oncekiModel verilirse soru o modele uygulanacak DÜZELTME sayılır. */
export const soruSor = (haritaId: string, soru: string, signal?: AbortSignal, oncekiModel?: QueryModel) =>
  apiSend<AskQuestionResult>('/api/soru', 'POST',
    { haritaId, soru, oncekiModelJson: oncekiModel ? JSON.stringify(oncekiModel) : null },
    undefined, signal);

/** Yerel model hazır mı? Değilse sebep alanı kurulum yönergesidir (ekran karta basar). */
export const soruDurum = () => apiGet<NlqStatus>('/api/soru/durum');

/** S34 — ISITMA: harita seçilir seçilmez çağrılır, modele istem önekini önceden işletir.
 * Kullanıcı sorusunu YAZARKEN çalıştığı için beklenmez ve sonucu kullanılmaz; ölçüm (24 Ağu 2026):
 * önek işleme ilk seferde 49.7 sn, önbellekten 0.2 sn — yani ilk sorunun yarım dakikası
 * yazma süresine gizlenir. Hata bilerek yutulur: ısıtma başarısız olsa da soru sorulabilir. */
/* Isıtma HARİTA BAŞINA seyrekleştirilir: ekranlar arası her gidiş gelişte yeniden ısıtmak,
 * Ollama tek tek işlediği için gerçek soruyu kuyruğa itiyordu. Pencere modelin bellekte kalma
 * süresinden (keep_alive 30 dk) kısa tutulur ki model düşmeden önce tazelensin. */
const ISITMA_PENCERESI_MS = 15 * 60 * 1000;
const sonIsitma = new Map<string, number>();

export const soruIsit = (haritaId: string, signal?: AbortSignal) => {
  const onceki = sonIsitma.get(haritaId);
  if (onceki && Date.now() - onceki < ISITMA_PENCERESI_MS) {
    return Promise.resolve(undefined);
  }

  sonIsitma.set(haritaId, Date.now());
  return apiSend<{ isitildi: boolean; onekKarakter: number }>('/api/soru/isit', 'POST', { haritaId },
    undefined, signal)
    // Başarısızlıkta damgayı DÜŞÜR: bir sonraki açılış yeniden denesin, 15 dk beklemesin.
    .catch(() => { sonIsitma.delete(haritaId); return undefined; });
};
