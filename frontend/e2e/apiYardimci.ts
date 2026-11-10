import fs from 'node:fs';
import type { APIRequestContext } from '@playwright/test';
import { OIDC_DOSYA } from './paths';

// Test-olmayan ortak modül (Playwright: test dosyaları birbirini import edemez).
// Kimlik: setup projesi oidc kullanıcısını dosyaya yazar; access_token oradan okunur, böylece
// API'ye tarayıcı açmadan gidilebilir.

export const API = process.env.KDS_API ?? 'http://localhost:5210';
export const KEYCLOAK = process.env.KDS_KEYCLOAK ?? 'http://localhost:8080';

/** Testlerin ürettiği kayıtların ortak öneki — teardown YALNIZ bunu siler. */
export const ONEK = 'E2E ';

function kullanici(): { access_token?: string; refresh_token?: string } | null {
  if (!fs.existsSync(OIDC_DOSYA)) return null;
  const { value } = JSON.parse(fs.readFileSync(OIDC_DOSYA, 'utf8')) as { value: string };
  return JSON.parse(value) as { access_token?: string; refresh_token?: string };
}

export function jeton(): string | null {
  return kullanici()?.access_token ?? null;
}

/**
 * TAZE access token — setup'takini körü körüne kullanmaz.
 *
 * Neden: access token ömrü **15 dakika**, tam süit ise 37 dakika sürüyor. Setup'ta bir kez alınan
 * token koşumun ortasında ölüyor; 19 Ağu 2026'da teardown tam bu yüzden hiçbir şeyi temizleyemedi
 * (her uçtan 401 aldı). Refresh token'la yenilenir, yenilenemezse eldekiyle denenir (kısa
 * koşumlarda o hâlâ geçerlidir).
 */
export async function tazeJeton(request: APIRequestContext): Promise<string | null> {
  const k = kullanici();
  if (!k?.refresh_token) return k?.access_token ?? null;

  const yanit = await request.post(`${KEYCLOAK}/realms/kds/protocol/openid-connect/token`, {
    form: { client_id: 'kds-web', grant_type: 'refresh_token', refresh_token: k.refresh_token },
  });
  if (!yanit.ok()) return k.access_token ?? null;
  return ((await yanit.json()) as { access_token?: string }).access_token ?? k.access_token ?? null;
}

export function baslik(jetonDegeri?: string | null): Record<string, string> {
  return { Authorization: `Bearer ${jetonDegeri ?? jeton() ?? ''}` };
}

/**
 * Adı verilen ham SQL raporunun VAR OLMASINI garanti eder (yoksa oluşturur), id'sini döndürür.
 *
 * Neden: pano testi grid'i doğrulamak için bir rapora ihtiyaç duyuyor ve bunu BAŞKA bir testin
 * bıraktığı kayıttan umuyordu ("sıralı koşumda E2E SQL Rapor mevcut"). Bu kırılgan bir desendi —
 * tek başına ya da farklı sırayla koşulunca test düşüyordu (19 Ağu 2026'da düştü). Artık test
 * kendi bağımlılığını üretiyor; ad ONEK taşıdığı için teardown sonunda temizler.
 */
export async function hamSqlRaporVarEt(request: APIRequestContext, ad: string): Promise<string> {
  const j = await tazeJeton(request);
  const mevcut = await request.get(`${API}/api/raporlar`, { headers: baslik(j) });
  const rapor = ((await mevcut.json()) as { id: string; ad: string }[]).find((r) => r.ad === ad);
  if (rapor) return rapor.id;

  const baglantilar = (await (await request.get(`${API}/api/baglantilar`, { headers: baslik(j) })).json()) as {
    id: string; ad: string; veritabani: string;
  }[];
  // Demo veritabanına bakan bir bağlantı seç; testlerin kendi kurduğu "KdsDemo E2E" varsa o.
  const baglanti = baglantilar.find((b) => b.ad === 'KdsDemo E2E')
    ?? baglantilar.find((b) => b.veritabani === 'KdsDemo');
  if (!baglanti) throw new Error('KdsDemo bağlantısı yok — E2E ön koşulu sağlanmadı.');

  const yanit = await request.post(`${API}/api/raporlar`, {
    headers: baslik(j),
    data: {
      id: null,
      ad,
      klasorId: null,
      model: null,
      gorsellestirme: 'Tablo',
      paylasim: 'Ekip',
      // Hafif tutuldu: pano widget'ı hızlı render olsun, soğuk önbellekte zaman aşımına girmesin.
      hamSql: 'SELECT TOP 5 Id, ServisId FROM talep.Talepler',
      baglantiId: baglanti.id,
    },
  });
  if (!yanit.ok()) throw new Error(`Rapor oluşturulamadı (${yanit.status()}): ${await yanit.text()}`);
  return ((await yanit.json()) as { id: string }).id;
}
