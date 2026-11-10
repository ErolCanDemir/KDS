import { useCallback, useEffect, useRef, useState, useSyncExternalStore } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import {
  kadiDinle, kadiKur, kadiYukleniyorAktif, kadiSeviye, kadiSeviyeAbone, kadiNobet,
  type KadiDurum, type KadiEylem, type KadiKarartma,
} from './kadiKanal';
import { getAlerts, type AlertSummary } from '../api/alerts';
import { Kadi3D } from './Kadi3D';
import './kadi.css';

/* S28/S29 — Kadi bot yoldaş. Sağ altta nöbet tutar; ekran değişince tek cümle söyler, sayfalar
 * kadiKanal ile sahne oynatır. S29 yetenekleri: konuşkanlık seviyesi (Ayarlar), ekranın her
 * noktasına uçuş (isaret/git), eylemli balonlar, fısıltı ipuçları, uyarı nöbeti (60 sn'de bir
 * yoklar; Sakin→Tetiklendi geçişinde alarm), balondan soru sorma, ilk kullanım turu.
 * Clippy sigortası: akışı kesmez, balonlar söner, "–" ile küçültülür, seviye "kapalı" olabilir. */

const EV = { sag: 120, alt: 168 };
const MINI_ANAHTAR = 'kds.kadi';
const SELAM_ANAHTAR = 'kds.kadiSelam';
const IPUCU_GUN_ANAHTAR = 'kds.kadiIpucuGun';
const NOBET_ANAHTAR = 'kds.kadiNobetDurum';
const TUR_ANAHTAR = 'kds.kadiTur';

type EkranGirisi = { cumle: string; poz?: KadiDurum };
const EKRANLAR: Record<string, EkranGirisi> = {
  '/': { cumle: 'Hoş geldin! Bir şey mi arıyorsun? Bana tıkla, sorunu yazıver.', poz: 'selam' },
  '/soru-sor': { cumle: 'Burası benim evim — Türkçe sor, yerel modelde çeviririm; veri dışarı çıkmaz.', poz: 'selam' },
  '/panolar': { cumle: 'Panoların burada — birini açarsan kartları tazelerim.' },
  '/raporlar': { cumle: 'Raporların burada; kim neyi görür, paylaşım rozetlerinde yazar.' },
  '/metrikler': { cumle: 'Ölçüt kataloğun — bir kez tanımla, her yerde aynı kullan.', poz: 'dusun' },
  '/tasarimci': { cumle: 'Elle kurmak istemezsen bana tıkla — Türkçe söyle, modeli birlikte kuralım.', poz: 'dusun' },
  '/sql': { cumle: 'Sorgunu izliyorum — burada yalnız SELECT çalışır, içim rahat.', poz: 'okey' },
  '/veri-haritalari': { cumle: 'Tablolar ve ilişkiler burada — harita sağlamsa raporlar da sağlam olur.', poz: 'dusun' },
  '/baglantilar': { cumle: 'Test et dersen uçar giderim, sonucu ben söylerim.' },
  '/ice-alim': { cumle: 'Zamanlanmış aktarımlar bende — istersen hemen de koştururuz.', poz: 'kos' },
  '/zamanlanmis-raporlar': { cumle: 'Aboneliklerini ben koştururum — sen uyu, ben nöbetteyim.', poz: 'okey' },
  '/uyarilar': { cumle: 'Eşiklerini izliyorum; biri aşılırsa haber veririm.', poz: 'alarm' },
  '/yonetim': { cumle: 'Kullanıcılar ve roller burada yönetilir.' },
  '/yonetim/roles': { cumle: 'Yetkiler bende — kim neyi görür, buradan belirlenir.', poz: 'okey' },
  '/yonetim/satir-guvenligi': { cumle: 'Satır kuralları aktifken herkes yalnız kendi satırlarını görür.', poz: 'okey' },
  '/yonetim/ayarlar': { cumle: 'Bir ayarı değiştirirsen etkisini sana söylerim. Benim ayarlarım da burada!' },
  '/yonetim/kurulus': { cumle: 'Kuruluş bilgilerin ve planın burada.' },
  // S42: burası artık yer tutucu değil — cümle de "v2'de gelecek" demeyi bıraktı.
  '/yonetim/gomme': { cumle: 'Raporu dışarı gömüyoruz: anahtar kimin adına çalışırsa satır güvenliği de ona göre işler.', poz: 'dusun' },
  '/yonetim/denetim': { cumle: 'Her işlem kayıt altında — merak ettiğinde buradan bakarız.', poz: 'dusun' },
  '/yonetim/kullanim': { cumle: 'Neyin çok, neyin hiç kullanılmadığı burada görünür.', poz: 'dusun' },
};

/* Fısıltı ipuçları (S29/5): ekrana özel, GERÇEK özellik bilgisi. Normal seviyede günde 1,
 * konuşkanda ekran başına oturumda 1. */
