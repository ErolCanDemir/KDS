import { test as base, expect } from '@playwright/test';
import fs from 'node:fs';
import { KEYCLOAK } from './apiYardimci';
import { OIDC_DOSYA } from './paths';

// Her sayfa yüklemesinden ÖNCE, setup'ta kaydedilen oidc kullanıcısını sessionStorage'a enjekte
// eder → react-oidc-context geçerli kullanıcıyı bulur, login yönlendirmesi olmadan kimlikli açılır.

/**
 * Enjekte edilecek oidc kullanıcısını TAZELER.
 *
 * Neden: access token ömrü **15 dakika**, tam süit ise ~37 dakika sürüyor. Setup'ta bir kez alınan
 * token koşumun ortasında ölüyordu ve o andan sonraki HER test düşüyordu — sebebi görünmediği için
 * "testler kırık" sanılıyordu; oysa 19 Ağu 2026'da ölçüldü: `nav.spec` tek başına 10.8 sn'de
 * geçerken süitte 16:29'da (token ölümünden 1 dk sonra) düşüyordu. Yani süit bugüne kadar hiç
 * sonuna kadar koşamamıştı.
 *
 * Her testten önce refresh token ile yenilenir; yenilenen değer dosyaya geri yazılır — böylece
 * Keycloak refresh token'ı döndürse (rotation) bile zincir kopmaz ve SSO oturumu boşta sayılmaz.
 * Yenileme başarısızsa eldeki kullanıcı kullanılır: kısa koşumlarda o hâlâ geçerlidir, yani
 * tazeleme bir iyileştirmedir, yeni bir kırılma noktası değil.
 */
async function tazeKullanici(): Promise<{ key: string; value: string } | null> {
  if (!fs.existsSync(OIDC_DOSYA)) return null;

  const kayit = JSON.parse(fs.readFileSync(OIDC_DOSYA, 'utf8')) as { key: string; value: string };
  const kullanici = JSON.parse(kayit.value) as {
    access_token?: string; refresh_token?: string; id_token?: string; expires_at?: number;
  };
  if (!kullanici.refresh_token) return kayit;

  const yanit = await fetch(`${KEYCLOAK}/realms/kds/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      client_id: 'kds-web',
      grant_type: 'refresh_token',
      refresh_token: kullanici.refresh_token,
    }),
  });
  if (!yanit.ok) return kayit;

  const yeni = (await yanit.json()) as {
    access_token?: string; refresh_token?: string; id_token?: string; expires_in?: number;
  };
  kullanici.access_token = yeni.access_token ?? kullanici.access_token;
  if (yeni.refresh_token) kullanici.refresh_token = yeni.refresh_token;
  if (yeni.id_token) kullanici.id_token = yeni.id_token;
  // expires_at olmadan oidc-client-ts token'ı süresi geçmiş sayıp sessiz yenilemeye kalkar (test
  // bağlamında iframe akışı çalışmaz) → saniye cinsinden güncellenir.
  kullanici.expires_at = Math.floor(Date.now() / 1000) + (yeni.expires_in ?? 900);

  const guncel = { key: kayit.key, value: JSON.stringify(kullanici) };
  fs.writeFileSync(OIDC_DOSYA, JSON.stringify(guncel));
  return guncel;
}

export const test = base.extend({
  page: async ({ page }, use) => {
    const kullanici = await tazeKullanici();
    if (kullanici) {
      await page.addInitScript(
        ([k, v]) => window.sessionStorage.setItem(k, v),
        [kullanici.key, kullanici.value] as [string, string],
      );
    }
    // oxlint-disable-next-line react-hooks/rules-of-hooks -- Playwright fixture `use`, React hook değil
    await use(page);
  },
});

export { expect };
