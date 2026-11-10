// oxlint-disable react/only-export-components -- sabit/tip/yardımcılar küçük UI parçalarıyla birlikte yaşar
import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import MergeTypeIcon from '@mui/icons-material/MergeType';
import TuneOutlinedIcon from '@mui/icons-material/TuneOutlined';
import PlayCircleOutlineIcon from '@mui/icons-material/PlayCircleOutlined';
import type {
  AggregationType, ComparisonOp, JoinType, LogicConnector, ParametreTipi, TarihKovasi, PencereIslevi, AraToplamTuru,
} from '../../api/queries';
import type { ColumnRole } from '../../api/dataMaps';
import { smallRadius } from '../../theme';

// ---- Sorgu Tasarımcısı ortak sabitleri / tipleri / mini UI parçaları (Y4 refactor: bölüm 1) ----

export const roleColor: Record<ColumnRole, string> = { Boyut: '#2f6df6', Olcu: 'success.main', Anahtar: '#b0851f', Gizli: '#8a8f98' };
// SQL karşılığı etikette (test dönüşü #15): "Say COUNT mu?" belirsizliği kalmasın.
export const aggLabel: Record<AggregationType, string> = {
  Yok: 'Varsayılan', Topla: 'Topla (SUM)', Ortalama: 'Ortalama (AVG)', EnAz: 'En az (MIN)',
  EnCok: 'En çok (MAX)', Say: 'Say (COUNT)', BenzersizSay: 'Benzersiz say (COUNT DISTINCT)',
};
export const aggShort: Record<AggregationType, string> = { Yok: '', Topla: 'Σ', Ortalama: 'ort', EnAz: 'min', EnCok: 'max', Say: '#', BenzersizSay: '#≠' };
export const aggOrder: AggregationType[] = ['Yok', 'Topla', 'Ortalama', 'EnAz', 'EnCok', 'Say', 'BenzersizSay'];
export const opLabel: Record<ComparisonOp, string> = {
  Esit: '=', EsitDegil: '≠', Buyuk: '>', BuyukEsit: '≥', Kucuk: '<', KucukEsit: '≤',
  Icinde: 'içinde', Arasinda: 'arasında', Icerir: 'içerir', EmptyState: 'boş', DoluDegil: 'dolu',
};
export const opOrder: ComparisonOp[] = ['Esit', 'EsitDegil', 'Buyuk', 'BuyukEsit', 'Kucuk', 'KucukEsit', 'Icinde', 'Arasinda', 'Icerir', 'EmptyState', 'DoluDegil'];
export const isValueless = (op: ComparisonOp) => op === 'EmptyState' || op === 'DoluDegil';
export const uuid = () => crypto.randomUUID();
/** İki tablo Id'sini yönden bağımsız kanonik çift-anahtara indirger (join seçimi anahtarı). */
export const pairKey = (a: string, b: string) => (a <= b ? `${a}|${b}` : `${b}|${a}`);
export const dragType = 'application/kds-kolon';
/** Türkçe-duyarsız arama katlaması (test turu M3): i/ı/İ/I tek harfe indirgenir — "ilceler"
 * yazınca "Ilceler", "ıller" yazınca "Iller" bulunur. Salt `toLocaleLowerCase('tr')` I→ı
 * çevirdiğinden ("Ilceler"→"ılceler") kullanıcının "ilceler" araması boş dönüyordu. */
export const araKatla = (s: string) => s.replace(/[İIı]/g, 'i').toLowerCase();

// Y4: kova dolu ise alan bir TARİH KIRILIMIdır (yıl/ay/…) — boyuttur, toplama alamaz.
export type EditField = { anahtar: string; tabloId: string; kolon: string; etiket: string; rol: ColumnRole; toplama: AggregationType; takmaAd: string; kova?: TarihKovasi };

