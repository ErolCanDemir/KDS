import { kadiYukleniyor, kadiSoyle } from './kadiKanal';

/** Pano/galeri kartlarının TOPLU yükleme sahnesi.
 *
 * Kartlar birbirinden bağımsız koşar (her biri kendi isteğini atar) — bu iyi, ızgara bloklanmıyor.
 * Ama Kadi tarafında her kart kendi sahnesini açsaydı aynı cümle 7 kez üst üste yazılır, sayaç
 * 7'ye çıkar ve kullanıcı ne kadar kaldığını göremezdi. Burada tek sahne açılır ve kartlar
 * bitirdikçe cümle ilerler: "Panoyu çalıştırıyorum… 3/7".
 *
 * Açılış bir tick ertelenir: kartlar aynı render'da mount olduğu için ilk katılımda toplam
 * henüz 1'dir; tick sonunda gerçek toplamla açılır ve cümle ilk andan doğru okunur. */
let toplam = 0;
let biten = 0;
let bitir: (() => void) | null = null;
let acilisBekliyor = false;
let sifirlaZamani: ReturnType<typeof setTimeout> | null = null;

function mesaj(): string {
  return toplam > 1
    ? `Panoyu çalıştırıyorum… ${biten}/${toplam}`
    : 'Raporu çalıştırıyorum…';
}

/** Bir kart koşuya katılır; dönen işlev o kart bitince (başarı ya da hata) çağrılır. */
export function panoKartiKatil(): () => void {
  if (sifirlaZamani) {
    clearTimeout(sifirlaZamani);
    sifirlaZamani = null;
  }

  toplam++;
  if (bitir) {
    kadiSoyle(mesaj(), 0);
  } else if (!acilisBekliyor) {
    acilisBekliyor = true;
    setTimeout(() => {
      acilisBekliyor = false;
      // Bu arada hepsi bitmiş olabilir (önbellekten dönen hızlı kartlar) — sahneyi hiç açma.
      if (biten < toplam) {
        bitir = kadiYukleniyor(mesaj(), 'kos', 'hafif');
      }
    }, 0);
  }

  let dusuldu = false;
  return () => {
    if (dusuldu) {
      return;
    }

    dusuldu = true;
    biten++;
    if (biten < toplam) {
      kadiSoyle(mesaj(), 0);
      return;
    }

    bitir?.();
    bitir = null;
    // Tazeleme/parametre değişimi yeni bir koşudur: sayaçlar sıfırlanır. Kısa gecikme, aynı
    // turda geç mount olan kartın koşuyu baştan başlatmasını değil, mevcut tura katılmasını sağlar.
    sifirlaZamani = setTimeout(() => {
      toplam = 0;
      biten = 0;
      sifirlaZamani = null;
    }, 400);
  };
}
