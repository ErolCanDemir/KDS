// Erisim jetonunu ve "oturumu refresh" eylemini modül düzeyinde tutar ki apiGet
// hook'a bağımlı olmadan (bileşen dışından da) jetona ulaşsın ve 401'de yeniden
// girişi tetikleyebilsin. TokenBridge bileşeni bu değerleri oturuma göre günceller.
let gecerliJeton: string | undefined;
let yenileyici: (() => void) | undefined;

export function setToken(jeton: string | undefined): void {
  gecerliJeton = jeton;
}

export function jetonAl(): string | undefined {
  return gecerliJeton;
}

export function setSessionRefresher(fn: (() => void) | undefined): void {
  yenileyici = fn;
}

/** Oturum düştüğünde (401) yeniden giriş akışını tetikler; kayıtlı değilse sessizdir. */
export function refreshSession(): void {
  yenileyici?.();
}