/** Y4 — tarih kırılımı seçenekleri. İki aile: dönem BAŞLANGICI (kronolojik; grafikte eksen olur)
 * ve dönem PARÇASI (yıldan bağımsız sayı; mevsimsellik/saatlik desen). Hafta pazartesi başlar. */
export const tarihKovalari: { deger: TarihKovasi; etiket: string; aile: 'baslangic' | 'parca' }[] = [
  { deger: 'Yil', etiket: 'Yıl (dönem başı)', aile: 'baslangic' },
  { deger: 'Ceyrek', etiket: 'Çeyrek (dönem başı)', aile: 'baslangic' },
  { deger: 'Ay', etiket: 'Ay (dönem başı)', aile: 'baslangic' },
  { deger: 'Hafta', etiket: 'Hafta (pazartesi)', aile: 'baslangic' },
  { deger: 'Gun', etiket: 'Gün (saati at)', aile: 'baslangic' },
  { deger: 'YilNo', etiket: 'Yıl no — 2026', aile: 'parca' },
  { deger: 'CeyrekNo', etiket: 'Çeyrek no — 1..4', aile: 'parca' },
  { deger: 'AyNo', etiket: 'Ay no — 1..12', aile: 'parca' },
  { deger: 'SaatNo', etiket: 'Saat — 0..23', aile: 'parca' },
];
export const kovaEtiketi = (k?: TarihKovasi) => tarihKovalari.find((x) => x.deger === k)?.etiket ?? '';
/** Tarih kırılımı yalnız tarih tipli kolonlarda anlamlıdır (backend de reddeder). */
export const tarihTipiMi = (veriTipi?: string | null) =>
  ['date', 'datetime', 'datetime2', 'smalldatetime', 'datetimeoffset'].includes((veriTipi ?? '').toLowerCase());