const IPUCLARI: Record<string, string> = {
  '/': 'Biliyor muydun? Sağ üstteki ay/güneş ile koyu temaya geçebilirsin.',
  '/soru-sor': 'Biliyor muydun? "Modele gideni gör" ile yerel modele giden istemin tamamını okuyabilirsin.',
  '/raporlar': 'Biliyor muydun? Rapor sonucu bu tarayıcıda saklanır — ekrana dönünce beklemeden açılır.',
  '/panolar': 'Biliyor muydun? Pano kartına tıklayınca rapora iner, geri tuşuyla panoya dönersin.',
  '/metrikler': 'Biliyor muydun? "Ölç" düğmesi metriğin güncel değerini anında hesaplar.',
  '/tasarimci': 'Biliyor muydun? Kolon başlığına tıklayarak sonucu sıralayabilirsin.',
  '/sql': 'Biliyor muydun? Sorgu yavaşsa "Sorun ne?" düğmesi tanı koyar.',
  '/veri-haritalari': 'Biliyor muydun? Tablo eklerken FK ilişkileri otomatik önerilir.',
  '/baglantilar': 'Biliyor muydun? İçe Alım modu veriyi KDS deposuna kopyalar — kaynak yorulmaz.',
  '/ice-alim': 'Biliyor muydun? Aktarım geçmişindeki satıra tıklayınca ayrıntı açılır.',
  '/zamanlanmis-raporlar': 'Biliyor muydun? "Şimdi Gönder" zamanı beklemeden posta yollar.',
  '/uyarilar': 'Biliyor muydun? Uyarı yalnız durum DEĞİŞİNCE mail atar — gereksiz tekrar yok.',
  '/yonetim/roles': 'Biliyor muydun? Rol üyeliği kullanıcının SONRAKİ girişinde geçerli olur.',
  '/yonetim/ayarlar': 'Biliyor muydun? SMTP ayarlarını test mailiyle anında sınayabilirsin.',
};

/* İlk kullanım turu (S29/6): Kadi butonların YANINA uçarak ürünü tanıtır. Hedefler sayfalardaki
 * data-kadi-hedef öznitelikleriyle bulunur; bulunamayan adım atlanır. */
const TUR_ADIMLARI: { rota: string; hedef: string; cumle: string }[] = [
  { rota: '/baglantilar', hedef: 'yeni-baglanti', cumle: '1/6 · Önce buradan veri kaynağını bağlarsın — "Test Et" dersen ben denerim.' },
  { rota: '/veri-haritalari', hedef: 'yeni-harita', cumle: '2/6 · Sonra tabloları dostça adlarla haritaya alırsın; ilişkileri ben öneririm.' },
  { rota: '/metrikler', hedef: 'yeni-metrik', cumle: '3/6 · Ölçütlerini bir kez burada tanımlarsın — her rapor aynı hesabı kullanır.' },
  { rota: '/soru-sor', hedef: 'soru-sor-btn', cumle: '4/6 · Türkçe sorunu buraya yazarsın — çevirisi bende, verin dışarı çıkmaz. Cevabı raporlaştırabilirsin.' },
  { rota: '/panolar', hedef: 'yeni-pano', cumle: '5/6 · Raporlarını burada tek panoda toplarsın — ekiple de paylaşırsın.' },
  { rota: '/uyarilar', hedef: 'yeni-uyari', cumle: '6/6 · Eşiklerini kurarsın; ben 7/24 nöbet tutar, aşılınca haber veririm.' },
];

function ekranGirisi(yol: string): EkranGirisi | null {
  let enIyi: EkranGirisi | null = null;
  let enIyiUzunluk = -1;
  for (const [onek, giris] of Object.entries(EKRANLAR)) {
    const uyar = onek === '/' ? yol === '/' : yol === onek || yol.startsWith(onek + '/');
    if (uyar && onek.length > enIyiUzunluk) { enIyi = giris; enIyiUzunluk = onek.length; }
  }
  return enIyi;
}

