/* S28 — Kadi olay kanalı: sayfalar Kadi'ye buradan komut yollar (bileşene import
 * bağımlılığı olmadan; Kadi kapalıysa komutlar sessizce boşa düşer). */

export type KadiDurum = '' | 'ucus' | 'okey' | 'dusun' | 'uzgun' | 'alarm' | 'selam' | 'kos';

/** Balon eylemi: tıklanabilir düğme — rota verilirse oraya gider, islem verilirse çağrılır. */
export type KadiEylem = { etiket: string; rota?: string; islem?: () => void };

export type KadiKomut =
  | { tur: 'soyle'; metin: string; sureMs?: number; eylemler?: KadiEylem[] }
  | { tur: 'durum'; durum: KadiDurum }
  | { tur: 'uc'; hedef: HTMLElement | null } // null → sağ alttaki evine döner
  | { tur: 'isaret'; hedef: HTMLElement; konum: 'sag' | 'sol' | 'alt' | 'ust' } // hedefin YANINA uçar
  | { tur: 'git'; x: number; y: number }     // serbest nokta — Kadi ekranın her yerini gezebilir
  /** Yükleme sahnesi: Kadi ortaya süzülür. <c>alan</c> verilirse EKRANIN değil o alanın ortasına
   * gelir — işin gerçekte olduğu yerde beklesin diye (ör. Soru Sor'da sonuç tuvali). */
  | { tur: 'merkez'; karartma?: KadiKarartma; alan?: string }
  | { tur: 'sus' };

/** Merkez sahnesinin perdesi. 'yogun': sayfa yükleniyor, içerik okunmasın · 'hafif': işlem
 * sürüyor, içerik görünür kalsın · 'yok': perdesiz (nadiren — sahnenin kendi bağlamı varsa). */
export type KadiKarartma = 'yogun' | 'hafif' | 'yok';

/* --- KONUŞKANLIK SEVİYESİ (S29/7 — Kadi ayarları): bu tarayıcıda saklanır.
 * kapali: Kadi hiç görünmez (perdeler klasik spinner'a döner) · sessiz: yalnız iş sahneleri,
 * cümle/ipucu/duyuru yok (uyarı nöbeti istisna) · normal: bugünkü davranış · konuskan: + bol ipucu. */
export type KadiSeviye = 'kapali' | 'sessiz' | 'normal' | 'konuskan';
const SEVIYE_ANAHTAR = 'kds.kadiSeviye';
let seviye: KadiSeviye = (() => {
  const s = localStorage.getItem(SEVIYE_ANAHTAR);
  return s === 'kapali' || s === 'sessiz' || s === 'konuskan' ? s : 'normal';
})();
const seviyeDinleyicileri = new Set<() => void>();
export const kadiSeviye = () => seviye;
export function kadiSeviyeAyarla(yeni: KadiSeviye) {
  seviye = yeni;
  localStorage.setItem(SEVIYE_ANAHTAR, yeni);
  seviyeDinleyicileri.forEach((d) => d());
  montajDinleyicileri.forEach((d) => d()); // kadiAktif de seviyeye bağlı — perdeler tazelensin
}
export function kadiSeviyeAbone(dinleyici: () => void): () => void {
  seviyeDinleyicileri.add(dinleyici);
  return () => seviyeDinleyicileri.delete(dinleyici);
}

const OLAY = 'kds-kadi';

function gonder(komut: KadiKomut) {
  window.dispatchEvent(new CustomEvent<KadiKomut>(OLAY, { detail: komut }));
}

export function kadiDinle(dinleyici: (komut: KadiKomut) => void): () => void {
  const sarici = (e: Event) => dinleyici((e as CustomEvent<KadiKomut>).detail);
  window.addEventListener(OLAY, sarici);
  return () => window.removeEventListener(OLAY, sarici);
}

export const kadiSoyle = (metin: string, sureMs = 3600, eylemler?: KadiEylem[]) =>
  gonder({ tur: 'soyle', metin, sureMs, eylemler });
