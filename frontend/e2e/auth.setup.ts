import { test as setup, expect } from '@playwright/test';
import fs from 'node:fs';
import { AUTH_DIZIN, OIDC_DOSYA } from './paths';

// Keycloak login otomasyonu: uygulama açılınca Keycloak'a yönlenir; formu doldurup geri döneriz.
// Token sessionStorage'da (oidc-client-ts) → oidc kullanıcı bloğunu dosyaya yazarız; testler
// bunu her sayfaya enjekte eder (storageState sessionStorage'ı taşımaz). Kimlik bilgileri env ile
// override edilebilir; varsayılan geliştirme kullanıcısı admin/admin (yalnız yerel realm).
const KULLANICI = process.env.KDS_KULLANICI ?? 'admin';
const PAROLA = process.env.KDS_PAROLA ?? 'admin';

setup('keycloak girişi', async ({ page }) => {
  fs.mkdirSync(AUTH_DIZIN, { recursive: true });

  await page.goto('/');

  // Keycloak login ekranına yönlendik mi bekle (özel "kds" teması da standart id'leri korur).
  await page.waitForURL(/\/realms\/kds\/protocol\/openid-connect\/auth|localhost:8080/, { timeout: 30_000 });
  await page.locator('#username').fill(KULLANICI);
  await page.locator('#password').fill(PAROLA);
  await page.locator('#kc-login, button[type=submit], input[type=submit]').first().click();

  // Uygulamaya kimlikli dönüş.
  await page.waitForURL(/localhost:5173/, { timeout: 30_000 });

  // oidc-client-ts kullanıcıyı sessionStorage'a yazana kadar bekle, sonra dosyala.
  const kullanici = await page.waitForFunction(() => {
    for (let i = 0; i < sessionStorage.length; i++) {
      const k = sessionStorage.key(i);
      if (k && k.startsWith('oidc.user:')) return { key: k, value: sessionStorage.getItem(k) };
    }
    return null;
  }, { timeout: 20_000 });

  const veri = (await kullanici.jsonValue()) as { key: string; value: string } | null;
  expect(veri, 'oidc kullanıcısı sessionStorage\'da bulunamadı').toBeTruthy();
  fs.writeFileSync(OIDC_DOSYA, JSON.stringify(veri));
});
