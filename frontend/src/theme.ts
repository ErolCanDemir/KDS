import { createTheme, type Theme } from '@mui/material/styles';

/** Tema modu (21 Ağu 2026, mockup turu kararı): tek kimlik Lacivert & Teal, açık + koyu eş. */
export type TemaModu = 'acik' | 'koyu';

/** Koyu modun temel tonları — mockup'taki onaylı token çiftinin birebir karşılığı. */
const koyu = {
  bg: '#0b1622',
  panel: '#12202e',
  ink: '#dce7f2',
  mut: '#8ba3ba',
  cizgi: '#1f3245',
  teal: '#14b8c9',
};

/** Ürün kimliği: koyu lacivert + teal (00-analiz kapağı / login teması ile aynı).
 * Fabrika: aynı kimliğin açık ve koyu eşini üretir — bileşen kuralları ortak, yalnız renk değişir. */
export function temaYap(mod: TemaModu): Theme {
  const koyuMu = mod === 'koyu';
  return createTheme({
    palette: koyuMu
      ? {
          mode: 'dark',
          // Koyu zeminde lacivert görünmez — birincil eylem rengi teal'e döner (mockup kararı).
          primary: { main: koyu.teal },
          secondary: { main: '#2dd4bf' },
          background: { default: koyu.bg, paper: koyu.panel },
          text: { primary: koyu.ink, secondary: koyu.mut },
          divider: koyu.cizgi,
          success: { main: '#4ade80' },
          error: { main: '#f87171' },
          warning: { main: '#fbbf24' },
        }
      : {
          mode: 'light',
          primary: { main: '#0f2d4e' },
          secondary: { main: '#0e6f7a' },
          background: { default: '#f4f7fa', paper: '#ffffff' },
          success: { main: '#1c9d6b' },
        },
    shape: { borderRadius: 12 },
    // Yoğunluk standardı (2026-08-06, kullanıcı: "yazılar çok büyük, kalabalık"): taban 13px,
    // başlıklar küçük, kontroller varsayılan "small" + kompakt dolgu — TÜM ekranlar aynı.
    typography: {
      fontFamily: '"Segoe UI", Roboto, Arial, sans-serif',
      fontSize: 13,
      h5: { fontWeight: 700, letterSpacing: '-.3px', fontSize: 18 },
      h6: { fontWeight: 700, fontSize: 15.5 },
      body2: { fontSize: 12.5 },
    },
    components: {
      // Tema uyumu (2026-08-07): başlık şeridi markalı olunca altı da akraba olsun — paneller
      // gri-varsayılan yerine marka-tonlu soğuk sınır + ÇOK hafif lacivert gölgeyle "yüzer".
      MuiPaper: {
        styleOverrides: {
          root: { backgroundImage: 'none' },
          outlined: koyuMu
            ? { borderColor: koyu.cizgi, boxShadow: 'none' }
            : {
                borderColor: '#e2e9f0',
                boxShadow: '0 1px 3px rgba(15,45,78,.05), 0 4px 14px rgba(15,45,78,.04)',
              },
        },
      },
      // Standart (2026-08-06): buton ve input'lar app-genelinde KESKİN (oval değil) ve KOMPAKT.
      // Tema shape.borderRadius=12 kart/menü için; kontroller 8px + küçük boy + dar dolguya sabit.
      MuiButton: {
        defaultProps: { size: 'small' },
        styleOverrides: {
          root: {
            borderRadius: 8,
            // Birincil dolgulu buton başlık şeridiyle akraba: lacivert→teal gradient
            // (koyuda gece laciverti→teal). Bu MUI sürümünün tipinde containedPrimary
            // slot'u yok → sınıf seçicisiyle.
            '&.MuiButton-containedPrimary': koyuMu
              ? {
                  background: 'linear-gradient(135deg,#0e5c74,#14b8c9)',
                  color: '#fff',
                  boxShadow: '0 3px 10px rgba(0,0,0,.35)',
                  '&:hover': { background: 'linear-gradient(135deg,#0c4d61,#10a3b3)' },
                  '&.Mui-disabled': { background: 'rgba(20,184,201,.18)', color: 'rgba(255,255,255,.5)' },
                }
              : {
                  background: 'linear-gradient(135deg,#0f2d4e,#0e6f7a)',
                  boxShadow: '0 3px 10px rgba(15,45,78,.25)',
                  '&:hover': { background: 'linear-gradient(135deg,#0c2440,#0b5b65)' },
                  '&.Mui-disabled': { background: 'rgba(15,45,78,.18)', color: 'rgba(255,255,255,.7)' },
                },
          },
        },
      },
      MuiTextField: { defaultProps: { size: 'small' } },
      MuiSelect: { defaultProps: { size: 'small' } },
      MuiOutlinedInput: {
        styleOverrides: {
          root: { borderRadius: 8 },
          input: { paddingTop: 6.5, paddingBottom: 6.5, fontSize: 13 },
        },
      },
      // M20 (test turu): kontroller kompakt (input py 6.5, font 13) ama MUI'nin YÜZEN ETİKET konumu
      // varsayılan input yüksekliğine göre hesaplı kalıyordu — boş alanlarda etiket ("Harita adı" vb.)
      // kutu içinde yukarı kayık görünüyordu. Küçültülmüş kutuya göre dikey ortalanır; büzüşmüş
      // (shrink) hâline dokunulmaz.
      MuiInputLabel: {
        styleOverrides: {
          root: {
            fontSize: 13,
            '&.MuiInputLabel-outlined:not(.MuiInputLabel-shrink)': {
              transform: 'translate(14px, 7px) scale(1)',
            },
          },
        },
      },
    },
  });
}

