import { useEffect, useMemo, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import Chip from '@mui/material/Chip';
import MuiAlert from '@mui/material/Alert';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemText from '@mui/material/ListItemText';
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome';
import RefreshIcon from '@mui/icons-material/Refresh';
import HistoryIcon from '@mui/icons-material/History';
import AddCommentOutlinedIcon from '@mui/icons-material/AddCommentOutlined';
import { PageHeader, bandButton, bandButtonWhite, whiteBox } from '../components/PageHeader';
import { boxRadius } from '../theme';
import { useSession } from '../identity/sessionContext';
import { Roles } from '../identity/roles';
import { soruSor, soruDurum, soruIsit, type AskQuestionResult, type NlqStatus, type DurumCipi } from '../api/soru';
import { getDataMaps, type DataMapSummary } from '../api/dataMaps';
import { getFolders, addReport, type FolderDto, type VisualizationType } from '../api/reports';
import { generateQuerySql, type QueryModel } from '../api/queries';
import { SohbetSutunu } from '../components/sorusor/SohbetSutunu';
import { SonucTuvali } from '../components/sorusor/SonucTuvali';
import { KadiRozet } from '../components/sorusor/KadiRozet';
import type { Mesaj, TakipOnerisi } from '../components/sorusor/tipler';
import { kadiDurum, kadiSoyle, kadiBasarili, kadiYukleniyor } from '../kadi/kadiKanal';

const TUR_SIRASI: VisualizationType[] = ['Tablo', 'Cubuk', 'Cizgi', 'Pasta', 'SayiKarti'];
const turCoz = (v: string): VisualizationType =>
  (TUR_SIRASI as string[]).includes(v) ? (v as VisualizationType) : 'Tablo';

/* --- Sohbet geçmişi: harita başına son 20 KONUŞMA (S30'da tek tek sorulardı). --- */
type GecmisKaydi = { soru: string; an: number; gorsellestirme: string; satir: number; ms: number };
/** Kadi'nin bekleme sahnesini kuracağı alan: sonucun çizileceği tuval. Kadi ekranın ortasında
 * değil İŞİN OLDUĞU yerde bekler; sonuç gelince evine (sağ alt) süzülüp duyurusunu orada yapar. */
const TUVAL_ALANI = '[data-kadi-alan="tuval"]';

const gecmisAnahtar = (haritaId: string) => `kds.soruGecmisi.${haritaId}`;
function gecmisOku(haritaId: string): GecmisKaydi[] {
  try {
    const r: unknown = JSON.parse(localStorage.getItem(gecmisAnahtar(haritaId)) ?? '[]');
    return Array.isArray(r) ? (r as GecmisKaydi[]) : [];
  } catch { return []; }
}
function gecmisYaz(haritaId: string, kayit: GecmisKaydi): GecmisKaydi[] {
  const liste = [kayit, ...gecmisOku(haritaId).filter((k) => k.soru !== kayit.soru)].slice(0, 20);
  localStorage.setItem(gecmisAnahtar(haritaId), JSON.stringify(liste));
  return liste;
}

let sayac = 0;
const yeniId = () => `m${++sayac}-${Date.now()}`;

/** Cevabın tek cümlelik özeti — tabloyu okumadan "ne çıktı" sorusunu cevaplar. */
function ozetCumle(cevap: AskQuestionResult): string {
  const { satirlar, kolonlar } = cevap.sonuc;
  if (satirlar.length === 0) return 'Bu soruya uyan kayıt bulamadım.';
  const sayisal = kolonlar.map((_, i) => i).filter((i) => satirlar.every((r) => r[i] === null || typeof r[i] === 'number'));
  const olcuIdx = sayisal[sayisal.length - 1];
  if (satirlar.length === 1 && olcuIdx !== undefined) {
    const d = satirlar[0][olcuIdx];
    return `${kolonlar[olcuIdx]?.ad ?? 'Sonuç'}: ${typeof d === 'number' ? d.toLocaleString('tr-TR') : String(d ?? '—')}`;
  }
  if (olcuIdx !== undefined) {
    let enB = -Infinity; let enBSatir: unknown[] | null = null;
    for (const r of satirlar) {
      const v = r[olcuIdx];
      if (typeof v === 'number' && v > enB) { enB = v; enBSatir = r; }
    }
    const boyutIdx = kolonlar.findIndex((_, i) => !sayisal.includes(i));
    if (enBSatir && boyutIdx >= 0) {
      return `${satirlar.length} satır buldum; en yüksek ${enB.toLocaleString('tr-TR')} — ${String(enBSatir[boyutIdx] ?? '—')}.`;
    }
    return `${satirlar.length} satır buldum; en yüksek ${enB.toLocaleString('tr-TR')}.`;
  }
  return `${satirlar.length} satır buldum.`;
}

/** Takip çipleri: cevabın DURUMUNA bakıp anlamlı düzeltmeler önerir (tek tıkla düzeltme —
 * yazı yazmadan). En fazla 3: araştırma bulgusu, uzun liste gürültü yapıyor. */
function takipOnerileri(cevap: AskQuestionResult): TakipOnerisi[] {
  const o: TakipOnerisi[] = [];
  const limit = cevap.model.satirLimiti;
  const kovaVar = cevap.model.alanlar.some((a) => a.kova);
  const siralamaVar = (cevap.model.siralama?.length ?? 0) > 0;

  if (limit && limit <= 10) o.push({ etiket: `İlk ${limit * 2}'ye çıkar`, metin: `ilk ${limit * 2} satırı göster` });
  else if (!limit && cevap.sonuc.satirlar.length > 12) o.push({ etiket: 'İlk 10 yeter', metin: 'ilk 10 satırı göster' });
  if (siralamaVar) o.push({ etiket: 'Ters sırala', metin: 'sıralamayı tersine çevir' });
  else o.push({ etiket: 'Büyükten küçüğe sırala', metin: 'sonucu azalan sırala' });
  if (!kovaVar) o.push({ etiket: 'Aylık kır', metin: 'sonucu aylık kırılımla göster' });
  return o.slice(0, 3);
}

/** Soru Sor (S23 · NLQ, S31 tezgâh düzeni): solda konuşma, sağda sonuç tuvali.
 *
 * Düzen kararı (kullanıcı onaylı mockup "A yönü", 23 Ağu 2026): grafik ve tablo dar sohbet
 * sütununda ÇİZİLMEZ — konuşma akarken sonuç tam boy ve KALICI durur. Gerekçe tasarım
 * araştırmasında: sohbet akışında önceki görseller yukarı kayıp gidince kullanıcı çalışma
 * alanının zihinsel haritasını kuramıyor, aktif filtre/limit mesajlara dağılıyor. */
export default function SoruSorPage() {
  const oturum = useSession();
  const raporYazabilir = oturum.roller.includes(Roles.Admin) || oturum.roller.includes(Roles.Gelistirici);

  const [durum, setDurum] = useState<NlqStatus | null>(null);
  const [haritalar, setHaritalar] = useState<DataMapSummary[]>([]);
  const [haritaId, setHaritaId] = useState('');
  const [soru, setSoru] = useState('');
  const [bekliyor, setBekliyor] = useState(false);
  const [gecenSn, setGecenSn] = useState(0);
  const [mesajlar, setMesajlar] = useState<Mesaj[]>([]);
  const [seciliId, setSeciliId] = useState<string | null>(null);
  const [hata, setHata] = useState<string | null>(null);
  const [gecmis, setGecmis] = useState<GecmisKaydi[]>([]);
  const [gecmisAcik, setGecmisAcik] = useState(false);
  const iptalRef = useRef<AbortController | null>(null);
  const konum = useLocation();
  const sonKadiAnRef = useRef(0);
  /* Sahne sızıntı koruması: sonuç daveti 6.5 sn ekranda kalıyor; kullanıcı o sırada başka
   * sayfaya geçerse Kadi merkezde takılı kalırdı (rota effect'i sahne sürerken eve dönmüyor). */
  const sahneBitirRef = useRef<(() => void) | null>(null);
  useEffect(() => () => { sahneBitirRef.current?.(); }, []);

  // Şeffaflık "büyük göster" pencereleri
  const [buyukKanit, setBuyukKanit] = useState<'istem' | 'model' | 'sql' | null>(null);
  /** "Büyük göster" penceresi SQL'i kendi çeker: tuvaldeki kopya bileşen içinde yaşıyor ve
   * pencere onu görmüyor. Aynı uç, aynı model → aynı SQL. */
  const [buyukSql, setBuyukSql] = useState<string | null>(null);

  // Rapor olarak kaydet
  const [kayitAcik, setKayitAcik] = useState(false);
  const [kayitAd, setKayitAd] = useState('');
  const [klasorler, setKlasorler] = useState<FolderDto[]>([]);
  const [klasorId, setKlasorId] = useState('');
  const [kaydediliyor, setKaydediliyor] = useState(false);
  const [kayitSonuc, setKayitSonuc] = useState<string | null>(null);

  const secili = useMemo(
    () => mesajlar.find((m) => m.id === seciliId && m.rol === 'kadi') as Extract<Mesaj, { rol: 'kadi' }> | undefined,
    [mesajlar, seciliId],
  );
  const sonCevap = useMemo(() => {
    for (let i = mesajlar.length - 1; i >= 0; i--) {
      const m = mesajlar[i];
      if (m.rol === 'kadi') return m;
    }
    return undefined;
  }, [mesajlar]);

  useEffect(() => {
    soruDurum().then(setDurum).catch(() => setDurum({ hazir: false, model: null, sebep: 'Durum alınamadı.' }));
    getDataMaps().then((h) => {
      setHaritalar(h);
      if (h.length > 0) setHaritaId((v) => v || h[0].id);
    }).catch(() => setHata('Veri haritaları yüklenemedi.'));
  }, []);

  useEffect(() => { setGecmis(haritaId ? gecmisOku(haritaId) : []); }, [haritaId]);

  /* S34 ISITMA: harita belli olur olmaz modele istem önekini işlet. Kullanıcı sorusunu yazarken
   * arka planda koştuğu için beklenmez; ilk sorunun yarım dakikası (önek işleme 49.7 sn) böylece
   * yazma süresine gizlenir. Sonuç kullanılmaz, hata yutulur — ısıtma bir iyileştirmedir.
   * Model zaten yükleniyorsa Ollama isteği sıraya alır; kullanıcı soru sorduğunda önek hazırdır. */
  useEffect(() => {
    if (!haritaId || !durum?.hazir) return;
    const iptal = new AbortController();
    void soruIsit(haritaId, iptal.signal);
    return () => iptal.abort();
  }, [haritaId, durum?.hazir]);

  useEffect(() => {
    if (!bekliyor) return;
    setGecenSn(0);
    const z = setInterval(() => setGecenSn((s) => s + 1), 1000);
    return () => clearInterval(z);
  }, [bekliyor]);

  const sor = async (metin?: string, onceki?: QueryModel) => {
    const s = (metin ?? soru).trim();
    if (!s || !haritaId || bekliyor) return;
    setSoru('');
    setHata(null);
    setKayitSonuc(null);
    setMesajlar((m) => [...m, { id: yeniId(), rol: 'ben', metin: s }]);
    setBekliyor(true);
    const iptal = new AbortController();
    iptalRef.current = iptal;
    /* Kadi ORTAYA gelir (kullanıcı kuralı, 24 Ağu 2026): "her bekleme anında Kadi ortada olsun,
     * ne yaptığını söylesin". S31'de burada merkez sahnesi kapatılmıştı — sohbetteki aşama kartı
     * yeter sanılmıştı; kullanıcı bulgusuyla geri alındı. Perde HAFİF: aşama kartı okunur kalsın. */
    const kadiBitir = kadiYukleniyor(
      onceki ? 'Düzeltiyorum — isteğini sorguya uyguluyorum…' : 'Sorgunu yazıyorum…',
      'dusun', 'hafif', TUVAL_ALANI);
    sahneBitirRef.current = kadiBitir;
    try {
      const c = await soruSor(haritaId, s, iptal.signal, onceki);
      const id = yeniId();
      setMesajlar((m) => [...m, {
        id, rol: 'kadi', ozet: ozetCumle(c), sorulan: s, cevap: c, tur: turCoz(c.oneriGorsellestirme),
        satir: c.sonuc.satirlar.length, ceviriMs: c.ceviriSureMs, sorguMs: c.sonuc.sureMs,
      }]);
      setSeciliId(id);
      if (!onceki) {
        setGecmis(gecmisYaz(haritaId, {
          soru: s, an: Date.now(), gorsellestirme: c.oneriGorsellestirme,
          satir: c.sonuc.satirlar.length, ms: c.ceviriSureMs,
        }));
      }
      /* SONUÇ SAHNESİ (kullanıcı bulgusu 24 Ağu 2026): Kadi beklerken TUVALİN ortasındadır ama
       * sonuç gelince ORADA DURMAZ — tuvali kendi gövdesiyle kapatmış olur. Sahne kapanır (Kadi
       * evine, sağ alta süzülür) ve duyuruyu ORADA yapar; sonuç aynı anda görünür hâle gelir. */
      kadiBitir({
        mesaj: onceki
          ? `Düzelttim — ${c.sonuc.satirlar.length} satır çıktı. Sonuç tuvalde.`
          : `Sorgu çalıştı, ${c.sonuc.satirlar.length} satır geldi. Sonuç tuvalde — beraber inceleyelim mi?`,
        poz: 'okey',
        sureMs: 6000,
      });
    } catch (e) {
      kadiBitir();
      if (iptal.signal.aborted) {
        kadiDurum('');
        kadiSoyle('Tamam, vazgeçtik.', 1800);
      } else {
        const mesaj = e instanceof Error ? e.message : String(e);
        setMesajlar((m) => [...m, { id: yeniId(), rol: 'hata', metin: mesaj }]);
        kadiDurum('uzgun');
        kadiSoyle('Bu soruyu çeviremedim — biraz farklı sorar mısın?', 4500);
        setTimeout(() => kadiDurum(''), 4500);
      }
    } finally {
      /* Sahne burada KAPATILMAZ: başarı yolunda Kadi ortada kalıp davet ediyor (6.5 sn ya da
       * "Tuvale bak"). finally'de kapatmak o daveti anında yutuyordu. Hata/iptal yolları sahneyi
       * kendi kapatır; sayfa terk edilirse aşağıdaki unmount koruması kapatır. */
      setBekliyor(false);
      iptalRef.current = null;
    }
  };

  // S29/4: Kadi balonundan gelen soru — harita hazır olunca bir kez otomatik sorulur
  useEffect(() => {
    const st = konum.state as { kadiSoru?: string; an?: number } | null;
    if (!st?.kadiSoru || !st.an || st.an === sonKadiAnRef.current || !haritaId) return;
    sonKadiAnRef.current = st.an;
    void sor(st.kadiSoru);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- sor kararlı değil; an+harita yeter
  }, [konum.state, haritaId]);

  /** Durum rayında çipe basınca: "bu kısıtı kaldır" düzeltmesi. Silme işlemini modele YAPTIRIR —
   * modeli elle budamak, kurtarıcının doğrulama hattını atlamak olurdu. */
  const cipKaldir = (cip: DurumCipi) => {
    if (!secili) return;
    const istek = cip.tur === 'limit' ? 'satır limitini kaldır, tümünü göster'
      : cip.tur === 'siralama' ? 'sıralamayı kaldır'
        : cip.tur === 'filtre' ? `şu filtreyi kaldır: ${cip.etiket}`
          : `şu kırılımı kaldır: ${cip.etiket}`;
    void sor(istek, secili.cevap.model);
  };

  const yeniSohbet = () => {
    setMesajlar([]);
    setSeciliId(null);
    setSoru('');
    setHata(null);
    setKayitSonuc(null);
  };

  const kaydet = async () => {
    if (!secili || !kayitAd.trim()) return;
    setKaydediliyor(true);
    try {
      const r = await addReport({
        ad: kayitAd.trim(), klasorId: klasorId || null, model: secili.cevap.model,
        gorsellestirme: secili.tur, paylasim: 'Ozel',
      });
      setKayitAcik(false);
      setKayitSonuc(`"${r.ad}" raporu kaydedildi — Raporlar ekranından açıp tasarımcıda düzeltebilirsiniz.`);
      kadiBasarili();
    } catch (e) {
      setHata(e instanceof Error ? e.message : String(e));
    } finally {
      setKaydediliyor(false);
    }
  };

  const turDegistir = (t: VisualizationType) => {
    if (!secili) return;
    setMesajlar((m) => m.map((x) => (x.id === secili.id && x.rol === 'kadi' ? { ...x, tur: t } : x)));
  };

  const hazirDegil = durum !== null && !durum.hazir;
  const gosterilen = secili ?? sonCevap;

  /* "Büyük göster" penceresi SQL'i kendi çeker (tuvaldeki kopya bileşenin içinde yaşıyor).
   * Aynı uç + aynı model → aynı SQL; pencere kapanınca temizlenir ki bayat SQL görünmesin. */
  useEffect(() => {
    if (buyukKanit !== 'sql' || !gosterilen) { setBuyukSql(null); return; }
    let iptal = false;
    generateQuerySql(gosterilen.cevap.model)
      .then((u) => { if (!iptal) setBuyukSql(u.sql); })
      .catch((e: unknown) => { if (!iptal) setBuyukSql(e instanceof Error ? e.message : 'SQL üretilemedi.'); });
    return () => { iptal = true; };
  }, [buyukKanit, gosterilen]);

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%', minHeight: 0 }}>
      <PageHeader
        ikon={<AutoAwesomeIcon />}
        baslik="Soru Sor"
        aciklama="Türkçe sor; cevap tanımlı metriklere ve veri haritasına dayanır — yerel modelde, veri dışarı çıkmadan."
      >
        <Chip
          size="small"
          data-testid="nlq-durum"
          label={durum === null ? 'Denetleniyor…' : durum.hazir ? 'Yerel model hazır' : 'Yerel modele ulaşılamadı'}
          sx={{ color: '#fff', fontWeight: 700, bgcolor: durum?.hazir ? 'rgba(46,160,103,0.85)' : 'rgba(255,255,255,0.18)' }}
        />
        {/* Aksiyon şeridi S25'ten beri İÇERİK alanında (başlık bandı global) — burada beyaz metin
            açık temada görünmez oluyordu (kullanıcı bulgusu, 24 Ağu 2026). Tema token'ları. */}
        <FormControl size="small" sx={{ minWidth: 200 }}>
          <Select value={haritaId} onChange={(e) => { setHaritaId(e.target.value); yeniSohbet(); }}
            displayEmpty
            renderValue={(v) => (
              <Box component="span" sx={{ fontWeight: 600, fontSize: 13 }}>
                {haritalar.find((h) => h.id === v)?.ad ?? 'Veri haritası seç'}
              </Box>
            )}
            sx={{ ...whiteBox, height: 34, '& .MuiSelect-select': { py: 0.6 } }}>
            {haritalar.map((h) => <MenuItem key={h.id} value={h.id}>{h.ad}</MenuItem>)}
          </Select>
        </FormControl>
        <Button variant="outlined" startIcon={<HistoryIcon />} onClick={() => setGecmisAcik(true)}
          disabled={gecmis.length === 0} sx={bandButton}>Geçmiş</Button>
        <Button variant="contained" startIcon={<AddCommentOutlinedIcon />} onClick={yeniSohbet}
          disabled={mesajlar.length === 0} sx={bandButtonWhite}>Yeni sohbet</Button>
      </PageHeader>

      <Box sx={{ px: 2, pt: 1.5 }}>
        {hazirDegil && (
          <MuiAlert severity="warning" sx={{ mb: 1.5, borderRadius: boxRadius }}
            action={<Button color="inherit" size="small" startIcon={<RefreshIcon />}
              onClick={() => { setDurum(null); soruDurum().then(setDurum).catch(() => setDurum({ hazir: false, model: null, sebep: 'Durum alınamadı.' })); }}>
              Yeniden dene
            </Button>}>
            <b>Yerel AI'ya ulaşılamadı.</b> {durum?.sebep} Ürünün geri kalanı bundan etkilenmez —
            raporlar, panolar ve uyarılar normal çalışır. Adres/model: Yönetim → Sistem Ayarları → Soru Sor.
          </MuiAlert>
        )}
        {hata && <MuiAlert severity="error" sx={{ mb: 1.5, borderRadius: boxRadius }} onClose={() => setHata(null)}>{hata}</MuiAlert>}
        {kayitSonuc && <MuiAlert severity="success" sx={{ mb: 1.5, borderRadius: boxRadius }} onClose={() => setKayitSonuc(null)}>{kayitSonuc}</MuiAlert>}
      </Box>

      {/* TEZGÂH: solda konuşma, sağda sonuç. Yan yana düzen ancak tuvale GERÇEKTEN yer kalınca
          açılır (lg = 1200px): 900px'de sohbet 360px alınca tuvale ~290px kalıyordu ve tablo
          sıkışıyordu — dar ekranda alt alta, sohbet üstte ve boyu sınırlı. */}
      <Box sx={{
        flex: 1, minHeight: 0, display: 'grid', gap: 1.5, px: 2, pb: 2,
        gridTemplateColumns: { xs: '1fr', lg: '360px 1fr' },
        gridTemplateRows: { xs: 'minmax(0, 42vh) 1fr', lg: '1fr' },
      }}>
        <SohbetSutunu
          mesajlar={mesajlar}
          seciliId={gosterilen?.id ?? null}
          onSec={setSeciliId}
          bekliyor={bekliyor}
          gecenSn={gecenSn}
          onDurdur={() => iptalRef.current?.abort()}
          soru={soru}
          onSoru={setSoru}
          onGonder={() => void sor()}
          takipler={gosterilen ? takipOnerileri(gosterilen.cevap) : []}
          onTakip={(t) => { if (gosterilen) void sor(t.metin, gosterilen.cevap.model); }}
          kilit={hazirDegil || !haritaId}
        />

        {/* Kadi'nin bekleme sahnesi bu alanın ortasına kurulur (TUVAL_ALANI): iş burada olacak,
            Kadi de burada beklemeli. Sarmalayıcı hem dolu hem boş tuvali kapsar — ikisi ayrı
            bileşen ama Kadi için aynı sahne alanıdır. */}
        <Box data-kadi-alan="tuval" sx={{ minWidth: 0, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
        {gosterilen ? (
          <SonucTuvali
            cevap={gosterilen.cevap}
            sorulan={gosterilen.sorulan}
            tur={gosterilen.tur}
            onTur={turDegistir}
            onKaydet={() => {
              setKayitAd(gosterilen.sorulan.length > 60 ? `${gosterilen.sorulan.slice(0, 57)}…` : gosterilen.sorulan);
              setKlasorId('');
              getFolders().then(setKlasorler).catch(() => setKlasorler([]));
              setKayitAcik(true);
            }}
            onKanitAc={setBuyukKanit}
            onCipKaldir={cipKaldir}
            kaydedilebilir={raporYazabilir}
          />
        ) : (
          <BosTuval haritaAd={haritalar.find((h) => h.id === haritaId)?.ad ?? ''} bekliyor={bekliyor}
            onOrnek={(s) => void sor(s)} kilit={hazirDegil || !haritaId} />
        )}
        </Box>
      </Box>

      {/* Kanıt "büyük göster" pencereleri */}
      <Dialog open={buyukKanit !== null} onClose={() => setBuyukKanit(null)} maxWidth="md" fullWidth>
        <DialogTitle sx={{ fontWeight: 800 }}>
          {buyukKanit === 'istem' ? 'Modele giden istem'
            : buyukKanit === 'sql' ? 'Veritabanında çalışan SQL'
              : 'Üretilen sorgu modeli'}
        </DialogTitle>
        <DialogContent>
          <Box component="pre" sx={{ fontSize: 12, whiteSpace: 'pre-wrap', bgcolor: 'action.hover', p: 1.5, borderRadius: 2, m: 0,
            fontFamily: buyukKanit === 'sql' ? 'ui-monospace, SFMono-Regular, Menlo, monospace' : undefined }}>
            {buyukKanit === 'istem' ? gosterilen?.cevap.gidenIstem
              : buyukKanit === 'sql' ? (buyukSql ?? 'SQL üretiliyor…')
                : JSON.stringify(gosterilen?.cevap.model, null, 2)}
          </Box>
          {buyukKanit === 'model' && (
            <Box component="pre" sx={{ fontSize: 11.5, whiteSpace: 'pre-wrap', opacity: 0.6, mt: 1, m: 0 }}>
              Modelin ham çıktısı: {gosterilen?.cevap.modelCiktisi}
            </Box>
          )}
        </DialogContent>
        <DialogActions><Button onClick={() => setBuyukKanit(null)}>Kapat</Button></DialogActions>
      </Dialog>

      {/* Rapor olarak kaydet */}
      <Dialog open={kayitAcik} onClose={() => setKayitAcik(false)} maxWidth="xs" fullWidth>
        <DialogTitle sx={{ fontWeight: 800 }}>Rapor olarak kaydet</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '8px !important' }}>
          <TextField label="Rapor adı" size="small" fullWidth value={kayitAd}
            onChange={(e) => setKayitAd(e.target.value)} slotProps={{ htmlInput: { maxLength: 150 } }} autoFocus />
          <FormControl size="small" fullWidth>
            <InputLabel>Klasör</InputLabel>
            <Select value={klasorId} label="Klasör" onChange={(e) => setKlasorId(e.target.value)}>
              <MenuItem value="">(Klasörsüz)</MenuItem>
              {klasorler.map((k) => <MenuItem key={k.id} value={k.id}>{k.ad}</MenuItem>)}
            </Select>
          </FormControl>
          <Typography sx={{ fontSize: 12, opacity: 0.65 }}>
            Kaydedilen rapor "Özel" paylaşımla ve <b>{gosterilen?.tur}</b> görünümüyle açılır; Raporlar
            ekranından tasarımcıda açıp düzeltebilirsiniz.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setKayitAcik(false)}>Vazgeç</Button>
          <Button variant="contained" onClick={() => void kaydet()} disabled={kaydediliyor || !kayitAd.trim()}>
            {kaydediliyor ? 'Kaydediliyor…' : 'Kaydet'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Sohbet geçmişi */}
      <Dialog open={gecmisAcik} onClose={() => setGecmisAcik(false)} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ fontWeight: 800 }}>Soru geçmişi</DialogTitle>
        <DialogContent sx={{ pt: '4px !important' }}>
          <Typography sx={{ fontSize: 12.5, opacity: 0.7, mb: 1 }}>
            Bu veri haritasında sorulan son sorular — yalnız bu tarayıcıda saklanır. Tıklayınca yeniden sorulur.
          </Typography>
          <List dense disablePadding>
            {gecmis.map((k) => (
              <ListItemButton key={k.an} sx={{ borderRadius: 2 }}
                onClick={() => { setGecmisAcik(false); void sor(k.soru); }}>
                <ListItemText
                  primary={k.soru}
                  secondary={`${new Date(k.an).toLocaleString('tr-TR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })} · ${k.satir} satır · çeviri ${(k.ms / 1000).toFixed(1)} sn`}
                  slotProps={{ primary: { sx: { fontSize: 13.5, fontWeight: 600 } }, secondary: { sx: { fontSize: 11.5 } } }}
                />
              </ListItemButton>
            ))}
          </List>
        </DialogContent>
        <DialogActions>
          <Button color="inherit" sx={{ mr: 'auto', opacity: 0.75 }}
            onClick={() => { localStorage.removeItem(gecmisAnahtar(haritaId)); setGecmis([]); setGecmisAcik(false); }}>
            Geçmişi temizle
          </Button>
          <Button onClick={() => setGecmisAcik(false)}>Kapat</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