export function KadiYoldas() {
  const kutuRef = useRef<HTMLDivElement>(null);
  const balonZamanRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [balonMetin, setBalonMetin] = useState('');
  const [balonEylemler, setBalonEylemler] = useState<KadiEylem[]>([]);
  const [balonAcik, setBalonAcik] = useState(false);
  const [balonAltta, setBalonAltta] = useState(false);
  const [durum, setDurum] = useState<KadiDurum>('');
  const [mini, setMini] = useState(() => localStorage.getItem(MINI_ANAHTAR) === 'mini');
  const [merkez, setMerkez] = useState(false);
  /* Merkez sahnesinin perdesi Kadi'nin KENDİ işi: sayfalar kadiYukleniyor'u doğrudan çağırdığında
   * da ekran kararsın (İçe Alım bulgusu, 24 Ağu 2026). Perde Kadi'nin ALTINDA, diyalogların
   * (1300) üstünde durur ki sahne her yerden görünsün. */
  const [perde, setPerde] = useState<KadiKarartma>('yok');
  const [soruModu, setSoruModu] = useState(false);
  /** Kutu ilk girişte KENDİLİĞİNDEN açıldı mı — başlık o zaman Kadi'yi de tanıtır. */
  const [ilkKutu, setIlkKutu] = useState(false);
  const [soru, setSoru] = useState('');
  const miniRef = useRef(mini);
  miniRef.current = mini;
  const konum = useLocation();
  const git = useNavigate();
  const seviye = useSyncExternalStore(kadiSeviyeAbone, kadiSeviye);
  const seviyeRef = useRef(seviye);
  seviyeRef.current = seviye;
  const turAktifRef = useRef(false);
  const durumRef = useRef(durum);
  durumRef.current = durum;
  const merkezRef = useRef(merkez);
  merkezRef.current = merkez;
  const soruModuRef = useRef(soruModu);
  soruModuRef.current = soruModu;
  /* S30 çakışma kaçınması: Kadi "ev ailesinde" mi? (ev + kaçınma konumları). Sahne/tur/işaret
   * konumları evden sayılmaz — kaçınma onları bozmasın. */
  const evdeRef = useRef(true);

  useEffect(() => kadiKur(), []);

  const soyle = useCallback((metin: string, sureMs = 3600, eylemler: KadiEylem[] = []) => {
    setBalonMetin(metin);
    setBalonEylemler(eylemler);
    setBalonAcik(true);
    if (balonZamanRef.current) clearTimeout(balonZamanRef.current);
    if (sureMs > 0) balonZamanRef.current = setTimeout(() => setBalonAcik(false), sureMs);
  }, []);

  const konumla = useCallback((x: number, y: number) => {
    const k = kutuRef.current;
    if (k) { k.style.left = `${x}px`; k.style.top = `${y}px`; }
    setBalonAltta(y < 200); // üst bölgedeyken balon ALTINA açılır — banta/tavana taşmasın
  }, []);

  /* ekranın her noktasına UÇARAK gider: yolda uçuş pozu, varınca normale döner */
  const ucusluKonumla = useCallback((x: number, y: number) => {
    setDurum('ucus');
    konumla(x, y);
    setTimeout(() => { if (durumRef.current === 'ucus') setDurum(''); }, 980);
  }, [konumla]);

  const eveDon = useCallback(() => {
    setMerkez(false);
    setPerde('yok');
    setBalonAltta(false);
    evdeRef.current = true;
    if (miniRef.current) konumla(window.innerWidth - 64, window.innerHeight - 80);
    else konumla(window.innerWidth - EV.sag, window.innerHeight - EV.alt);
  }, [konumla]);

  /** Merkez sahnesinin hedef alanı — balon açılıp kapandıkça yeniden ortalamak için saklanır. */
  const merkezAlanRef = useRef<string | undefined>(undefined);

  /** Kadi'yi hedef alanın (yoksa ekranın) ortasına yerleştirir.
   *
   * <para>Hizalama KUTUYA değil <b>GÖVDEYE</b> göredir: kutunun içinde balon üstte, gövde altta
   * durur; sabit piksel ofsetiyle hizalayınca balon ortada kalıyor ama Kadi'nin kendisi aşağı
   * kayıyordu (kullanıcı bulgusu 25 Ağu 2026 — "Kadi rapor ekranının ortasında değil"). Ofset
   * gerçek DOM'dan ölçülür; kutu içi yerleşim konumdan bağımsız olduğu için taşıma animasyonu
   * sırasında da doğrudur.</para> */
  const merkezeYerlestir = useCallback((alan?: string) => {
    const k = kutuRef.current;
    if (!k) return;

    const hedef = alan ? document.querySelector(alan) : null;
    const r = hedef?.getBoundingClientRect();
    const gecerli = r && r.width > 180 && r.height > 140
      && r.right > 0 && r.bottom > 0 && r.left < window.innerWidth && r.top < window.innerHeight;
    const merkezX = gecerli ? r.left + r.width / 2 : window.innerWidth / 2;
    const merkezY = gecerli ? r.top + r.height / 2 : window.innerHeight / 2;

    const kutuR = k.getBoundingClientRect();
    const govR = k.querySelector('.kadi-gov')?.getBoundingClientRect();
    const ofsetX = govR ? govR.left + govR.width / 2 - kutuR.left : 48;
    const ofsetY = govR ? govR.top + govR.height / 2 - kutuR.top : 118;

    konumla(
      Math.min(window.innerWidth - 120, Math.max(16, merkezX - ofsetX)),
      Math.min(window.innerHeight - 130, Math.max(8, merkezY - ofsetY)),
    );
  }, [konumla]);

  /** <paramref name="alan"/> verilirse Kadi EKRANIN değil o alanın ortasına gelir — işin
   * gerçekte olduğu yerde beklesin diye (ör. Soru Sor'da sonucun çizileceği tuval). Alan
   * bulunamazsa ya da ekran dışındaysa ekran ortasına düşülür: sahne asla kaybolmamalı. */
  const merkezeGel = useCallback((karartma: KadiKarartma = 'yogun', alan?: string) => {
    setMerkez(true);
    setPerde(karartma);
    evdeRef.current = false;
    merkezAlanRef.current = alan;
    merkezeYerlestir(alan);
  }, [merkezeYerlestir]);

  const hedefeUc = useCallback((el: HTMLElement) => {
    setMerkez(false);
    evdeRef.current = false;
    const r = el.getBoundingClientRect();
    ucusluKonumla(
      Math.min(window.innerWidth - 130, Math.max(240, r.left + r.width / 2 - 48)),
      Math.min(window.innerHeight - 170, Math.max(70, r.bottom + 12)),
    );
  }, [ucusluKonumla]);

  /* hedefin YANINA konar (tur + öneriler): sag/sol/alt/ust — kenarlarda akıllıca kırpılır */
  const isaretle = useCallback((el: HTMLElement, yer: 'sag' | 'sol' | 'alt' | 'ust') => {
    setMerkez(false);
    evdeRef.current = false;
    const r = el.getBoundingClientRect();
    let x = r.right + 14;
    let y = r.top + r.height / 2 - 64;
    if (yer === 'sol') x = r.left - 128;
    if (yer === 'alt') { x = r.left + r.width / 2 - 55; y = r.bottom + 10; }
    if (yer === 'ust') { x = r.left + r.width / 2 - 55; y = r.top - 150; }
    ucusluKonumla(
      Math.min(window.innerWidth - 130, Math.max(240, x)),
      Math.min(window.innerHeight - 170, Math.max(64, y)),
    );
  }, [ucusluKonumla]);

  /* kanal komutları */
  useEffect(() => kadiDinle((komut) => {
    switch (komut.tur) {
      case 'soyle': soyle(komut.metin, komut.sureMs ?? 3600, komut.eylemler ?? []); break;
      case 'durum': setDurum(komut.durum); break;
      case 'uc': komut.hedef ? hedefeUc(komut.hedef) : eveDon(); break;
      case 'isaret': isaretle(komut.hedef, komut.konum); break;
      case 'git': evdeRef.current = false; ucusluKonumla(komut.x, komut.y); break;
      case 'merkez': merkezeGel(komut.karartma ?? 'yogun', komut.alan); break;
      case 'sus': setBalonAcik(false); break;
    }
  }), [soyle, hedefeUc, eveDon, isaretle, ucusluKonumla, merkezeGel]);

  /* göz takibi (SVG yedeği için CSS değişkenleri; 3D beden kendi takibini yapar) */
  useEffect(() => {
    let cerceve = 0;
    const izle = (e: MouseEvent) => {
      if (cerceve) return;
      cerceve = requestAnimationFrame(() => {
        cerceve = 0;
        const k = kutuRef.current;
        if (!k) return;
        const r = k.getBoundingClientRect();
        const a = Math.atan2(e.clientY - (r.top + 44), e.clientX - (r.left + r.width / 2));
        k.style.setProperty('--kadi-px', `${(Math.cos(a) * 3).toFixed(1)}px`);
        k.style.setProperty('--kadi-py', `${Math.min(3, Math.max(-2, Math.sin(a) * 3)).toFixed(1)}px`);
      });
    };
    window.addEventListener('mousemove', izle);
    return () => { window.removeEventListener('mousemove', izle); if (cerceve) cancelAnimationFrame(cerceve); };
  }, []);

  /* ---- İLK KULLANIM TURU (S29/6) ---- */
  const turAdimRef = useRef(0);
  const turAdimIsle = useCallback((adimNo: number) => {
    if (adimNo >= TUR_ADIMLARI.length) {
      turAktifRef.current = false;
      localStorage.setItem(TUR_ANAHTAR, 'tamam');
      eveDon();
      setDurum('okey');
      soyle('Tur bitti! Takıldığın yerde bana tıklaman yeter.', 5000);
      setTimeout(() => setDurum(''), 1900);
      return;
    }
    const adim = TUR_ADIMLARI[adimNo];
    turAdimRef.current = adimNo;
    turAktifRef.current = true;
    git(adim.rota);
    /* sayfanın kendi yükleme sahnesi bitene kadar bekle — yoksa sahnenin kapanışı (sus)
     * tur balonunu yutuyordu; en fazla ~5 sn bekleyip yine de konuşur */
    const goster = (deneme: number) => {
      if (kadiYukleniyorAktif() && deneme < 20) { setTimeout(() => goster(deneme + 1), 250); return; }
      const el = document.querySelector<HTMLElement>(`[data-kadi-hedef="${adim.hedef}"]`);
      if (el) isaretle(el, 'alt'); else eveDon();
      const son = adimNo === TUR_ADIMLARI.length - 1;
      setTimeout(() => soyle(adim.cumle, 0, [
        { etiket: son ? 'Bitir' : 'Devam →', islem: () => turAdimIsle(adimNo + 1) },
        ...(son ? [] : [{ etiket: 'Turu kapat', islem: () => turAdimIsle(TUR_ADIMLARI.length) }]),
      ]), 300);
    };
    setTimeout(() => goster(0), 700);
  }, [git, isaretle, soyle, eveDon]);

  /* açılış: eve kon + ilk selam (oturumda bir kez) + tur teklifi; ekran değişince cümle+poz */
  const selamAnRef = useRef(0);
  useEffect(() => {
    if (turAktifRef.current) return; // tur kendi konuşmasını ve KONUMUNU yönetir
    /* Yükleme sahnesi SÜRERKEN eve dönme (24 Ağu 2026 bulgusu): sayfa geçişinde perde Kadi'yi
     * merkeze çağırıyor, hemen ardından bu rota effect'i koşup eveDon() ile merkezi iptal
     * ediyordu — SQL Editörü açılışında "ekran karardı ama Kadi köşeye kaçtı" tam buydu.
     * Sahne bittiğinde kadiYukleniyor'un kendi bitişi zaten eve döndürür. */
    if (!kadiYukleniyorAktif()) eveDon();
    if (seviyeRef.current === 'kapali' || seviyeRef.current === 'sessiz') return;
    if (!sessionStorage.getItem(SELAM_ANAHTAR)) {
      sessionStorage.setItem(SELAM_ANAHTAR, '1');
      selamAnRef.current = Date.now();
      setDurum('selam');
      /* İLK GİRİŞTE KUTU AÇIK GELİR (kullanıcı isteği 25 Ağu 2026): eskiden "bana tıklarsan
       * sorunu yazabilirsin" diyordu — davet, kendisi için bir tıklama daha istiyordu. Artık
       * yazma alanı hazır; tanıtım cümlesi kutunun BAŞLIĞINA taşındı (iki balon üst üste binmesin). */
      setSoruModu(true);
      setIlkKutu(true);
      const z = setTimeout(() => setDurum(''), 1900);
      // tur teklifi: selam söndükten sonra. Zamanlayıcı BİLEREK cleanup dışında —
      // StrictMode ilk mount'un cleanup'ıyla teklifi öldürüyordu; unmount koruması cb içinde.
      setTimeout(() => {
        if (!kutuRef.current) return;
        // Kutu hâlâ açıksa tur teklifi ERTELENİR: kullanıcı yazarken üstüne ikinci balon binmesin
        // (bir sonraki girişte teklif edilir — TUR_ANAHTAR yazılmadığı için hak yanmaz).
        if (soruModuRef.current) return;
        if (!localStorage.getItem(TUR_ANAHTAR) && !turAktifRef.current) {
          soyle('İstersen sana etrafı göstereyim — kısa bir tur?', 0, [
            { etiket: 'Gez 🚀', islem: () => turAdimIsle(0) },
            { etiket: 'Şimdi değil', islem: () => { localStorage.setItem(TUR_ANAHTAR, 'tamam'); setBalonAcik(false); } },
          ]);
        }
      }, 6200);
      return () => clearTimeout(z);
    }
    if (Date.now() - selamAnRef.current < 5200) return;
    const giris = ekranGirisi(konum.pathname);
    if (giris && !mini) {
      /* Sayfanın yükleme sahnesi (LoadingCurtain) rota efektinden ÖNCE başlar — cümleyi atlamak
       * yerine sahne bitene kadar ERTELE (en çok ~5 sn). Yoklama aralığı 300 → 120 ms: denetimde
       * cümlenin sahne bittikten sonra ~300 ms daha geciktiği ölçüldü (24 Ağu 2026); sık yoklama
       * o boşluğu kapatır, toplam bekleme sınırı aynı kalsın diye deneme sayısı 16 → 40. */
      let z2: ReturnType<typeof setTimeout> | null = null;
      const konus = (deneme: number) => {
        if (kadiYukleniyorAktif() && deneme < 40) { z = setTimeout(() => konus(deneme + 1), 120); return; }
        if (kadiYukleniyorAktif() || turAktifRef.current) return;
        soyle(giris.cumle);
        /* Poz temizliği sahneye DOKUNMAZ (24 Ağu 2026): ekran giriş pozunun 2.2 sn'lik
         * temizleyicisi, o arada başlayan bir işlem sahnesinin pozunu siliyordu — İçe Alım'da
         * Kadi ortaya gelip "çalışıyorum" derken koşma pozu düşüyor, nötr duruyordu. */
        if (giris.poz) { setDurum(giris.poz); z2 = setTimeout(() => { if (!kadiYukleniyorAktif()) setDurum(''); }, 2200); }
      };
      let z = setTimeout(() => konus(0), 400);
      // fısıltı ipucu: cümleden sonra — normalde günde 1, konuşkanda ekran başına oturumda 1
      const ipucu = IPUCLARI[konum.pathname];
      let z3: ReturnType<typeof setTimeout> | null = null;
      if (ipucu) {
        const bugun = new Date().toDateString();
        const konuskan = seviyeRef.current === 'konuskan';
        const oturumAnahtari = 'kds.kadiIpucu:' + konum.pathname;
        const hakVar = konuskan
          ? !sessionStorage.getItem(oturumAnahtari)
          : localStorage.getItem(IPUCU_GUN_ANAHTAR) !== bugun;
        if (hakVar) {
          z3 = setTimeout(() => {
            if (kadiYukleniyorAktif() || turAktifRef.current) return;
            if (konuskan) sessionStorage.setItem(oturumAnahtari, '1');
            else localStorage.setItem(IPUCU_GUN_ANAHTAR, bugun);
            soyle('💡 ' + ipucu, 7000);
          }, 6500);
        }
      }
      return () => { clearTimeout(z); if (z2) clearTimeout(z2); if (z3) clearTimeout(z3); };
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- yalnız rota değişimi tetiklesin
  }, [konum.pathname]);

  /* ---- UYARI NÖBETİ (S29/1): 60 sn'de bir yoklar; Sakin→Tetiklendi GEÇİŞİNDE haber verir.
   * İlk yoklamada yalnız durum kaydedilir (açılışta eski tetiklenmişler bağırmasın). ---- */
  useEffect(() => {
    if (seviye === 'kapali') return;
    let iptal = false;
    const yokla = async () => {
      try {
        const liste: AlertSummary[] = await getAlerts();
        if (iptal) return;
        const eskiHam = localStorage.getItem(NOBET_ANAHTAR);
        const eski: Record<string, string> = eskiHam ? JSON.parse(eskiHam) : {};
        const yeni: Record<string, string> = {};
        const tetiklenenler: string[] = [];
        for (const u of liste) {
          yeni[u.id] = u.durum;
          if (eskiHam && u.durum === 'Tetiklendi' && eski[u.id] && eski[u.id] !== 'Tetiklendi')
            tetiklenenler.push(u.ad);
        }
        /* DURUM YALNIZ HABER VERİLEBİLDİYSE İLERLER (24 Ağu 2026 denetim bulgusu).
         * Önce durum koşulsuz yazılıyordu; geçiş bir sayfa yüklemesine ya da tura denk gelirse
         * bağırma bastırılıyor ama geçiş TÜKETİLİYORDU — eşik aşıldığı hâlde kullanıcı hiçbir
         * zaman haber almıyordu. Bu, sunucudaki RunAlertHandler'ın "bildirim borcu kapanmadıysa
         * Tetiklendi bayrağını ilerletme" kuralının ekran tarafındaki eşidir: susmak zorunda
         * kaldıysak durumu ESKİ hâlinde bırak, bir sonraki yoklama (60 sn) yeniden denesin. */
        const susmakZorunda = kadiYukleniyorAktif() || turAktifRef.current;
        if (tetiklenenler.length === 0 || !susmakZorunda) {
          localStorage.setItem(NOBET_ANAHTAR, JSON.stringify(yeni));
        }

        if (tetiklenenler.length > 0 && !susmakZorunda) {
          const mesaj = tetiklenenler.length === 1
            ? `Eşik aşıldı: ${tetiklenenler[0]}!`
            : `${tetiklenenler.length} uyarı tetiklendi!`;
          kadiNobet(mesaj, window.location.pathname === '/uyarilar' ? [] : [{ etiket: 'Uyarılara git', rota: '/uyarilar' }]);
        }
      } catch { /* nöbet sessiz düşer — ürün işlevini etkilemez */ }
    };
    void yokla();
    const z = setInterval(() => void yokla(), 60_000);
    return () => { iptal = true; clearInterval(z); };
  }, [seviye]);

  useEffect(() => {
    const yenile = () => { if (!merkez) eveDon(); };
    window.addEventListener('resize', yenile);
    return () => window.removeEventListener('resize', yenile);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [eveDon]);
  useEffect(() => { if (!merkez) eveDon(); }, [mini]); // eslint-disable-line react-hooks/exhaustive-deps

  /* Merkez sahnesinde balon açılıp kapandıkça ya da metni değiştikçe kutunun yüksekliği değişir;
   * gövde hizası bozulmasın diye yeniden ortalanır. Sahne kurulurken balon HENÜZ açılmamış olur
   * (önce merkez, sonra kadiSoyle) — bu effect o gecikmeyi kapatır. */
  useEffect(() => {
    if (merkez) merkezeYerlestir(merkezAlanRef.current);
  }, [merkez, balonAcik, balonMetin, merkezeYerlestir]);

  /* ---- ÇAKIŞMA KAÇINMASI (S30, kullanıcı kararı): Kadi evdeyken altında tıklanabilir öğe ya da
   * metin kalıyorsa (ör. cevap kartındaki "Düzelt" düğmesi) yukarı doğru — gerekirse biraz sola —
   * İLK boş alana süzülür; alan boşalınca (scroll/içerik değişimi) evine döner. Yalnız ev
   * duruşunda çalışır: sahneler, tur, işaret konumları ve soru kutusu asla bozulmaz. ---- */
  useEffect(() => {
    const ETKILESIMLI = 'button, a, input, textarea, select, [role="button"], [role="combobox"],'
      + ' [role="checkbox"], [role="tab"], [role="menuitem"], .MuiChip-root, .MuiSlider-root';
    /* İçerik engelleri BLOK düzeyinde tutulur (tablonun kendisi — hücreleri değil): binlerce
     * td/span taramak scroll'da pahalıya patlar, tablo zaten bir bütün olarak engeldir. */
    const ICERIK = 'table, canvas, img, pre, p, h1, h2, h3, h4, h5, h6, li, .MuiAlert-root';

    /* Engel dikdörtgenleri. Nokta örneklemesi DEĞİL geometri: (a) örnek satırları arasına düşen
     * ince düğme bantları görünmez kalıyordu, (b) hit-test `pointer-events:none` olan PASİF
     * düğmeleri hiç döndürmüyordu (canlı debug: boş girdideki "Düzelt") — geometri ikisini de
     * kökten çözer. Dev konteynerler (sayfa zemini) elenir; onlar "içerik" değildir. */
    const engelleriTopla = (secici: string): DOMRect[] => {
      const kutu = kutuRef.current;
      const enBuyukAlan = window.innerWidth * window.innerHeight * 0.6;
      const liste: DOMRect[] = [];
      for (const el of document.querySelectorAll(secici)) {
        if (liste.length >= 400) break;   // sigorta: dev listelerde tarama maliyeti sabit kalsın
        if (kutu?.contains(el)) continue;
        const r = el.getBoundingClientRect();
        if (r.width < 6 || r.height < 6) continue;                         // gizli/boş
        if (r.bottom < 0 || r.top > window.innerHeight) continue;          // viewport dışı
        if (r.right < 0 || r.left > window.innerWidth) continue;
        if (r.width * r.height > enBuyukAlan) continue;                    // sayfa zemini
        liste.push(r);
      }
      return liste;
    };

    const alanDolu = (sol: number, ust: number, gen: number, yuk: number, engeller: DOMRect[]) =>
      engeller.some((r) => r.right > sol && r.left < sol + gen && r.bottom > ust && r.top < ust + yuk);

    const degerlendir = () => {
      const kutu = kutuRef.current;
      if (!kutu) return;
      if (!evdeRef.current || merkezRef.current || turAktifRef.current || soruModuRef.current
        || kadiYukleniyorAktif() || durumRef.current === 'ucus') return;
      // Diyalog/çekmece açıkken kaçınma yok: Kadi zaten perdenin ALTINDA (z-index 1250 < 1300),
      // örttüğü bir şey yok — kaçmak, perde kapanınca Kadi'yi alakasız bir yerde bırakırdı.
      if (document.querySelector('.MuiModal-root')) return;

      /* Ölçü KUTUDAN alınır, gövdeden değil: gövde poz animasyonlarıyla oynar (alarm zıplaması
       * ±18px) ve hesabı her tikte kaydırıp Kadi'yi salınıma sokardı. Kutu layout kutusudur —
       * çocuğun transform'u onu etkilemez. */
      const kutuR = kutu.getBoundingClientRect();
      if (kutuR.width === 0) return;
      const gen = kutuR.width;
      const yuk = kutuR.height;

      const evX = miniRef.current ? window.innerWidth - 64 : window.innerWidth - EV.sag;
      const evY = miniRef.current ? window.innerHeight - 80 : window.innerHeight - EV.alt;
      // Aday sırası = tercih sırası: önce ev, sonra ev sütununda yukarı, sonra bir tık solda.
      const adaylar: { x: number; y: number }[] = [{ x: evX, y: evY }];
      for (const sutun of [0, -150]) {
        for (let y = evY - 56; y >= 72; y -= 56) adaylar.push({ x: evX + sutun, y });
      }
      const tiklanabilir = engelleriTopla(ETKILESIMLI);
      const icerik = tiklanabilir.concat(engelleriTopla(ICERIK));
      /* Hesap yalnız GÖVDE alanı üzerinden yapılır; balon payı BİLEREK yok. Denendi ve geri
       * alındı (canlı tur): balonun ~160×92'lik payı eklenince 8 ekranda gereksiz kaçış oldu —
       * Kadi her ekranda başka yere konan huzursuz bir şeye dönüştü. Balon zaten geçici ve
       * pointer-events:none (tıklamayı engellemez); gövdenin düğme örtmemesi asıl mesele. */
      const bul = (engeller: DOMRect[]) =>
        adaylar.findIndex((a) => !alanDolu(a.x, a.y, gen, yuk, engeller));
      /* Yakınlık > mükemmellik: tamamen boş yer, en yakın "hiç düğme yok" yerinden en fazla
       * 3 adım (~170px) uzaktaysa tercih edilir; yoksa Kadi uzak köşede asılı kalmak yerine
       * yakında durur (yazı örtmek, düğme örtmekten iyidir). Hiçbir aday düğmesiz değilse
       * evde kalır — kaçacak anlamlı yer yok. */
      const gevsekIdx = bul(tiklanabilir);
      const katiIdx = bul(icerik);
      const hedef = gevsekIdx === -1
        ? { x: evX, y: evY }
        : adaylar[katiIdx !== -1 && katiIdx - gevsekIdx <= 3 ? katiIdx : gevsekIdx];

      const simdiX = parseFloat(kutu.style.left);
      const simdiY = parseFloat(kutu.style.top);
      if (Math.abs(hedef.x - simdiX) < 4 && Math.abs(hedef.y - simdiY) < 4) return;
      if (hedef.x === evX && hedef.y === evY) eveDon();
      else ucusluKonumla(hedef.x, hedef.y); // evdeRef true kalır — kaçınma da "ev ailesi"dir
    };

    const ilk = setTimeout(degerlendir, 900);
    const z = setInterval(degerlendir, 1300);
    let erteleme: ReturnType<typeof setTimeout> | null = null;
    const tetikle = () => {
      if (erteleme) return;
      erteleme = setTimeout(() => { erteleme = null; degerlendir(); }, 250);
    };
    window.addEventListener('scroll', tetikle, { capture: true, passive: true });
    return () => {
      clearTimeout(ilk); clearInterval(z); if (erteleme) clearTimeout(erteleme);
      window.removeEventListener('scroll', tetikle, { capture: true });
    };
  }, [eveDon, ucusluKonumla]);

  /* tık: mini ise büyüt; değilse SORU KUTUSU aç (S29/4 — balondan soru sorma) */
  const tikla = () => {
    // Sahne sürerken Kadi'ye tıklamak soru kutusunu açmaz: yükleme anlatısının üstüne
    // ikinci bir etkileşim binmesin (perde de arkayı kilitliyor).
    if (merkez) return;
    if (mini) {
      setMini(false);
      localStorage.setItem(MINI_ANAHTAR, 'acik');
      return;
    }
    if (turAktifRef.current) return;
    setSoruModu((a) => {
      if (a) setIlkKutu(false);   // kapanıyorsa tanıtım başlığı da düşer
      return !a;
    });
    setBalonAcik(false);
  };

  const soruGonder = () => {
    const s = soru.trim();
    setSoruModu(false);
    setIlkKutu(false);
    setSoru('');
    if (!s) return;
    setDurum('okey');
    soyle('Aldım! Soru Sor\'da çeviriyorum…', 2200);
    setTimeout(() => setDurum(''), 1500);
    git('/soru-sor', { state: { kadiSoru: s, an: Date.now() } });
  };

  const kucult = (e: React.MouseEvent) => {
    e.stopPropagation();
    setMini(true);
    setBalonAcik(false);
    setSoruModu(false);
    localStorage.setItem(MINI_ANAHTAR, 'mini');
  };

  const eylemCalistir = (ey: KadiEylem) => {
    setBalonAcik(false);
    if (ey.islem) ey.islem();
    else if (ey.rota) git(ey.rota);
  };

  if (seviye === 'kapali') return null;

  const etkilesimli = balonEylemler.length > 0;
  return (
    <>
    {perde !== 'yok' && <div className={`kadi-perde kadi-perde-${perde}`} aria-hidden />}
    <div
      ref={kutuRef}
      className={`kadi-kutu${mini ? ' kadi-mini' : ''}${merkez ? ' kadi-merkez' : ''}${balonAltta ? ' kadi-balon-altta' : ''}`}
      data-durum={durum || undefined}
      role="button"
      tabIndex={0}
      aria-label="Kadi — soru sormak için tıkla"
      onClick={tikla}
      onKeyDown={(e) => { if ((e.key === 'Enter' || e.key === ' ') && !soruModu) { e.preventDefault(); tikla(); } }}
    >
      <div
        className={`kadi-balon${balonAcik ? ' kadi-gorunur' : ''}${etkilesimli ? ' kadi-etkilesimli' : ''}`}
        onClick={(e) => etkilesimli && e.stopPropagation()}
      >
        <div className="kadi-balon-ad">Kadi</div>
        {balonMetin}
        {etkilesimli && (
          <div className="kadi-balon-eylemler">
            {balonEylemler.map((ey) => (
              <button key={ey.etiket} onClick={(e) => { e.stopPropagation(); eylemCalistir(ey); }}>{ey.etiket}</button>
            ))}
          </div>
        )}
      </div>
      {soruModu && !mini && (
        <div className="kadi-balon kadi-gorunur kadi-etkilesimli" onClick={(e) => e.stopPropagation()}>
          <div className="kadi-balon-ad">Kadi</div>
          {ilkKutu ? 'Ben Kadi. Ne aramıştın? Sorunu buraya yaz, Soru Sor\'da çevireyim:'
            : 'Sorunu yaz, Soru Sor\'da çevireyim:'}
          <input
            autoFocus
            className="kadi-soru-girdi"
            value={soru}
            placeholder="ör. marka bazında talep sayısı"
            onChange={(e) => setSoru(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') soruGonder();
              if (e.key === 'Escape') { setSoruModu(false); setIlkKutu(false); setSoru(''); }
            }}
          />
          <div className="kadi-balon-eylemler">
            <button onClick={(e) => { e.stopPropagation(); soruGonder(); }}>Sor</button>
            <button onClick={(e) => { e.stopPropagation(); setSoruModu(false); setIlkKutu(false); setSoru(''); }}>Vazgeç</button>
          </div>
        </div>
      )}
      {!mini && <button className="kadi-mini-btn" title="Kadi'yi küçült" onClick={kucult}>–</button>}
      <div className="kadi-gov">
        <Kadi3D durum={durum} mini={mini} />
      </div>
    </div>
    </>
  );
}