/** Açık tema — geriye dönük varsayılan (testler ve tema-bilinçsiz kullanımlar için). */
export const theme = temaYap('acik');

/* Ray + bant, MOCKUP'ın birebir değerleri (kullanıcı bulgusu 23 Ağu: ray-bant köşesindeki renk
 * tuhaflığı mockup'tan sapmadan geliyordu). Ray LACİVERTTE kalır (teal'e akmaz); bant rayın
 * bittiği lacivertten teal'e akar — köşe böylece kaynaşır. */
export function rayGradyani(mod: TemaModu): string {
  return mod === 'koyu'
    ? 'linear-gradient(180deg, #081420 0%, #0b1c2c 100%)'
    : 'linear-gradient(180deg, #0c2036 0%, #0f2a43 100%)';
}

/** Üst bandın GÖRSEL TAKIMI: zemin ve üstündeki her öğenin rengi tek yerden gelir.
 * Öğe renkleri ayrı ayrı '#fff' diye sabitlenirse açık zeminli bir varyantta metin/ikon
 * görünmez olur — o yüzden zeminle birlikte taşınırlar. */
export type BantStili = {
  zemin: string;
  metin: string;
  metinSolgun: string;
  ikonZemin: string;
  avatarZemin: string;
  avatarMetin: string;
  /** Açık zeminli varyantta bandı gövdeden ayıran ince çizgi; koyu bantta gerekmez. */
  altCizgi?: string;
};

/* S43 (27 Ağu 2026) — kullanıcı bulgusu: "header karanlık ve açık temada değişmiyor". Ölçüm
 * doğruladı: eski açık-tema bandı #0f2a43→#12958a ile koyu bandı #0b1c2c→#0d7a70 arasındaki fark
 * gözle seçilmiyordu. İki aday ekranda karşılaştırıldı (canlı teal / açık zemin+koyu metin);
 * kullanıcı CANLI TEAL'i seçti: kimlik (lacivert→teal akış) iki temada da tanınıyor.
 * NOT: açık temada bant artık rayın bittiği lacivertten DEĞİL, doğrudan teal'den başlıyor —
 * yani aşağıdaki rayGradyani yorumunda anlatılan köşe kaynaşması açık temada geçerli değil,
 * orada ray ile bant arasında bilinçli bir sınır var (ekranda görülüp onaylandı). */
export function bantStili(mod: TemaModu): BantStili {
  if (mod === 'koyu') {
    return {
      zemin: 'linear-gradient(90deg, #0b1c2c 0%, #0d7a70 100%)',
      metin: '#fff',
      metinSolgun: 'rgba(255,255,255,.85)',
      ikonZemin: 'rgba(255,255,255,.15)',
      avatarZemin: 'rgba(255,255,255,.22)',
      avatarMetin: '#fff',
    };
  }
  // Açık tema: aynı lacivert→teal akışı, belirgin biçimde açılmış ve doygunlaşmış hâliyle.
  return {
    zemin: 'linear-gradient(90deg, #106e86 0%, #2ec4b6 100%)',
    metin: '#fff',
    metinSolgun: 'rgba(255,255,255,.9)',
    ikonZemin: 'rgba(255,255,255,.2)',
    avatarZemin: 'rgba(255,255,255,.28)',
    avatarMetin: '#fff',
  };
}

/** Kutu yarıçap standardı (2026-08-06) — tüm kart/panel/diyalog/tablo kutuları aynı olsun diye.
 * sx multiplier'ı (borderRadius:3 = 36px) oval çıkardığından net px kullanılır. */
export const boxRadius = '10px'; // kart · panel · diyalog · tablo kutusu
export const smallRadius = '8px'; // buton · liste satırı · input