/** Boş tuval: ürünün ne yapabildiğini ANLATIR. "Yönlendirmesiz boş kutu bir özellik değil,
 * yarım kalmış bir iştir" (tasarım araştırması). Örnekler haritanın adıyla bağlanır. */
function BosTuval({ haritaAd, bekliyor, onOrnek, kilit }: {
  haritaAd: string; bekliyor: boolean; onOrnek: (s: string) => void; kilit: boolean;
}) {
  const ORNEKLER = [
    'servis başına toplam talep tutarı nedir, ilk 3ü göster',
    'marka bazında talep sayısı',
    '2026 yılında aylık talep sayısı nasıl değişmiş?',
    'garanti kapsamındaki taleplerin oranı',
  ];
  return (
    <Paper variant="outlined" sx={{ borderRadius: boxRadius, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', p: 4, gap: 2.5 }}>
      {bekliyor ? (
        <IskeletTuval />
      ) : (
        <>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.75 }}>
            <Box sx={{ transform: 'scale(1.7)' }}><KadiRozet /></Box>
            <Box>
              <Typography sx={{ fontSize: 19, fontWeight: 800, letterSpacing: '-0.3px' }}>Ne öğrenmek istersin?</Typography>
              <Typography sx={{ fontSize: 13, opacity: 0.75 }}>
                Sorunu gündelik Türkçeyle yaz — sorguyu ben kurarım, sonucu birlikte rapora çeviririz.
              </Typography>
            </Box>
          </Box>

          <Box sx={{ width: '100%', maxWidth: 620 }}>
            <Typography sx={{ fontSize: 12.5, fontWeight: 700, mb: 1 }}>
              {haritaAd ? `${haritaAd} için örnekler` : 'Örnek sorular'}
            </Typography>
            <Box sx={{ display: 'flex', gap: 0.75, flexWrap: 'wrap' }}>
              {ORNEKLER.map((o) => (
                <Chip key={o} label={o} size="small" variant="outlined" disabled={kilit}
                  onClick={() => onOrnek(o)} sx={{ cursor: 'pointer', fontSize: 12 }} />
              ))}
            </Box>
          </Box>

          <Box sx={{ width: '100%', maxWidth: 620, display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr 1fr' }, gap: 1.25 }}>
            {[
              ['Tanımlı metriklere dayanır', '"İki farklı ciro" olmaz — cevap katalogdaki tanımı kullanır.'],
              ['Veri makineden çıkmaz', 'Model yerelde koşar; yalnız tablo ve kolon adlarını görür.'],
              ['Cevap kara kutu değil', 'Her cevapta nasıl hesaplandığı ve üretilen sorgu açık.'],
            ].map(([b, a]) => (
              <Paper key={b} variant="outlined" sx={{ p: 1.5, borderRadius: boxRadius }}>
                <Typography sx={{ fontSize: 12.5, fontWeight: 700, mb: 0.3 }}>{b}</Typography>
                <Typography sx={{ fontSize: 11.5, opacity: 0.75 }}>{a}</Typography>
              </Paper>
            ))}
          </Box>
        </>
      )}
    </Paper>
  );
}

/** Bekleme iskeleti: cevabın ŞEKLİNİ taklit eder — boş beyaz alan yerine ne geleceğini gösterir
 * (düzen sıçramasını da önler). */
function IskeletTuval() {
  const yukseklikler = [52, 74, 63, 88, 70, 96];
  return (
    <Box sx={{ width: '100%', maxWidth: 620, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Box sx={{ height: 13, width: '44%', bgcolor: 'var(--kds-dolgu)', borderRadius: 1 }} />
      <Box sx={{ height: 10, width: '62%', bgcolor: 'var(--kds-dolgu)', borderRadius: 1, opacity: 0.7 }} />
      <Box sx={{ display: 'flex', alignItems: 'flex-end', gap: 1.5, height: 130, mt: 0.75 }}>
        {yukseklikler.map((h, i) => (
          <Box key={i} sx={{ flex: 1, height: `${h}%`, bgcolor: 'var(--kds-dolgu)', borderRadius: '4px 4px 0 0' }} />
        ))}
      </Box>
      <Box sx={{ height: 34, bgcolor: 'var(--kds-zemin2)', border: '1px solid', borderColor: 'divider', borderRadius: boxRadius }} />
      <Typography sx={{ fontSize: 12.5, opacity: 0.7, textAlign: 'center' }}>
        Sonuç buraya gelecek — grafik türünü sonra değiştirebilirsin.
      </Typography>
    </Box>
  );
}
