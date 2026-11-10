import { test as teardown } from '@playwright/test';
import { API, ONEK, baslik, tazeJeton } from './apiYardimci';

// E2E süiti KALICI kayıt üretir (rapor, pano, abonelik, harita, bağlantı, içe alım işi). Bunlar
// temizlenmezse HER koşuda birikir: 19 Ağu 2026'da dev veritabanında 58 raporun 42'si, 25 panonun
// 23'ü test artığıydı — gerçek veri artıkların içinde kayboluyordu. Bu teardown, süit bittiğinde
// kendi ürettiklerini siler. Sözleşme: testler oluşturduğu her kaydı "E2E " önekiyle adlandırır;
// önek dışındaki hiçbir kayda dokunulmaz (kullanıcının verisi asla silinmez).
//
// Neden tarayıcı yok: kimlik setup'ta dosyalanan oidc kullanıcısında; access_token'ı okuyup API'yi
// doğrudan çağırmak hem hızlı hem de önyüz hatalarından bağımsız (temizlik, UI kırıksa da çalışır).

/** Silme SIRASI önemlidir — referans veren kayıt önce gider (pano→rapor, iş→bağlantı). */
const KAYNAKLAR = [
  { yol: 'api/panolar', ad: 'pano' },
  { yol: 'api/abonelikler', ad: 'abonelik' },
  { yol: 'api/uyarilar', ad: 'uyarı' },
  { yol: 'api/raporlar', ad: 'rapor' },
  { yol: 'api/metrikler', ad: 'metrik' },
  { yol: 'api/ice-alim', ad: 'içe alım işi' },
  { yol: 'api/haritalar', ad: 'veri haritası' },
  { yol: 'api/baglantilar', ad: 'bağlantı' },
];

teardown('E2E artıklarını temizle', async ({ request }) => {
  const j = await tazeJeton(request);
  if (!j) {
    console.warn('[temizlik] kimlik dosyası yok — atlanıyor (setup düşmüş olabilir).');
    return;
  }

  for (const kaynak of KAYNAKLAR) {
    const liste = await request.get(`${API}/${kaynak.yol}`, { headers: baslik(j) });
    if (!liste.ok()) {
      console.warn(`[temizlik] ${kaynak.ad} listelenemedi (${liste.status()}) — atlanıyor.`);
      continue;
    }

    const artiklar = ((await liste.json()) as { id: string; ad: string }[])
      .filter((k) => k.ad?.startsWith(ONEK));

    let silinen = 0;
    for (const artik of artiklar) {
      const yanit = await request.delete(`${API}/${kaynak.yol}/${artik.id}`, { headers: baslik(j) });
      if (yanit.ok()) silinen++;
      else console.warn(`[temizlik] "${artik.ad}" silinemedi (${yanit.status()}).`);
    }

    if (artiklar.length > 0) {
      console.log(`[temizlik] ${kaynak.ad}: ${silinen}/${artiklar.length} artık silindi.`);
    }
  }
});
