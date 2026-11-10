import { WebStorageStateStore } from 'oidc-client-ts';
import type { AuthProviderProps } from 'react-oidc-context';

/** Keycloak erişilemediğinde (ör. Docker/Java yok) kimlik akışını kapatan geliştirme
 * bayrağı. Varsayılan: açık — S3'ten itibaren gerçek Keycloak login'i beklenir. */
export const kimlikKapali = import.meta.env.VITE_KIMLIK_KAPALI === 'true';

// Ortam-özel adres: dev'de yerel varsayılan; ÜRETİMDE env verilmemişse S48 tek-origin yayın
// deseni geçerlidir — kimlik, sayfanın kendi adresinin /kimlik yolundadır (Caddy orayı
// Keycloak'a vekiller). Bu, eski "prod'da env zorunlu" kuralının (kod incelemesi #1) bilinçli
// revizyonudur: yayın adresi (tünel) derleme ANINDA bilinemez, adresi derlemeye gömmek her
// adres değişiminde yeniden derleme demekti; origin'den türetmek sessiz-localhost tuzağı da
// taşımaz (origin her zaman kullanıcının gerçekten baktığı adrestir). Env yine de her şeyi ezer.
const yetkili = import.meta.env.VITE_KIMLIK_YETKILI
  ?? (import.meta.env.DEV
    ? 'http://localhost:8080/realms/kds'
    : `${window.location.origin}/kimlik/realms/kds`);

export const oidcAyar: AuthProviderProps = {
  authority: yetkili ?? '',
  client_id: import.meta.env.VITE_KIMLIK_ISTEMCI ?? 'kds-web',
  redirect_uri: window.location.origin,
  post_logout_redirect_uri: window.location.origin,
  scope: 'openid profile email',
  // Token'lar sessionStorage'da (localStorage yerine): sekme kapanınca temizlenir,
  // XSS'e açık pencere daralır. Kalıcılık yerine güvenlik önceliği (kod incelemesi #3).
  userStore: new WebStorageStateStore({ store: window.sessionStorage }),
  // Kod incelemesi O9: sessiz yenileme AÇIKÇA sabitlenir (kütüphane varsayılanına güvenme) —
  // access token süresi dolmadan refresh token'la yenilenir; uzun oturumda kullanıcı tam
  // yeniden-girişe düşmez. 401'de istemcinin refreshSession() tetiklemesi yedek olarak kalır.
  automaticSilentRenew: true,
  onSigninCallback: () => {
    window.history.replaceState({}, document.title, window.location.pathname);
  },
};
