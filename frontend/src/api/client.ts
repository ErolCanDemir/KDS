import { jetonAl, refreshSession } from '../identity/tokenStore';

// Dev'de yerel API; prod'da env ile verilir, yoksa aynı origin (relative) — sessizce
// localhost'a düşmez (kod incelemesi #1).
const apiAdres = import.meta.env.VITE_API_ADRES ?? (import.meta.env.DEV ? 'http://localhost:5210' : '');

/* S48/ngrok — ücretsiz ngrok, tarayıcıdan gelen İLK isteklere "Visit Site" ARA SAYFASI (HTML)
 * döndürebilir; fetch bu HTML'i JSON sanıp patlar. Bu başlık ara sayfayı atlatır; kendi API'miz
 * ve diğer tüneller başlığı yok sayar — koşulsuz eklemek zararsızdır. */
const TUNEL_BASLIKLARI = { 'ngrok-skip-browser-warning': '1' } as const;

/** Backend'e GET. Jeton verilmezse geçerli oturum jetonu (varsa) otomatik eklenir.
 * 401 (oturum düştü/iptal) gelirse yeniden giriş tetiklenir (kod incelemesi #6). */
export async function apiGet<T>(
  yol: string,
  jeton: string | undefined = jetonAl(),
  signal?: AbortSignal, // O8: sayfadan ayrılınca uçuştaki istek iptal edilebilsin
): Promise<T> {
  const yanit = await fetch(`${apiAdres}${yol}`, {
    headers: { ...TUNEL_BASLIKLARI, ...(jeton ? { Authorization: `Bearer ${jeton}` } : {}) },
    signal,
  });
  if (yanit.status === 401) {
    refreshSession();
    throw new Error(`API 401: ${yol}`);
  }
  if (!yanit.ok) {
    // Hata GÖVDESİ okunur (apiSend ile aynı): sunucu ProblemDetails'te sebebi söylüyorsa
    // kullanıcı "API 403: /api/haritalar" gibi ham bir metin yerine onu görsün. Ekran
    // testinde yakalandı — GET'lerde sunucunun anlamlı mesajları atılıyordu.
    const metin = await yanit.text().catch(() => '');
    throw new Error(problemMetni(metin) || durumMetni(yanit.status, yol));
  }
  return (await yanit.json()) as T;
}

/** Backend'e gövdeli istek (POST/PUT/DELETE). Boş yanıtta (204) undefined döner; hata
 * gövdesi (doğrulama mesajı vb.) varsa hata metnine yansıtılır. */
export async function apiSend<T>(
  yol: string,
  yontem: 'POST' | 'PUT' | 'DELETE',
  govde?: unknown,
  jeton: string | undefined = jetonAl(),
  signal?: AbortSignal, // O8
): Promise<T> {
  const yanit = await fetch(`${apiAdres}${yol}`, {
    method: yontem,
    headers: {
      ...TUNEL_BASLIKLARI,
      ...(jeton ? { Authorization: `Bearer ${jeton}` } : {}),
      ...(govde !== undefined ? { 'Content-Type': 'application/json' } : {}),
    },
    body: govde !== undefined ? JSON.stringify(govde) : undefined,
    signal,
  });
  if (yanit.status === 401) {
    refreshSession();
    throw new Error(`API 401: ${yol}`);
  }
  if (!yanit.ok) {
    const metin = await yanit.text().catch(() => '');
    throw new Error(problemMetni(metin) || durumMetni(yanit.status, yol));
  }
  const govdeMetni = await yanit.text();
  return (govdeMetni ? JSON.parse(govdeMetni) : undefined) as T;
}

/** Backend'den DÜZ METİN yanıt (S46 — OSI YAML gibi JSON olmayan gövdeler). Hata yolu apiGet
 * ile aynı; başarıda gövde ayrıştırılmadan döner. */
export async function apiGetText(yol: string, jeton: string | undefined = jetonAl()): Promise<string> {
  const yanit = await fetch(`${apiAdres}${yol}`, {
    headers: { ...TUNEL_BASLIKLARI, ...(jeton ? { Authorization: `Bearer ${jeton}` } : {}) },
  });
  if (yanit.status === 401) {
    refreshSession();
    throw new Error(`API 401: ${yol}`);
  }
  if (!yanit.ok) {
    const metin = await yanit.text().catch(() => '');
    throw new Error(problemMetni(metin) || durumMetni(yanit.status, yol));
  }
  return yanit.text();
}

/** Kimlikli bir ucu çağırıp dönen dosyayı (blob) tarayıcıda indirir (S13 dışa aktarım). Dosya adı
 * Content-Disposition başlığından çözülür. GET (rapor) ve POST+gövde (ham SQL) desteklenir. */
export async function apiDownload(
  yol: string,
  yontem: 'GET' | 'POST',
  govde?: unknown,
  jeton: string | undefined = jetonAl(),
): Promise<void> {
  const yanit = await fetch(`${apiAdres}${yol}`, {
    method: yontem,
    headers: {
      ...TUNEL_BASLIKLARI,
      ...(jeton ? { Authorization: `Bearer ${jeton}` } : {}),
      ...(govde !== undefined ? { 'Content-Type': 'application/json' } : {}),
    },
    body: govde !== undefined ? JSON.stringify(govde) : undefined,
  });
  if (yanit.status === 401) {
    refreshSession();
    throw new Error(`API 401: ${yol}`);
  }
  if (!yanit.ok) {
    const metin = await yanit.text().catch(() => '');
    throw new Error(problemMetni(metin) || durumMetni(yanit.status, yol));
  }
  const blob = await yanit.blob();
  const ad = dosyaAdiCoz(yanit.headers.get('Content-Disposition')) ?? 'indirilen';
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = ad;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

/** Content-Disposition'dan indirilecek dosya adını çıkarır (filename*=UTF-8'' ya da filename="..."). */
function dosyaAdiCoz(cd: string | null): string | null {
  if (!cd) return null;
  const yildizli = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(cd);
  if (yildizli) return decodeURIComponent(yildizli[1].replace(/"/g, '').trim());
  const duz = /filename="?([^";]+)"?/i.exec(cd);
  return duz ? duz[1].trim() : null;
}

/** Gövdesiz hataları insan diline çevirir: 403 bir adres değil bir DURUMDUR — kullanıcının
 * yapabileceği tek şey yetki istemek, mesaj da onu söyler. Diğerlerinde teşhis için yol kalır.
 * (Ekran testi bulgusu: müşteri elle /tasarimci yazınca ham "API 403: /api/haritalar" görüyordu.) */
function durumMetni(durum: number, yol: string): string {
  return durum === 403 ? 'Bu işlem için yetkiniz yok.' : `API ${durum}: ${yol}`;
}

/** Hata gövdesi ProblemDetails JSON'u ise insana okunur kısmını (detail/title ya da doğrulama
 * hatalarını) çıkarır; değilse ham metni bırakır — kullanıcı ham JSON görmesin. */
function problemMetni(metin: string): string {
  if (!metin.startsWith('{')) return metin;
  try {
    const p = JSON.parse(metin) as { detail?: string; title?: string; errors?: Record<string, string[]> };
    if (p.errors) return Object.values(p.errors).flat().join(' · ');
    return p.detail || p.title || metin;
  } catch {
    return metin;
  }
}