// paramAnahtar dolu ise değer sabit d1/d2 yerine o çalıştırma-anı PARAMETRESİNE bağlanır.
// M36: baglac — satırın BİR ÖNCEKİ satırla ilişkisi (VE/VEYA); ilk satırda yok sayılır.
export type EditFilter = { anahtar: string; tabloId: string; kolon: string; op: ComparisonOp; d1: string; d2: string; paramAnahtar?: string; baglac?: LogicConnector };
// Çalıştırma-anı parametre taslağı (tasarımcıda tanımlanır). secenekler yalnız Liste (virgülle ayrı "değer").
export type EditParametre = { anahtar: string; etiket: string; tip: ParametreTipi; zorunlu: boolean; varsayilan: string; secenekler: string };
// Y2: cikisAdi dolu ise sıra bir ÇIKIŞ KOLONUNU (hesaplanan/özet/ölçü/CASE/birleşik) işaret eder;
// boşsa alanAnahtar ile seçili bir ALANI işaret eder (eski davranış).
export type EditSort = { anahtar: string; alanAnahtar: string; azalan: boolean; cikisAdi?: string };
// İlişkili özet alan taslağı: farklı tablodan, kendi filtreli özet kolon (fan-trap'siz;
// backend türetilmiş-tablo LEFT JOIN üretir). filtreler yalnız özet tablosunun kolonlarından.
export type EditSummaryFilter = { anahtar: string; kolon: string; op: ComparisonOp; d1: string; d2: string };
export type EditSummary = {
  anahtar: string; tabloId: string; kolon: string; toplama: AggregationType;
  bagTabloId: string; takmaAd: string; filtreler: EditSummaryFilter[];
};
// Birleşik kolon taslağı: sıralı parçalar (kolon ya da sabit metin) tek çıktı kolonunda birleşir.
export type EditCompositePart = { anahtar: string; tur: 'kolon' | 'sabit'; tabloId: string; kolon: string; sabit: string };
export type EditComposite = { anahtar: string; takmaAd: string; parcalar: EditCompositePart[] };
// Koşullu ölçü taslağı: AGG(CASE WHEN koşullar THEN değer …). Kolon boş = sabit 1 (adet sayımı);
// koşul satırları ana filtre satırlarıyla aynı desendedir (VE ile birleşir).
export type EditConditional = {
  anahtar: string; toplama: AggregationType; tabloId: string; kolon: string; takmaAd: string;
  kosullar: EditFilter[];
};
// M29 CASE kolonu taslağı: dal başına koşullar (VE) + sonuç (sabit ya da kolon); varsayılan
// (ELSE) yok/sabit/kolon. Üretimde CASE WHEN … THEN … [ELSE …] END olur (aggregate YOK).
export type EditCaseDal = {
  anahtar: string; kosullar: EditFilter[];
  sonucTur: 'sabit' | 'kolon'; tabloId: string; kolon: string; sabit: string;
};
export type EditCaseAlan = {
  anahtar: string; takmaAd: string; dallar: EditCaseDal[];
  varsayilanTur: 'yok' | 'sabit' | 'kolon'; varsayilanTabloId: string; varsayilanKolon: string; varsayilanSabit: string;
};
// Join Yolları taslağı (tabloId → ayar): ebeveyn + tür + (ops.) elle koşullar (boş = haritadan).
// ebeveynTabloId dolu ise koşul sorgudaki BAŞKA bir tabloya başvurur (M5); undefined = ana ebeveyn.
export type EditJoinCondition = { anahtar: string; tabloKolon: string; ebeveynKolon: string; ebeveynTabloId?: string };
// Y13: ekKosullar JOIN'in ON'una girer (LEFT'te ana satırı elemez — WHERE'den farkı budur).
export type EditJoinSetting = {
  ebeveynTabloId: string; tur: JoinType; kosullar: EditJoinCondition[]; ekKosullar?: EditFilter[];
};
// Y1 hesaplanan kolon taslağı: SELECT'e formül ({Tablo.Kolon} yer tutucularıyla).
export type EditHesap = { anahtar: string; ifade: string; takmaAd: string };
// Y3 özet koşulu taslağı (HAVING): AGG(tablo.kolon) OP değer; baglac bir öncekiyle ilişki.
export type EditOzetKosul = {
  anahtar: string; toplama: AggregationType; tabloId: string; kolon: string;
  op: ComparisonOp; d1: string; d2: string; baglac?: LogicConnector;
};
// Y6 pencere kolonu taslağı: işlev + (gerekiyorsa) değer, bölüm (PARTITION BY), sıra (ORDER BY),
// sayı (kaydırma/dilim/pencere boyu). İfadeler Y1-Y2 dilindedir: {Tablo.Kolon} ya da [Çıkış].
export type EditPencereSira = { anahtar: string; ifade: string; azalan: boolean };
export type EditPencere = {
  anahtar: string; islev: PencereIslevi; deger: string; bolum: string[];
  sira: EditPencereSira[]; sayi: string; takmaAd: string;
};

// Y9 çapraz tablo taslağı: başlık/değer çıkış kolonu adları + sütuna dönecek DEĞERLER (satır satır
// metin kutusu yerine virgülle ayrılmış tek alan; "başlıkları getir" düğmesi doldurur).
export type EditCapraz = {
  etkin: boolean; baslikKolonu: string; degerKolonu: string; basliklar: string; toplama: AggregationType;
};
// Y10 ara toplam taslağı.
export type EditAraToplam = { etkin: boolean; tur: AraToplamTuru; etiket: string; etiketle: boolean };
// Y11 varlık koşulu taslağı: aranan tablo + var/yok + (ops.) o tablonun kolonlarıyla iç filtre.
export type EditVarlik = {
  anahtar: string; tabloId: string; yok: boolean; baglac?: LogicConnector; filtreler: EditSummaryFilter[];
};