export const kadiDurum = (durum: KadiDurum) => gonder({ tur: 'durum', durum });
export const kadiUc = (hedef: HTMLElement | null) => gonder({ tur: 'uc', hedef });
export const kadiIsaret = (hedef: HTMLElement, konum: 'sag' | 'sol' | 'alt' | 'ust' = 'sag') =>
  gonder({ tur: 'isaret', hedef, konum });
export const kadiGit = (x: number, y: number) => gonder({ tur: 'git', x, y });
/** <paramref name="alan"/>: CSS seçici. Verilirse Kadi ekranın değil O ALANIN ortasına gelir;
 * alan bulunamazsa ekran ortasına düşer (sahne asla kaybolmaz). */
export const kadiMerkez = (karartma: KadiKarartma = 'yogun', alan?: string) =>
  gonder({ tur: 'merkez', karartma, alan });
export const kadiSus = () => gonder({ tur: 'sus' });

/* Boş durum önerisi (S29/2): hedefin yanına uçup yol gösterir — sessiz/kapalı seviyede susar.
 * 1.4 sn gecikmeli: ekran giriş cümlesi (~0.4 sn'de gelir) öneriyi ezmesin; öneri ÜSTÜNE yazar. */
export function kadiOner(mesaj: string, hedef?: HTMLElement | null, eylemler?: KadiEylem[]) {
  if (seviye === 'kapali' || seviye === 'sessiz') return;
  setTimeout(() => {
    if (hedef?.isConnected) { kadiIsaret(hedef, 'alt'); setTimeout(() => kadiDurum('selam'), 990); setTimeout(() => kadiDurum(''), 2600); }
    kadiSoyle(mesaj, 8000, eylemler);
  }, 1400);
}

/* Uyarı nöbeti duyurusu (S29/1): kritik — sessizde bile çalışır, yalnız kapalıda susar. */
export function kadiNobet(mesaj: string, eylemler?: KadiEylem[]) {
  if (seviye === 'kapali') return;
  kadiDurum('alarm');
  kadiSoyle(mesaj, 9000, eylemler);
  setTimeout(() => kadiDurum(''), 3400);
}

/* --- Kadi montaj bayrağı: yükleme perdeleri Kadi yokken (kurulum/kimlik aşaması)
 * eski spinner'a düşebilsin diye. ABONELİKLİ: sayfalar ilk render'da Kadi'nin effect'i
 * koşmadan çizilir — bayrak değişince perdeler yeniden render olup Kadi sahnesine geçer
 * (useSyncExternalStore ile kullanılır; salt okuma anlık görüntüsü yetmez). --- */
let kadiMontajli = false;
const montajDinleyicileri = new Set<() => void>();
export function kadiKur(): () => void {
  kadiMontajli = true;
  montajDinleyicileri.forEach((d) => d());
  return () => {
    kadiMontajli = false;
    montajDinleyicileri.forEach((d) => d());
  };
}
export const kadiAktif = () => kadiMontajli && seviye !== 'kapali';
export function kadiAktifAbone(dinleyici: () => void): () => void {
  montajDinleyicileri.add(dinleyici);
  return () => montajDinleyicileri.delete(dinleyici);
}

/* --- YÜKLEME SAHNESİ: tüm "Yükleniyor…" perdeleri Kadi'yi ortaya çağırır; iç içe
 * yüklemeler sayaçla katlanır — sonuncusu bitince Kadi HEMEN evine döner (yapay
 * bekletme yok: yükleme anında bittiyse sahne de anında biter — kullanıcı kararı). --- */
let yuklemeSayaci = 0;
/** <paramref name="karartma"/> (24 Ağu 2026): sahne artık kendi PERDESİNİ getirir. Önce perde
 * yalnız LoadingCurtain/RunningIndicator bileşenlerindeydi; sayfalar doğrudan kadiYukleniyor
 * çağırınca (İçe Alım "Şimdi Çalıştır") Kadi perdesiz kalıyor, balonu beyaz kartların arasında
 * kayboluyordu — kullanıcı "Kadi ortaya geliyor ama yazı yok" diye bildirdi. */
