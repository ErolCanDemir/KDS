// S16/A2 — Kurulum sihirbazının API'si. Bilerek `client.ts` KULLANILMAZ: kurulum sırasında
// oturum/jeton yoktur (kimlik sağlayıcı henüz yapılandırılmamış olabilir) ve 401'de yeniden
// giriş tetiklenmemelidir. Güvenlik jeton yerine tek kullanımlık KURULUM ANAHTARI ile sağlanır.
const apiAdres = import.meta.env.VITE_API_ADRES ?? (import.meta.env.DEV ? 'http://localhost:5210' : '');

export type KurulumDurum = { yapilandirildi: boolean; surum: number; anahtarDosyasi: string | null };

export type VeritabaniIstek = { sunucu: string; veritabani: string; kullaniciAdi?: string; sifre?: string };
export type VeritabaniTest = {
  basarili: boolean; mesaj: string; sunucuSurumu?: string | null;
  veritabaniVar: boolean; olusturabilirMi: boolean; ddlYetkisi: boolean;
};
export type KimlikIstek = { yetkili: string; istemci: string; metadataAdresi?: string | null; httpsZorunlu: boolean };
export type KimlikTest = { basarili: boolean; mesaj: string; yayinci?: string | null };
export type TamamlaIstek = {
  veritabani: VeritabaniIstek;
  kimlik?: KimlikIstek | null;
  kiraciAdi: string;
  kiraciId?: string | null;
  onyuzAdres?: string | null;
  depoSunucusu?: string | null;
  logBaglantisi?: string | null;
};
export type TamamlaYanit = { basarili: boolean; mesaj: string; kiraciId: string };

async function gonder<T>(yol: string, anahtar: string, govde: unknown): Promise<T> {
  const yanit = await fetch(`${apiAdres}/api/kurulum/${yol}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Kurulum-Anahtari': anahtar },
    body: JSON.stringify(govde),
  });
  const metin = await yanit.text();
  const veri = metin ? JSON.parse(metin) : null;
  if (!yanit.ok) {
    if (yanit.status === 401) throw new Error('Kurulum anahtarı geçersiz — sunucudaki kurulum-anahtari.txt dosyasına bakın.');
    if (yanit.status === 423) throw new Error('Çok sayıda hatalı deneme: kurulum kilitlendi, uygulamayı yeniden başlatın.');
    if (yanit.status === 410) throw new Error('Uygulama zaten kurulu.');
    throw new Error(veri?.mesaj ?? `Kurulum hatası (${yanit.status})`);
  }
  return veri as T;
}

/** Açılışta sorulan tek uç (anahtar istemez): uygulama kurulu mu? */
export async function kurulumDurumu(): Promise<KurulumDurum> {
  const yanit = await fetch(`${apiAdres}/api/kurulum/durum`);
  if (!yanit.ok) throw new Error(`Kurulum durumu okunamadı (${yanit.status})`);
  return (await yanit.json()) as KurulumDurum;
}

export const veritabaniTest = (anahtar: string, istek: VeritabaniIstek) =>
  gonder<VeritabaniTest>('veritabani-test', anahtar, istek);

export const kimlikTest = (anahtar: string, istek: KimlikIstek) =>
  gonder<KimlikTest>('kimlik-test', anahtar, istek);

export const kurulumTamamla = (anahtar: string, istek: TamamlaIstek) =>
  gonder<TamamlaYanit>('tamamla', anahtar, istek);