// Y8 ara küme taslağı: dış katman kırılımları (çıkış kolonu adları) + toplamaları.
export type EditAraKumeOzet = { anahtar: string; toplama: AggregationType; cikisAdi: string; takmaAd: string };
export type EditAraKume = { etkin: boolean; gruplar: string[]; ozetler: EditAraKumeOzet[] };

// Y7 grup içi ilk N taslağı: etkin=false ise modele hiç gitmez (ekranda kapalı anahtar).
export type EditIlkN = {
  etkin: boolean; adet: string; bolum: string[]; sira: EditPencereSira[]; esitleriDeAl: boolean;
};

/** Y6 — pencere işlevleri: ne yaptıkları ekranda yazılır (kullanıcı SQL adını bilmek zorunda değil).
 * degerli: bir değer kolonu/formülü ister · siraliZorunlu: "neye göre" sorusu cevaplanmalı ·
 * sayiEtiket: dolu ise ek bir sayı alanı çıkar. */
export const pencereIslevleri: {
  deger: PencereIslevi; etiket: string; aciklama: string;
  degerli: boolean; siraZorunlu: boolean; sayiEtiket?: string; sayiVarsayilan?: string;
}[] = [
  { deger: 'SiraNo', etiket: 'Sıra no (1, 2, 3…)', aciklama: 'Her satıra sıra numarası verir (eşitler farklı numara alır).', degerli: false, siraZorunlu: true },
  { deger: 'Sira', etiket: 'Sıralama — eşitler aynı, sonraki atlar', aciklama: 'İki satır eşitse ikisi de 1 olur, sonraki 3’tür (klasik şampiyonluk sırası).', degerli: false, siraZorunlu: true },
  { deger: 'YogunSira', etiket: 'Sıralama — eşitler aynı, atlamaz', aciklama: 'İki satır eşitse ikisi de 1 olur, sonraki 2’dir.', degerli: false, siraZorunlu: true },
  { deger: 'Dilim', etiket: 'Dilim (çeyreklik vb.)', aciklama: 'Satırları eşit büyüklükte N gruba böler — ör. 4 dilim = çeyreklik segmentasyon.', degerli: false, siraZorunlu: true, sayiEtiket: 'Dilim sayısı', sayiVarsayilan: '4' },
  { deger: 'Onceki', etiket: 'Önceki dönemin değeri', aciklama: 'Sıraya göre N satır ÖNCEKİ değeri getirir — “geçen ayki ciro” gibi.', degerli: true, siraZorunlu: true, sayiEtiket: 'Kaç satır geri', sayiVarsayilan: '1' },
  { deger: 'Sonraki', etiket: 'Sonraki dönemin değeri', aciklama: 'Sıraya göre N satır SONRAKİ değeri getirir.', degerli: true, siraZorunlu: true, sayiEtiket: 'Kaç satır ileri', sayiVarsayilan: '1' },
  { deger: 'KosanToplam', etiket: 'Koşan (kümülatif) toplam', aciklama: 'Baştan bu satıra kadarki toplam — “yılbaşından bugüne”.', degerli: true, siraZorunlu: true },
  { deger: 'BolumToplami', etiket: 'Bölümün toplamı (paya oran için)', aciklama: 'Bölümün TAMAMININ toplamını her satıra yazar; hesaplanan kolonla “pay %” çıkarılır.', degerli: true, siraZorunlu: false },
  { deger: 'HareketliOrtalama', etiket: 'Hareketli ortalama', aciklama: 'Son N satırın ortalaması — dalgalanmayı yumuşatır.', degerli: true, siraZorunlu: true, sayiEtiket: 'Kaç satırlık pencere', sayiVarsayilan: '3' },
];
export const pencereTanim = (i: PencereIslevi) => pencereIslevleri.find((x) => x.deger === i)!;

// Y2 formül koşulu taslağı: sol taraf serbest formül ya da [Çıkış Kolonu] atfı; sağ taraf
// operatör + değer(ler). Nereye (WHERE/HAVING) gideceğine backend karar verir.
export type EditIfadeKosul = {
  anahtar: string; ifade: string; op: ComparisonOp; d1: string; d2: string;
  paramAnahtar?: string; baglac?: LogicConnector;
};