/** Sahne biterken söylenecek son söz. Kadi ÖNCE evine (sağ alt) döner, cümleyi ORADA kurar —
 * merkezde duyurup sonra kayması, kullanıcının okuduğu balonu yerinden oynatıyordu (kullanıcı
 * bulgusu 24 Ağu 2026: "sonuç gelince sağ alta kayıp mesajını versin"). */
export type KadiSahneSonu = { mesaj: string; poz?: KadiDurum; sureMs?: number; eylemler?: KadiEylem[] };

export function kadiYukleniyor(mesaj: string, poz: KadiDurum = 'dusun',
  karartma?: KadiKarartma, alan?: string): (son?: KadiSahneSonu) => void {
  if (bitisZamani) { clearTimeout(bitisZamani); bitisZamani = null; }   // devralınan sahne
  yuklemeSayaci++;
  /* AÇILIŞ GECİKMESİ (180 ms): iş bundan önce biterse sahne HİÇ açılmaz. Kaydetme gibi çoğu
   * istek ~100 ms sürüyor; sahneyi anında açmak Kadi'yi ortaya fırlatıp geri çeken bir "flaş"
   * üretiyordu. Araştırma eşiğiyle de uyumlu: 1 sn altındaki işler gösterge istemez.
   * Devralınan sahnede (sayaç > 1) gecikme YOK — Kadi zaten ortada. */
  const acilisGecikmesi = yuklemeSayaci > 1 ? 0 : 180;
  const ac = () => {
    // Varsayılan poza bağlı: 'kos' = işlem sürüyor (içerik görünür) · 'dusun' = sayfa yükleniyor.
    kadiMerkez(karartma ?? (poz === 'kos' ? 'hafif' : 'yogun'), alan);
    kadiDurum(poz);
    kadiSoyle(mesaj, 0);
  };
  let acilisZamani: ReturnType<typeof setTimeout> | null = null;
  if (acilisGecikmesi === 0) ac();
  else acilisZamani = setTimeout(() => { acilisZamani = null; ac(); }, acilisGecikmesi);

  let bitti = false;
  return (son?: KadiSahneSonu) => {
    if (bitti) return;
    bitti = true;
    if (acilisZamani) { clearTimeout(acilisZamani); acilisZamani = null; }
    if (--yuklemeSayaci > 0) return;
    /* Bitişte KISA bir bekleme (yapay uzatma DEĞİL): ard arda sahnelerde — Suspense fallback
     * bitip sayfanın kendi perdesi açılırken — Kadi eve dönüp hemen geri geliyor, sıçrama
     * görünüyordu. Bu pencerede yeni sahne açılırsa devir kesintisiz olur (24 Ağu 2026). */
    if (bitisZamani) clearTimeout(bitisZamani);
    bitisZamani = setTimeout(() => {
      bitisZamani = null;
      if (yuklemeSayaci > 0) return;   // devralındı — Kadi merkezde kalsın
      kadiDurum(''); kadiSus(); kadiUc(null);
      /* Son söz TEMİZLİKTEN SONRA kurulur: aksi hâlde yukarıdaki kadiSus() onu yutardı. Sıra
       * bilerek böyle — Kadi önce evine döner (kadiUc(null)), cümleyi orada söyler. */
      if (son && seviye !== 'kapali' && seviye !== 'sessiz') {
        kadiDurum(son.poz ?? 'okey');
        kadiSoyle(son.mesaj, son.sureMs ?? 6000, son.eylemler);
        setTimeout(() => kadiDurum(''), 3200);
      }

      if (bekleyenDuyuru) { const d = bekleyenDuyuru; bekleyenDuyuru = null; duyuruOynat(d); }
    }, 140);
  };
}
let bitisZamani: ReturnType<typeof setTimeout> | null = null;
/** Sahne SÜRÜYOR mu? Sayaç sıfırlansa bile 140 ms'lik devir penceresi kapanmadan sahne bitmiş
 * SAYILMAZ: o pencerede gelen `kadiSus()` ekran giriş cümlesini yutuyordu (24 Ağu 2026 denetimi —
 * cümle DOM'a yazılıyor ama balon hiç açılmıyordu). Cümleyi erteleyen döngüler bunu sorar. */
