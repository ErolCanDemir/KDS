import path from 'node:path';

// Test-olmayan ortak modül (Playwright: test dosyaları birbirini import edemez).
export const AUTH_DIZIN = path.join('e2e', '.auth');
export const OIDC_DOSYA = path.join(AUTH_DIZIN, 'oidc-user.json');