/** Oturum-durumu şema sürümü (O2): saklanan alanların YAPISI değişince artır — eski veri atılır. */
export const STATE_VERSION = 4; // 4: M29 caseAlanlar eklendi

export const stepDefs = [
  { ad: 'Sorgu Oluştur', ikon: <AccountTreeOutlinedIcon sx={{ fontSize: 16 }} /> },
  { ad: 'Join Yolları', ikon: <MergeTypeIcon sx={{ fontSize: 16 }} /> },
  { ad: 'Kolonlar & Ölçüler', ikon: <TuneOutlinedIcon sx={{ fontSize: 16 }} /> },
  { ad: 'Sonuç & SQL', ikon: <PlayCircleOutlineIcon sx={{ fontSize: 16 }} /> },
];

// ---- küçük UI parçaları ----

export function DialogLabel({ children }: { children: ReactNode }) {
  return <Typography sx={{ fontSize: 10.5, fontWeight: 800, textTransform: 'uppercase', letterSpacing: 0.4, color: 'text.secondary', mb: 0.5 }}>{children}</Typography>;
}

/** Bölüm başlığı: renkli ikon rozeti + başlık + kısa açıklama (adım panelleri). */
export function SectionTitle({ ikon, baslik, ek }: { ikon: ReactNode; baslik: string; ek?: string }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
      <Box sx={{ width: 26, height: 26, borderRadius: '7px', bgcolor: 'var(--kds-tealdolgu)', color: 'secondary.main', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
        {ikon}
      </Box>
      <Typography sx={{ fontSize: 13, fontWeight: 800, color: 'primary.main' }}>{baslik}</Typography>
      {ek && <Typography sx={{ fontSize: 11.5, color: 'text.disabled' }}>— {ek}</Typography>}
    </Box>
  );
}

/** Y2/Y6/Y7 ortak ifade girişi: yazılabilir metin + "＋ kolon" ({Tablo.Kolon} basar) +
 * "＋ çıkış kolonu" ([Ad] basar). MODÜL DÜZEYİNDE tanımlıdır: bileşeni bir başka bileşenin
 * GÖVDESİNDE tanımlamak her tuş vuruşunda yeniden monte edilmesine ve kutunun ODAĞI
 * KAYBETMESİNE yol açar. */
export function IfadeGirisi({ deger, yaz, ipucu, tumKolonlar, cikisKolonlari, minGenislik = 220 }: {
  deger: string;
  yaz: (v: string) => void;
  ipucu: string;
  tumKolonlar: { kolon: string; etiket: string; yerTutucu: string }[];
  cikisKolonlari: { ad: string; tur: string }[];
  minGenislik?: number;
}) {
  const ekle = (m: string) => yaz(deger.trim() ? `${deger.trimEnd()} ${m}` : m);
  return (
    <Box sx={{ display: 'flex', gap: 0.6, alignItems: 'center', flexWrap: 'wrap' }}>
      <TextField size="small" placeholder={ipucu} value={deger} onChange={(e) => yaz(e.target.value)}
        sx={{ flex: `1 1 ${minGenislik}px`, minWidth: 180, '& input': { fontFamily: 'ui-monospace, Consolas, monospace', fontSize: 12 } }} />
      <Select size="small" value="" displayEmpty sx={{ minWidth: 96, fontSize: 11.5, '& .MuiSelect-select': { py: 0.55 } }}
        title="Bir tablo kolonu ekle — metne {Tablo.Kolon} olarak girer"
        onChange={(e) => ekle(e.target.value as string)}
        renderValue={() => <Box component="span" sx={{ color: 'secondary.main', fontWeight: 600 }}>＋ kolon</Box>}>
        {tumKolonlar.map((k) => <MenuItem key={k.yerTutucu} value={k.yerTutucu} sx={{ fontSize: 12.5 }}>{k.etiket}</MenuItem>)}
      </Select>
      {cikisKolonlari.length > 0 && (
        <Select size="small" value="" displayEmpty sx={{ minWidth: 112, fontSize: 11.5, '& .MuiSelect-select': { py: 0.55 } }}
          title="Sonuçtaki bir kolona atıf — metne [Ad] olarak girer"
          onChange={(e) => ekle(`[${e.target.value as string}]`)}
          renderValue={() => <Box component="span" sx={{ color: 'var(--kds-mor)', fontWeight: 600 }}>＋ çıkış kolonu</Box>}>
          {cikisKolonlari.map((c) => (
            <MenuItem key={c.ad} value={c.ad} sx={{ fontSize: 12.5 }}>
              {c.ad} <Box component="span" sx={{ fontSize: 10.5, color: 'text.disabled', ml: 0.5 }}>({c.tur})</Box>
            </MenuItem>
          ))}
        </Select>
      )}
    </Box>
  );
}

export function InfoNote({ children }: { children: ReactNode }) {
  return <Typography sx={{ fontSize: 12 }} color="text.secondary">{children}</Typography>;
}

export function EmptyState({ children }: { children: ReactNode }) {
  return <Box sx={{ p: 2.5, textAlign: 'center' }}><Typography sx={{ fontSize: 12.5 }} color="text.secondary">{children}</Typography></Box>;
}

export function TabItem({ etkin, onClick, children }: { etkin: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <Box component="button" onClick={onClick}
      sx={{ font: 'inherit', fontSize: 12.5, fontWeight: 600, cursor: 'pointer', px: 1.75, py: 0.8, border: '1px solid', borderColor: etkin ? 'divider' : 'transparent', borderBottom: 'none', borderTopLeftRadius: smallRadius, borderTopRightRadius: smallRadius, bgcolor: etkin ? 'background.paper' : 'transparent', color: etkin ? 'primary.main' : 'text.secondary' }}>
      {children}
    </Box>
  );
}

/** Y5 — dinamik (kayan) tarih seçenekleri. Değer olarak belirteç saklanır; SQL üretilirken
 * ÇALIŞTIRMA GÜNÜNE göre çözülür — kaydedilen rapor zamanla kayar (sabit tarih yazılmaz). */
export const dinamikTarihler: { deger: string; etiket: string }[] = [
  { deger: '$bugun', etiket: 'Bugün' },
  { deger: '$bugun-1gun', etiket: 'Dün' },
  { deger: '$bugun-7gun', etiket: 'Bugün − 7 gün' },
  { deger: '$bugun-30gun', etiket: 'Bugün − 30 gün' },
  { deger: '$bugun-3ay', etiket: 'Bugün − 3 ay' },
  { deger: '$bugun-6ay', etiket: 'Bugün − 6 ay' },
  { deger: '$bugun-1yil', etiket: 'Bugün − 1 yıl' },
  { deger: '$bugun-2yil', etiket: 'Bugün − 2 yıl' },
  { deger: '$haftaBasi', etiket: 'Bu haftanın başı (Pzt)' },
  { deger: '$ayBasi', etiket: 'Bu ayın başı' },
  { deger: '$ayBasi-1ay', etiket: 'Geçen ayın başı' },
  { deger: '$yilBasi', etiket: 'Bu yılın başı' },
  { deger: '$yilBasi-1yil', etiket: 'Geçen yılın başı' },
];

/** Değer dinamik tarih belirteci mi (ekranda ham "$bugun-6ay" yerine etiketi gösterilir). */
export const dinamikTarihMi = (deger: string) => deger.startsWith('$');
export const dinamikTarihEtiketi = (deger: string) =>
  dinamikTarihler.find((d) => d.deger === deger)?.etiket ?? deger;