export const kadiYukleniyorAktif = () => yuklemeSayaci > 0 || bitisZamani !== null;

/* --- İŞLEM DÖNÜŞÜ DUYURULARI (S28e — kullanıcı standardı): başarıda Kadi "tamamladım" der,
 * hatada üzülüp haber verir. Sayfalar mesajı özelleştirebilir; ayrıntı her zaman sayfadaki
 * Alert'tedir — Kadi yalnız duyurur. Yükleme sahnesi SÜRERKEN yapılan duyuru bekletilir ve
 * sahne bitince (Kadi eve dönerken) oynatılır — catch'ler finally'deki bitişten önce koşar. */
let duyuruZamani: ReturnType<typeof setTimeout> | null = null;
let bekleyenDuyuru: { durum: KadiDurum; mesaj: string; sureMs: number; eylemler?: KadiEylem[] } | null = null;
function duyuruOynat(d: { durum: KadiDurum; mesaj: string; sureMs: number; eylemler?: KadiEylem[] }) {
  kadiDurum(d.durum);
  kadiSoyle(d.mesaj, d.sureMs, d.eylemler);
  if (duyuruZamani) clearTimeout(duyuruZamani);
  duyuruZamani = setTimeout(() => kadiDurum(''), d.sureMs);
}
function duyur(durum: KadiDurum, mesaj: string, sureMs: number, eylemler?: KadiEylem[]) {
  if (seviye === 'kapali' || seviye === 'sessiz') return; // sessizde duyuru yok (nöbet ayrı)
  if (yuklemeSayaci > 0) { bekleyenDuyuru = { durum, mesaj, sureMs, eylemler }; return; }
  duyuruOynat({ durum, mesaj, sureMs, eylemler });
}
export const kadiBasarili = (mesaj = 'Tamamladım — işte burada.') => duyur('okey', mesaj, 2400);
/* S29/3 — yardım köprüsü: eylem verilirse balon tıklanabilir olur (ör. ayarlara götürür). */
export const kadiHata = (mesaj = 'Maalesef bir sorunla karşılaştım — ayrıntı hata mesajında.', eylemler?: KadiEylem[]) =>
  duyur('uzgun', mesaj, eylemler?.length ? 8000 : 4500, eylemler);

const bekle = (ms: number) => new Promise((coz) => setTimeout(coz, ms));

/* Ortak sahne: hedefe uç → çalışırken konuş → sonucu bildir → eve dön.
 * `is` asıl işi koşar; sahne yalnız süslemedir — iş hatası sahneyi bozmadan dışarı fırlar. */
export async function kadiGorevSahnesi<T>(ayar: {
  hedef: HTMLElement | null;
  calisirken: string;
  calisirkenDurum?: KadiDurum;
  is: () => Promise<T>;
  sonuc: (deger: T) => { basarili: boolean; mesaj: string };
  hataMesaji?: (hata: unknown) => string;
}): Promise<T> {
  if (ayar.hedef) { kadiUc(ayar.hedef); kadiDurum('ucus'); await bekle(1000); }
  kadiDurum(ayar.calisirkenDurum ?? 'dusun');
  kadiSoyle(ayar.calisirken, 0);
  try {
    const deger = await ayar.is();
    const s = ayar.sonuc(deger);
    kadiDurum(s.basarili ? 'okey' : 'uzgun');
    kadiSoyle(s.mesaj, 4200);
    return deger;
  } catch (hata) {
    kadiDurum('uzgun');
    kadiSoyle(ayar.hataMesaji ? ayar.hataMesaji(hata) : 'Olmadı — bir sorun çıktı.', 4200);
    throw hata;
  } finally {
    setTimeout(() => { kadiDurum('ucus'); kadiUc(null); setTimeout(() => kadiDurum(''), 1000); }, 2200);
  }
}
