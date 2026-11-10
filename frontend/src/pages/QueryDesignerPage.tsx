import { useCallback, useEffect, useMemo, useRef, useState, type DragEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Dialog from '@mui/material/Dialog';
import DialogContent from '@mui/material/DialogContent';
import Switch from '@mui/material/Switch';
import PlaylistAddCheckIcon from '@mui/icons-material/PlaylistAddCheck'; // Y11 kaydı var/yok
import ContentCopyIcon from '@mui/icons-material/ContentCopy'; // Y14 tabloyu ikinci kez ekle
import PlaylistAddIcon from '@mui/icons-material/PlaylistAdd'; // Y15 birleşim
import FilterListIcon from '@mui/icons-material/FilterList'; // Y7 grup içi ilk N
import FormControlLabel from '@mui/material/FormControlLabel';
import Checkbox from '@mui/material/Checkbox';
import TuneOutlinedIcon from '@mui/icons-material/TuneOutlined';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import ListSubheader from '@mui/material/ListSubheader';
import CalculateOutlinedIcon from '@mui/icons-material/CalculateOutlined';
import RuleOutlinedIcon from '@mui/icons-material/RuleOutlined'; // Y2 formül filtresi
import EventRepeatIcon from '@mui/icons-material/EventRepeat';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import Divider from '@mui/material/Divider';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import AddIcon from '@mui/icons-material/Add';
import CloseIcon from '@mui/icons-material/Close';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import CodeIcon from '@mui/icons-material/Code';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import ArrowForwardIcon from '@mui/icons-material/ArrowForward';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import TableChartOutlinedIcon from '@mui/icons-material/TableChartOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined';
import ViewColumnOutlinedIcon from '@mui/icons-material/ViewColumnOutlined';
import FilterAltOutlinedIcon from '@mui/icons-material/FilterAltOutlined';
import LayersOutlinedIcon from '@mui/icons-material/LayersOutlined';
import FunctionsIcon from '@mui/icons-material/Functions';
import MergeTypeIcon from '@mui/icons-material/MergeType';
import TimelineOutlinedIcon from '@mui/icons-material/TimelineOutlined'; // Y6 pencere kolonu
import SwapVertIcon from '@mui/icons-material/SwapVert';
import SpeedIcon from '@mui/icons-material/Speed';
import SaveOutlinedIcon from '@mui/icons-material/SaveOutlined';
import RestartAltIcon from '@mui/icons-material/RestartAlt';
import DesignServicesOutlinedIcon from '@mui/icons-material/DesignServicesOutlined';
import { PageHeader, whiteBox, bandButton } from '../components/PageHeader';
import { PaylasimSecici } from '../components/PaylasimSecici';
import {
  applyNodeChanges, Background, ReactFlow,
  type Edge, type NodeChange,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { getDataMaps, getDataMap, type DataMapSummary, type DataMapDetail, type ColumnRole } from '../api/dataMaps';
import {
  generateQuerySql, runQuery, diagnoseQueryModel,
  type QueryModel, type AggregationType, type ComparisonOp, type FilterCondition, type FilterNode,
  type JoinSelection, type GroupingItem, type GeneratedQuery, type QueryRunResult,
  type JoinType, type LogicConnector, type ParametreTipi, type ParametreDegerleri, type ReportParameter,
  type TarihKovasi, type AraToplamTuru, type TableInstance,
} from '../api/queries';
import { ParametrePanel, ilkParametreDegerleri } from '../components/ParametrePanel';
import type { DataMapRelationshipDto } from '../api/dataMaps';
import {
  getReport, getReports, addReport, updateReport, getFolders, addFolder,
  type FolderDto, type VisualizationType, type ReportSharing,
} from '../api/reports';
import { ResultTable } from '../components/ResultTable';
import { SorunNeButonu, useYavasEsik } from '../components/SorunNe';
import { vizDisplay } from '../components/reportVisual';
import { LoadingCurtain, RunningIndicator } from '../components/Loading';
import { useConfirm } from '../components/ConfirmDialog';
import { boxRadius, smallRadius } from '../theme';

import {
  roleColor, aggLabel, aggShort, aggOrder, opLabel, opOrder, isValueless, uuid,
  tarihKovalari, kovaEtiketi, tarihTipiMi,
  pairKey, dragType, araKatla, STATE_VERSION, stepDefs, dinamikTarihler, dinamikTarihMi, dinamikTarihEtiketi,
  DialogLabel, SectionTitle, InfoNote, EmptyState, TabItem,
  type EditField, type EditFilter, type EditParametre, type EditSort, type EditSummary,
  type EditCompositePart, type EditComposite, type EditConditional, type EditCaseAlan, type EditOzetKosul, type EditHesap, type EditIfadeKosul, type EditPencere, type EditIlkN, type EditAraKume, type EditCapraz, type EditAraToplam, type EditVarlik, type EditJoinSetting, pencereTanim, IfadeGirisi,
} from './designer/shared';
import { dugumTurleri, kenarTurleri, type TabloNode, type JoinEdgeData } from './designer/Canvas';
import { buildTree, buildModel, joinSirasiDegistir, filtreDuzlestir } from './designer/model';
import { getMetrics, type MetricSummary } from '../api/metrics';
import {
  JoinColumnDialog, SummaryDialog, ConditionalDialog, CaseDialog, CompositeDialog, RawConditionDialog, HesapDialog, PencereDialog,
} from './designer/Dialogs';
import { kadiYukleniyor, kadiHata, kadiBasarili } from '../kadi/kadiKanal';


const paramTipEtiket: Record<ParametreTipi, string> = {
  Metin: 'Metin', Sayi: 'Sayı', Tarih: 'Tarih', TarihAralik: 'Tarih Aralığı', EvetHayir: 'Evet/Hayır', Liste: 'Liste',
};
const paramTipListe: ParametreTipi[] = ['Metin', 'Sayi', 'Tarih', 'TarihAralik', 'EvetHayir', 'Liste'];

export function QueryDesignerPage() {
  const { askConfirm, confirmDialog } = useConfirm();
  const [haritalar, setHaritalar] = useState<DataMapSummary[]>([]);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [hata, setHata] = useState<string | null>(null);
  const [haritaId, setHaritaId] = useState('');
  const [detay, setDetay] = useState<DataMapDetail | null>(null);
  const [adim, setAdim] = useState(0);

  const [alanlar, setAlanlar] = useState<EditField[]>([]);
  // Sol panel: alan arama + katlanabilir tablo grupları. acikAyar = kullanıcının elle aç/kapa
  // kararı (override); kayıt yoksa varsayılan "kanvasta kullanılıyorsa açık". Böylece kullanılan
  // tablo da elle KAPATILABİLİR (test dönüşü #1).
  const [alanArama, setAlanArama] = useState('');
  const [acikAyar, setAcikAyar] = useState<Record<string, boolean>>({});
  const [filtreler, setFiltreler] = useState<EditFilter[]>([]);
  const [parametreler, setParametreler] = useState<EditParametre[]>([]);
  const [parametreDegerleri, setParametreDegerleri] = useState<ParametreDegerleri>({});
  const [siralar, setSiralar] = useState<EditSort[]>([]);
  const [limit, setLimit] = useState('1000');
  // M17: alansız eklenen tablolar — SELECT'e girmez, yalnız JOIN'e katılır (kanvasta köprü tablosu).
  const [sadeceJoin, setSadeceJoin] = useState<string[]>([]);
  // Y3: gruplama SONRASI koşullar (HAVING) — filtre (WHERE) satır bazındadır, bu grubun özetine bakar.
  const [ozetKosullar, setOzetKosullar] = useState<EditOzetKosul[]>([]);
  // Y1: hesaplanan (formül) kolonlar — SELECT'e serbest hesap; diyalogla düzenlenir.
  const [hesaplananlar, setHesaplananlar] = useState<EditHesap[]>([]);
  // S22: modele giren katalog metrik REFERANSLARI (kimlik + görüntü adı) + haritanın kataloğu.
  const [metrikler, setMetrikler] = useState<{ metrikId: string; ad: string }[]>([]);
  const [metrikKatalogu, setMetrikKatalogu] = useState<MetricSummary[]>([]);
  const [hesapDiyalog, setHesapDiyalog] = useState<EditHesap | null>(null);
  // Y2: formül koşulları — sol taraf hesap ya da [Çıkış Kolonu] atfı; WHERE mi HAVING mi olacağına
  // backend karar verir (toplama içeriyorsa gruplama sonrası).
  const [ifadeKosullari, setIfadeKosullari] = useState<EditIfadeKosul[]>([]);
  // Y6: pencere kolonları (sıra no, önceki dönem, kümülatif, bölüm toplamı…) — diyalogla düzenlenir.
  const [pencereler, setPencereler] = useState<EditPencere[]>([]);
  const [pencereDiyalog, setPencereDiyalog] = useState<EditPencere | null>(null);
  // Y7: her bölümden ilk N satır (satır limitinin grup bazlı karşılığı).
  const [ilkN, setIlkN] = useState<EditIlkN>({ etkin: false, adet: '3', bolum: [], sira: [], esitleriDeAl: false });
  // Y8: ara küme (ikinci kat) — sonucun üstünde yeniden kırılım/özet.
  const [araKume, setAraKume] = useState<EditAraKume>({ etkin: false, gruplar: [], ozetler: [] });
  // Y9: çapraz tablo · Y10: ara toplam satırları · Y11: kaydı var/yok koşulları.
  const [capraz, setCapraz] = useState<EditCapraz>({ etkin: false, baslikKolonu: '', degerKolonu: '', basliklar: '', toplama: 'Topla' });
  const [araToplam, setAraToplam] = useState<EditAraToplam>({ etkin: false, tur: 'GenelToplam', etiket: '(Tümü)', etiketle: true });
  const [varliklar, setVarliklar] = useState<EditVarlik[]>([]);
  // Y12: yinelenen satırları ele · Y16: sayfalama (kaçıncı satırdan başla).
  const [tekille, setTekille] = useState(false);
  const [atla, setAtla] = useState("");
  // Y14: tablo örnekleri (self-join). Örnek, haritaya SANAL olarak eklenir (detay'a) — böylece
  // palet, kanvas, join yolları ve kolon listeleri onu normal bir tablo gibi görür; modele de
  // ayrıca `tabloOrnekleri` olarak gider ve backend haritayı aynı şekilde genişletir.
  const [tabloOrnekleri, setTabloOrnekleri] = useState<TableInstance[]>([]);
  // Y15: birleşim (UNION) — bu sorgunun ALTINA eklenecek KAYITLI raporlar. Kolon sayıları
  // uyuşmalı; sıralama/limit ANA sorgudan gelir ve birleşimin tamamına uygulanır.
  const [birlesimler, setBirlesimler] = useState<{ id: string; ad: string; model: QueryModel }[]>([]);
  const [birlesimTekille, setBirlesimTekille] = useState(false);
  const [raporListesi, setRaporListesi] = useState<{ id: string; ad: string }[]>([]);
  // M36: OTOMATİK satırda yalnız join TÜRÜ ezmesi. Eskiden LEFT seçmek ebeveyni de sabitliyordu
  // (satır "ayarlı" olup otomatiklikten çıkıyordu; ebeveyn ara tablo ise satır join'den DÜŞÜYORDU).
  // Artık tür ayrı tutulur: satır otomatik kalır, SQL üretilirken güncel ebeveynle birleştirilir.
  const [joinTurleri, setJoinTurleri] = useState<Record<string, JoinType>>({});
  // M18: Join Yolları tablo sırası (kullanıcı ▲▼ ile değiştirir; boş = ekleme sırası). Sıra yalnız
  // görünüm değil: otomatik ebeveyn seçiminde işleme önceliğidir (modelle backend'e de gider).
  const [tabloSirasi, setTabloSirasi] = useState<string[]>([]);
  const [dugumler, setDugumler] = useState<TabloNode[]>([]);
  // Kullanıcının NodeResizer ile elle boyutlandırdığı düğümler — yeniden kurulumda boyutları korunur;
  // diğerleri içerik değiştikçe otomatik boylanır.
  const elleBoyutlanan = useRef<Set<string>>(new Set());
  // Join seçimi: aynı tablo çifti için o kenarda hangi KOLONLAR kullanılsın (kanvasta çizgiye
  // tıklayınca açılan diyalogdan değişir). Anahtar = sıralı çift; değer = seçilen JoinSelection
  // (tam kaynak/hedef tablo+kolon). Gruplama: elle GROUP BY listesi (null = otomatik).
  const [joinSecim, setJoinSecim] = useState<Record<string, JoinSelection>>({});
  const [gruplamalar, setGruplamalar] = useState<GroupingItem[] | null>(null);
  // İlişkili özet alanlar + düzenleme diyaloğu taslağı (anahtar boş = yeni kayıt).
  const [ozetler, setOzetler] = useState<EditSummary[]>([]);
  const [ozetDiyalog, setOzetDiyalog] = useState<EditSummary | null>(null);
  // Join Yolları (Adım 2): FROM kökü ('' = otomatik, ilk alanın tablosu) + tablo başına ayar.
  const [kokTabloId, setKokTabloId] = useState('');
  const [joinAyarlari, setJoinAyarlari] = useState<Record<string, EditJoinSetting>>({});
  // Koşullu ölçüler (Adım 3'ten yönetilir) + düzenleme diyaloğu taslağı.
  const [kosullular, setKosullular] = useState<EditConditional[]>([]);
  const [kosulluDiyalog, setKosulluDiyalog] = useState<EditConditional | null>(null);
  // S16/A3: "yavaş sorgu" eşiği sunucudaki Sistem Ayarları'ndan (koda gömülü değil).
  const yavasEsik = useYavasEsik();
  // M29: satır-düzeyi CASE kolonları.
  const [caseAlanlar, setCaseAlanlar] = useState<EditCaseAlan[]>([]);
  const [caseDiyalog, setCaseDiyalog] = useState<EditCaseAlan | null>(null);
  // Birleşik kolonlar (Adım 1 sol panelinden yönetilir) + elle SQL koşulu.
  const [bilesikler, setBilesikler] = useState<EditComposite[]>([]);
  const [bilesikDiyalog, setBilesikDiyalog] = useState<EditComposite | null>(null);
  const [elleKosul, setElleKosul] = useState('');
  const [elleBaglac, setElleBaglac] = useState<LogicConnector>('Ve'); // filtrelerle birleşim
  const [elleDiyalog, setElleDiyalog] = useState<string | null>(null); // taslak metin
  // Kanvasta bir kenara tıklanınca açılan join-kolon diyaloğu (çalışma seçimini kendi içinde tutar).
  const [joinDiyalog, setJoinDiyalog] = useState<
    { cift: string; tidA: string; tidB: string; kolA: string; kolB: string } | null>(null);

  const [sekme, setSekme] = useState<'sonuc' | 'sql'>('sql');
  const [sql, setSql] = useState<GeneratedQuery | null>(null);
  const [sonuc, setSonuc] = useState<QueryRunResult | null>(null);
  const [mesgul, setMesgul] = useState<'' | 'sql' | 'calistir'>('');
  const [islemHata, setIslemHata] = useState<string | null>(null);

  // Rapor bağlamı (S10): ?rapor=id ile yüklenen mevcut rapor; Kaydet bunu günceller.
  const git = useNavigate();
  const [arananParam] = useSearchParams();
  const [raporBilgi, setRaporBilgi] = useState<{ id: string; ad: string; klasorId: string | null; paylasim: ReportSharing; gorsellestirme: VisualizationType; paylasimRolleri?: string[] | null } | null>(null);
  const [basari, setBasari] = useState<string | null>(null);
  const [kaydetAcik, setKaydetAcik] = useState(false);
  const [klasorler, setKlasorler] = useState<FolderDto[]>([]);
  const [kAd, setKAd] = useState('');
  const [kKlasor, setKKlasor] = useState('');       // '' = klasörsüz, '__yeni' = yeni klasör
  const [kYeniKlasor, setKYeniKlasor] = useState('');
  const [kPaylasim, setKPaylasim] = useState<ReportSharing>('Ozel');
  const [kPaylasimRolleri, setKPaylasimRolleri] = useState<string[]>([]);   // S27
  const [kTur, setKTur] = useState<VisualizationType>('Tablo');
  const [kaydediyor, setKaydediyor] = useState(false);

  useEffect(() => {
    (async () => {
      try { setHaritalar(await getDataMaps()); }
      catch (e) { setHata((e as Error).message); kadiHata(); }
      finally { setYukleniyor(false); }
      // Y15: birleşime eklenebilecek kayıtlı raporlar (liste boş kalırsa bölüm yalnız uyarı gösterir).
      try { setRaporListesi((await getReports()).map((r) => ({ id: r.id, ad: r.ad }))); } catch { /* liste isteğe bağlı */ }
    })();
  }, []);

  /** Sorguyu TAMAMEN iptal et (test dönüşü #2 ek isteği): onay al, saklanan oturum durumunu sil,
   * aynı haritayla tertemiz başla. */
  async function sorguyuSifirla() {
    if (!(await askConfirm('Sorgu tasarımı tamamen sıfırlansın mı? Tüm alanlar, filtreler, join ayarları ve özetler silinir.',
      { baslik: 'Sorguyu iptal et', tehlikeli: true, onayYazisi: 'Sıfırla' }))) return;
    sessionStorage.removeItem('kds.tasarimci.durum');
    void selectMap(haritaId);
  }

  async function selectMap(id: string) {
    setHaritaId(id);
    setAlanlar([]); setFiltreler([]); setParametreler([]); setSiralar([]); setSadeceJoin([]); setTabloSirasi([]); setSql(null); setSonuc(null); setIslemHata(null);
    setDugumler([]); setAdim(0);
    elleBoyutlanan.current.clear();
    setJoinSecim({}); setGruplamalar(null); setJoinDiyalog(null);
    setOzetler([]); setOzetDiyalog(null);
    setKosullular([]); setKosulluDiyalog(null);
    setCaseAlanlar([]); setCaseDiyalog(null); setOzetKosullar([]); setHesaplananlar([]); setHesapDiyalog(null); setMetrikler([]);
    setIfadeKosullari([]); setPencereler([]); setPencereDiyalog(null); setIlkN({ etkin: false, adet: "3", bolum: [], sira: [], esitleriDeAl: false }); setAraKume({ etkin: false, gruplar: [], ozetler: [] }); setCapraz({ etkin: false, baslikKolonu: "", degerKolonu: "", basliklar: "", toplama: "Topla" }); setAraToplam({ etkin: false, tur: "GenelToplam", etiket: "(Tümü)", etiketle: true }); setVarliklar([]); setTekille(false); setAtla(""); setTabloOrnekleri([]); setBirlesimler([]); setBirlesimTekille(false);
    setKokTabloId(''); setJoinAyarlari({}); setJoinTurleri({});
    setBilesikler([]); setBilesikDiyalog(null); setElleKosul(''); setElleBaglac('Ve'); setElleDiyalog(null);
    setAlanArama(''); setAcikAyar({});
    setRaporBilgi(null); setBasari(null); // harita değişti → yeni rapor bağlamı
    setDetay(null);
    if (!id) return;
    try { setDetay(await getDataMap(id)); }
    catch (e) { setIslemHata((e as Error).message); kadiHata(); }
  }

  // S22: harita seçilince o haritanın metrik kataloğu yüklenir (Σ Metrik Ekle listesi + rapor
  // yüklemede referansların ad/formül gösterimi). Katalog küçüktür; süzme istemcide.
  useEffect(() => {
    if (!haritaId) { setMetrikKatalogu([]); return; }
    let iptal = false;
    getMetrics()
      .then((l) => { if (!iptal) setMetrikKatalogu(l.filter((m) => m.veriHaritasiId === haritaId)); })
      .catch(() => { if (!iptal) setMetrikKatalogu([]); });
    return () => { iptal = true; };
  }, [haritaId]);

  // Rapor yüklemesinde referanslar ad'sız gelir (modelde yalnız kimlik var) — katalog gelince tamamla.
  useEffect(() => {
    if (metrikKatalogu.length === 0 || metrikler.every((m) => m.ad !== '')) return;
    setMetrikler((p) => p.map((m) => m.ad === ''
      ? { ...m, ad: metrikKatalogu.find((k) => k.id === m.metrikId)?.ad ?? '' }
      : m));
  }, [metrikKatalogu, metrikler]);

  // ?rapor=id → raporu yükle: modelden tasarımcı durumunu geri kur (düzenleme akışı).
  useEffect(() => {
    const rid = arananParam.get('rapor');
    if (!rid) return;
    (async () => {
      try {
        const r = await getReport(rid);
        if (r.model === null || r.haritaId === null) {
          // SQL raporu tasarımcıda değil SQL Editörü'nde düzenlenir.
          git(`/sql?rapor=${r.id}`, { replace: true });
          return;
        }
        const d0 = await getDataMap(r.haritaId);
        setHaritaId(r.haritaId);
        // Y14: rapor tablo ÖRNEĞİ kullanıyorsa harita, alanlar çözülmeden ÖNCE genişletilmeli —
        // yoksa örneğe ait kolonlar "haritada yok" gibi görünür (etiketler/kanvas boş kalır).
        const ornekler = r.model?.tabloOrnekleri ?? [];
        const d = ornekler.length === 0 ? d0 : {
          ...d0,
          tablolar: [
            ...d0.tablolar,
            ...ornekler.flatMap((o) => {
              const kaynak = d0.tablolar.find((t) => t.id === o.kaynakTabloId);
              return kaynak ? [{ ...kaynak, id: o.ornekId, dostcaAd: o.etiket || `${kaynak.dostcaAd || kaynak.hamAd} (2)` }] : [];
            }),
          ],
          iliskiler: [
            ...d0.iliskiler,
            ...ornekler.flatMap((o) => [
              ...d0.iliskiler.filter((i) => i.kaynakTabloId === o.kaynakTabloId).map((i) => ({ ...i, kaynakTabloId: o.ornekId })),
              ...d0.iliskiler.filter((i) => i.hedefTabloId === o.kaynakTabloId).map((i) => ({ ...i, hedefTabloId: o.ornekId })),
            ]),
          ],
        };
        setDetay(d);
        setRaporBilgi({ id: r.id, ad: r.ad, klasorId: r.klasorId, paylasim: r.paylasim, gorsellestirme: r.gorsellestirme, paylasimRolleri: r.paylasimRolleri ?? null });

        const findColumn = (tabloId: string, kolon: string) => {
          const t = d.tablolar.find((x) => x.id === tabloId);
          const k = t?.kolonlar.find((x) => x.hamAd === kolon);
          return { etiket: k?.dostcaAd || kolon, rol: (k?.rol ?? 'Boyut') as ColumnRole };
        };
        const model = r.model;
        const yeniAlanlar: EditField[] = model.alanlar.map((a) => {
          const kb2 = findColumn(a.tabloId, a.kolon);
          // Y4: tarih kırılımı raporla birlikte geri gelir.
          return { anahtar: uuid(), tabloId: a.tabloId, kolon: a.kolon, etiket: kb2.etiket, rol: kb2.rol, toplama: a.toplama, takmaAd: a.takmaAd ?? '', kova: a.kova ?? undefined };
        });
        const duzKosullar = (f: FilterNode | null): FilterCondition[] =>
          !f ? [] : f.tur === 'kosul' ? [f] : f.cocuklar.flatMap(duzKosullar);
        const paramRef = (v: unknown): string | undefined =>
          v != null && typeof v === 'object' && '$param' in v ? String((v as { $param: unknown }).$param) : undefined;
        // M36: bağlaçlar (VE/VEYA) da geri gelir — düz koşul listesi yerine ağacı satırlara açan
        // filtreDuzlestir kullanılır (eski düz gruplar da desteklenir: hepsi VE).
        const yeniFiltreler: EditFilter[] = filtreDuzlestir(model.filtre).map(({ kosul: k, baglac }) => {
          const pa = paramRef(k.degerler[0]);
          return {
            anahtar: uuid(), tabloId: k.tabloId, kolon: k.kolon, op: k.op, paramAnahtar: pa, baglac,
            d1: pa ? '' : (k.op === 'Icinde' ? k.degerler.map(String).join(', ') : String(k.degerler[0] ?? '')),
            d2: pa ? '' : (k.op === 'Arasinda' ? String(k.degerler[1] ?? '') : ''),
          };
        });
        const yeniSiralar: EditSort[] = model.siralama.flatMap((s) => {
          // Y2: çıkış adına göre sıra (hesaplanan/özet/ölçü kolonu) alan listesinde aranmaz.
          if (s.cikisAdi) return [{ anahtar: uuid(), alanAnahtar: '', azalan: s.azalan, cikisAdi: s.cikisAdi }];
          // Y4: kırılım da eşleşmeye girer (aynı kolonun yıl/ay kırılımları ayrı kolonlardır).
          const a = yeniAlanlar.find((x) => x.tabloId === s.tabloId && x.kolon === s.kolon && x.toplama === s.toplama
            && (x.kova ?? null) === (s.kova ?? null));
          return a ? [{ anahtar: uuid(), alanAnahtar: a.anahtar, azalan: s.azalan }] : [];
        });
        setAlanlar(yeniAlanlar);
        setFiltreler(yeniFiltreler);
        setSadeceJoin(model.ekTablolar ?? []); // M17: alansız tablolar raporla birlikte geri gelir
        setTabloSirasi(model.tabloSirasi ?? []); // M18: kullanıcı sırası raporla geri gelir
        setParametreler((model.parametreler ?? []).map((p): EditParametre => ({
          anahtar: p.anahtar, etiket: p.etiket, tip: p.tip, zorunlu: p.zorunlu ?? false,
          varsayilan: p.varsayilan == null ? '' : String(p.varsayilan),
          secenekler: (p.secenekler ?? []).map((s) => String(s.deger)).join(', '),
        })));
        setSiralar(yeniSiralar);
        setLimit(model.satirLimiti != null ? String(model.satirLimiti) : '');
        // join seçimleri + elle gruplama + özet alanlar da geri yüklenir (kayıtlı raporda varsa)
        setJoinSecim(Object.fromEntries((model.joinler ?? []).map((j) =>
          [pairKey(j.kaynakTabloId, j.hedefTabloId), j])));
        setGruplamalar(model.gruplamalar ?? null);
        setOzetler((model.ozetAlanlar ?? []).map((o) => ({
          anahtar: uuid(), tabloId: o.tabloId, kolon: o.kolon, toplama: o.toplama,
          bagTabloId: o.bagTabloId, takmaAd: o.takmaAd ?? '',
          filtreler: duzKosullar(o.filtre ?? null).map((k) => ({
            anahtar: uuid(), kolon: k.kolon, op: k.op,
            d1: k.op === 'Icinde' ? k.degerler.map(String).join(', ') : String(k.degerler[0] ?? ''),
            d2: k.op === 'Arasinda' ? String(k.degerler[1] ?? '') : '',
          })),
        })));
        setBilesikler((model.bilesikAlanlar ?? []).map((b) => ({
          anahtar: uuid(), takmaAd: b.takmaAd ?? '',
          parcalar: b.parcalar.map((p): EditCompositePart => p.sabit != null
            ? { anahtar: uuid(), tur: 'sabit', tabloId: '', kolon: '', sabit: p.sabit }
            : { anahtar: uuid(), tur: 'kolon', tabloId: p.tabloId ?? '', kolon: p.kolon ?? '', sabit: '' }),
        })));
        setElleKosul(model.elleKosul ?? '');
        setElleBaglac(model.elleKosulBaglaci ?? 'Ve');
        setKokTabloId(model.kokTabloId ?? '');
        setJoinAyarlari(Object.fromEntries((model.joinAyarlari ?? []).map((a) => [a.tabloId, {
          ebeveynTabloId: a.ebeveynTabloId, tur: a.tur,
          // M36: koşulun HEDEF TABLOSU (M5) yüklemede DÜŞÜYORDU — koşul ana ebeveyne kayıyor,
          // kolon listesi o tablonunkine dönünce seçili kolon boşalıyor ve join koşulu sessizce
          // kayboluyordu ("tasarımcıda aç" sonrası 'kolon yok' hatası).
          kosullar: (a.kosullar ?? []).map((k) => ({
            anahtar: uuid(), tabloKolon: k.tabloKolon, ebeveynKolon: k.ebeveynKolon,
            ebeveynTabloId: k.ebeveynTabloId ?? undefined,
          })),
        } satisfies EditJoinSetting])));
        setKosullular((model.kosulluOlculer ?? []).map((ko) => ({
          anahtar: uuid(), toplama: ko.toplama, tabloId: ko.tabloId ?? '', kolon: ko.kolon ?? '',
          takmaAd: ko.takmaAd ?? '',
          kosullar: duzKosullar(ko.filtre).map((k) => ({
            anahtar: uuid(), tabloId: k.tabloId, kolon: k.kolon, op: k.op,
            d1: k.op === 'Icinde' ? k.degerler.map(String).join(', ') : String(k.degerler[0] ?? ''),
            d2: k.op === 'Arasinda' ? String(k.degerler[1] ?? '') : '',
          })),
        })));
        // Y1: hesaplanan (formül) kolonlar raporla birlikte geri gelir.
        setMetrikler((model.metrikAlanlar ?? []).map((mm) => ({ metrikId: mm.metrikId, ad: '' })));
        setHesaplananlar((model.hesaplananAlanlar ?? []).map((h) => ({
          anahtar: uuid(), ifade: h.ifade, takmaAd: h.takmaAd ?? '',
        })));
        // Y12/Y14/Y15/Y16 raporla birlikte geri gelir. (E2E "kaydet→tekrar aç" turu bunların
        // yükleme yolunda DÜŞTÜĞÜNÜ yakaladı — M36 ile aynı sınıf hata: modelde var, ekranda yok.)
        setTekille(model.tekille ?? false);
        setAtla(model.atla == null ? '' : String(model.atla));
        setTabloOrnekleri([...(model.tabloOrnekleri ?? [])]);
        setBirlesimTekille(model.birlesimTekille ?? false);
        // Birleşim dalları modelde SORGU olarak durur (rapor kimliği taşımaz) — sıra numarasıyla adlandırılır.
        setBirlesimler((model.birlesim ?? []).map((m, i) => ({ id: '', ad: `${i + 2}. sorgu`, model: m })));
        // Y9/Y10/Y11 raporla birlikte geri gelir (yoksa kapalı başlar).
        setCapraz(model.capraz
          ? {
              etkin: true, baslikKolonu: model.capraz.baslikKolonu, degerKolonu: model.capraz.degerKolonu,
              basliklar: (model.capraz.basliklar ?? []).join(', '), toplama: model.capraz.toplama,
            }
          : { etkin: false, baslikKolonu: '', degerKolonu: '', basliklar: '', toplama: 'Topla' });
        setAraToplam(model.araToplam
          ? { etkin: true, tur: model.araToplam.tur, etiket: model.araToplam.etiket, etiketle: model.araToplam.etiketle }
          : { etkin: false, tur: 'GenelToplam', etiket: '(Tümü)', etiketle: true });
        setVarliklar((model.varlikKosullari ?? []).map((v) => ({
          anahtar: uuid(), tabloId: v.tabloId, yok: v.yok ?? false, baglac: v.baglac ?? 'Ve',
          filtreler: duzKosullar(v.filtre ?? null).map((k) => ({
            anahtar: uuid(), kolon: k.kolon, op: k.op,
            d1: k.op === 'Icinde' ? k.degerler.map(String).join(', ') : String(k.degerler[0] ?? ''),
            d2: k.op === 'Arasinda' ? String(k.degerler[1] ?? '') : '',
          })),
        })));
        // Y8: ara küme raporla birlikte geri gelir (yoksa kapalı başlar).
        setAraKume(model.araKumeGruplari?.length || model.araKumeOzeti?.length
          ? {
              etkin: true, gruplar: [...(model.araKumeGruplari ?? [])],
              ozetler: (model.araKumeOzeti ?? []).map((o) => ({
                anahtar: uuid(), toplama: o.toplama, cikisAdi: o.cikisAdi, takmaAd: o.takmaAd ?? '',
              })),
            }
          : { etkin: false, gruplar: [], ozetler: [] });
        // Y7: grup içi ilk N raporla birlikte geri gelir (yoksa kapalı başlar).
        setIlkN(model.grupIciIlkN
          ? {
              etkin: true, adet: String(model.grupIciIlkN.adet), bolum: [...(model.grupIciIlkN.bolum ?? [])],
              sira: (model.grupIciIlkN.sira ?? []).map((s) => ({ anahtar: uuid(), ifade: s.ifade, azalan: s.azalan ?? false })),
              esitleriDeAl: model.grupIciIlkN.esitleriDeAl ?? false,
            }
          : { etkin: false, adet: '3', bolum: [], sira: [], esitleriDeAl: false });
        // Y6: pencere kolonları raporla birlikte geri gelir.
        setPencereler((model.pencereKolonlari ?? []).map((p) => ({
          anahtar: uuid(), islev: p.islev, deger: p.deger ?? '', bolum: [...(p.bolum ?? [])],
          sira: (p.sira ?? []).map((s) => ({ anahtar: uuid(), ifade: s.ifade, azalan: s.azalan ?? false })),
          sayi: p.sayi == null ? '' : String(p.sayi), takmaAd: p.takmaAd ?? '',
        })));
        // Y2: formül koşulları raporla birlikte geri gelir (değer parametreye bağlı olabilir).
        setIfadeKosullari((model.ifadeKosullari ?? []).map((k) => {
          const pa = paramRef(k.degerler[0]);
          return {
            anahtar: uuid(), ifade: k.ifade, op: k.op, paramAnahtar: pa, baglac: k.baglac ?? 'Ve',
            d1: pa ? '' : (k.op === 'Icinde' ? k.degerler.map(String).join(', ') : String(k.degerler[0] ?? '')),
            d2: pa ? '' : (k.op === 'Arasinda' ? String(k.degerler[1] ?? '') : ''),
          };
        }));
        // Y3: özet koşulları (HAVING) raporla birlikte geri gelir.
        setOzetKosullar((model.ozetKosullar ?? []).map((k) => ({
          anahtar: uuid(), toplama: k.toplama, tabloId: k.tabloId, kolon: k.kolon, op: k.op,
          baglac: k.baglac ?? 'Ve',
          d1: k.op === 'Icinde' ? k.degerler.map(String).join(', ') : String(k.degerler[0] ?? ''),
          d2: k.op === 'Arasinda' ? String(k.degerler[1] ?? '') : '',
        })));
        setCaseAlanlar((model.caseAlanlar ?? []).map((c) => ({
          anahtar: uuid(), takmaAd: c.takmaAd ?? '',
          dallar: c.dallar.map((d) => ({
            anahtar: uuid(),
            sonucTur: d.sabit != null ? 'sabit' as const : 'kolon' as const,
            tabloId: d.tabloId ?? '', kolon: d.kolon ?? '', sabit: d.sabit ?? '',
            kosullar: duzKosullar(d.filtre).map((k) => ({
              anahtar: uuid(), tabloId: k.tabloId, kolon: k.kolon, op: k.op,
              d1: k.op === 'Icinde' ? k.degerler.map(String).join(', ') : String(k.degerler[0] ?? ''),
              d2: k.op === 'Arasinda' ? String(k.degerler[1] ?? '') : '',
            })),
          })),
          varsayilanTur: c.varsayilanSabit != null ? 'sabit' as const
            : c.varsayilanKolon ? 'kolon' as const : 'yok' as const,
          varsayilanTabloId: c.varsayilanTabloId ?? '', varsayilanKolon: c.varsayilanKolon ?? '',
          varsayilanSabit: c.varsayilanSabit ?? '',
        })));
      } catch (e) { setIslemHata((e as Error).message); kadiHata(); }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- yalnız açılışta, paramdan bir kez
  }, []);

  // Tasarım durumu oturumda saklanır (test dönüşü #2): başka ekrana gidip dönünce kaldığın
  // yerden devam. ?rapor=id ile açılışta rapor yüklemesi önceliklidir (saklama yine çalışır).
  const durumHazir = useRef(false);
  useEffect(() => {
    if (arananParam.get('rapor')) { durumHazir.current = true; return; }
    const ham = sessionStorage.getItem('kds.tasarimci.durum');
    if (!ham) { durumHazir.current = true; return; }
    (async () => {
      try {
        // Kod incelemesi O2: şema SÜRÜM damgası — alan tipleri değişince (geçmişte joinSecim
        // string→obje oldu) eski oturum verisi sessiz bozulma yerine atılır.
        if ((JSON.parse(ham) as { surum?: number }).surum !== STATE_VERSION) {
          sessionStorage.removeItem('kds.tasarimci.durum');
          return;
        }
        const d = JSON.parse(ham) as {
          haritaId: string; adim: number; alanlar: EditField[]; filtreler: EditFilter[];
          siralar: EditSort[]; limit: string; joinSecim: Record<string, JoinSelection>;
          gruplamalar: GroupingItem[] | null; ozetler: EditSummary[]; bilesikler: EditComposite[];
          elleKosul: string; elleBaglac?: LogicConnector;
          kokTabloId?: string; joinAyarlari?: Record<string, EditJoinSetting>;
          joinTurleri?: Record<string, JoinType>;
          kosullular?: EditConditional[]; caseAlanlar?: EditCaseAlan[]; ozetKosullar?: EditOzetKosul[];
          hesaplananlar?: EditHesap[]; metrikler?: { metrikId: string; ad: string }[]; ifadeKosullari?: EditIfadeKosul[]; pencereler?: EditPencere[]; ilkN?: EditIlkN; araKume?: EditAraKume; capraz?: EditCapraz; araToplam?: EditAraToplam; varliklar?: EditVarlik[]; tekille?: boolean; atla?: string; tabloOrnekleri?: TableInstance[]; birlesimler?: { id: string; ad: string; model: QueryModel }[]; birlesimTekille?: boolean;
          parametreler?: EditParametre[];
          sadeceJoin?: string[]; tabloSirasi?: string[];
          raporBilgi: typeof raporBilgi;
        };
        if (!d.haritaId) return;
        const det = await getDataMap(d.haritaId);
        setHaritaId(d.haritaId); setDetay(det); setAdim(d.adim ?? 0);
        setAlanlar(d.alanlar ?? []); setFiltreler(d.filtreler ?? []); setParametreler(d.parametreler ?? []); setSiralar(d.siralar ?? []);
        setLimit(d.limit ?? '1000'); setJoinSecim(d.joinSecim ?? {});
        setGruplamalar(d.gruplamalar ?? null); setOzetler(d.ozetler ?? []);
        setBilesikler(d.bilesikler ?? []); setElleKosul(d.elleKosul ?? ''); setElleBaglac(d.elleBaglac ?? 'Ve');
        setKokTabloId(d.kokTabloId ?? ''); setJoinAyarlari(d.joinAyarlari ?? {}); setJoinTurleri(d.joinTurleri ?? {});
        setKosullular(d.kosullular ?? []);
        setCaseAlanlar(d.caseAlanlar ?? []); setOzetKosullar(d.ozetKosullar ?? []); setHesaplananlar(d.hesaplananlar ?? []); setMetrikler(d.metrikler ?? []);
        setIfadeKosullari(d.ifadeKosullari ?? []); setPencereler(d.pencereler ?? []); if (d.ilkN) setIlkN(d.ilkN); if (d.araKume) setAraKume(d.araKume); if (d.capraz) setCapraz(d.capraz); if (d.araToplam) setAraToplam(d.araToplam); setVarliklar(d.varliklar ?? []); setTekille(d.tekille ?? false); setAtla(d.atla ?? ""); setTabloOrnekleri(d.tabloOrnekleri ?? []); setBirlesimler(d.birlesimler ?? []); setBirlesimTekille(d.birlesimTekille ?? false);
        setSadeceJoin(d.sadeceJoin ?? []);
        setTabloSirasi(d.tabloSirasi ?? []);
        setRaporBilgi(d.raporBilgi ?? null);
      } catch { sessionStorage.removeItem('kds.tasarimci.durum'); }
      finally { durumHazir.current = true; }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- yalnız açılışta bir kez
  }, []);
  // M12: kayıt başarısının kendi state değişimi (raporBilgi) persist'i tetikler — o tek yazım
  // bastırılır ki silinen taslak geri yazılmasın; sonraki gerçek düzenlemeler yine saklanır.
  const kayitSonrasiTemiz = useRef(false);
  useEffect(() => {
    if (!durumHazir.current || !haritaId) return;
    if (kayitSonrasiTemiz.current) { kayitSonrasiTemiz.current = false; return; }
    // D1: her tuş vuruşunda değil, kısa sessizlikten sonra yaz (büyük state'in stringify maliyeti).
    const zamanlayici = setTimeout(() => {
      sessionStorage.setItem('kds.tasarimci.durum', JSON.stringify({
        surum: STATE_VERSION,
        haritaId, adim, alanlar, filtreler, parametreler, siralar, limit, joinSecim,
        gruplamalar, ozetler, bilesikler, elleKosul, elleBaglac, kokTabloId, joinAyarlari, joinTurleri, kosullular, caseAlanlar, ozetKosullar, hesaplananlar, metrikler, ifadeKosullari, pencereler, ilkN, araKume, capraz, araToplam, varliklar, tekille, atla, tabloOrnekleri, birlesimler, birlesimTekille, sadeceJoin, tabloSirasi, raporBilgi,
      }));
    }, 300);
    return () => clearTimeout(zamanlayici);
  }, [haritaId, adim, alanlar, filtreler, parametreler, siralar, limit, joinSecim, gruplamalar, ozetler, bilesikler, elleKosul, elleBaglac, kokTabloId, joinAyarlari, joinTurleri, kosullular, caseAlanlar, ozetKosullar, hesaplananlar, metrikler, ifadeKosullari, pencereler, ilkN, araKume, capraz, araToplam, varliklar, tekille, atla, tabloOrnekleri, birlesimler, birlesimTekille, sadeceJoin, tabloSirasi, raporBilgi]);

  /** Kaydet diyaloğunu aç: mevcut rapor bilgisi ya da akıllı varsayılanlarla. */
  async function openSave() {
    try { setKlasorler(await getFolders()); } catch { setKlasorler([]); }
    if (raporBilgi) {
      setKAd(raporBilgi.ad); setKKlasor(raporBilgi.klasorId ?? ''); setKPaylasim(raporBilgi.paylasim); setKTur(raporBilgi.gorsellestirme);
      setKPaylasimRolleri(raporBilgi.paylasimRolleri ?? []);
    } else {
      setKAd('');
      setKKlasor('');
      setKPaylasim('Ozel');
      setKPaylasimRolleri([]);
      // Öneri: tarih kırılımı → çizgi · tek alan + toplama → sayı kartı · toplama + kırılım → çubuk · aksi → tablo
      const tarihli = alanlar.some((a) => {
        if (a.toplama !== 'Yok') return false;
        const vt = detay?.tablolar.find((t) => t.id === a.tabloId)?.kolonlar.find((k) => k.hamAd === a.kolon)?.veriTipi;
        return vt?.toLowerCase().startsWith('date') ?? false;
      });
      const agg = alanlar.some((a) => a.toplama !== 'Yok');
      const kirilim = alanlar.some((a) => a.toplama === 'Yok');
      setKTur(tarihli && agg ? 'Cizgi' : agg && kirilim ? 'Cubuk' : agg && alanlar.length === 1 ? 'SayiKarti' : 'Tablo');
    }
    setKYeniKlasor('');
    setKaydetAcik(true);
  }

  async function saveReport() {
    const m = buildModelState(); if (!m) { setKaydetAcik(false); return; }
    setKaydediyor(true);
    const kadiBitir = kadiYukleniyor('Raporu kaydediyorum…');
    try {
      let klasorId: string | null = kKlasor === '' || kKlasor === '__yeni' ? null : kKlasor;
      if (kKlasor === '__yeni' && kYeniKlasor.trim())
        klasorId = (await addFolder(kYeniKlasor.trim())).id;
      const govde = {
        ad: kAd.trim(), klasorId, model: m, gorsellestirme: kTur, paylasim: kPaylasim,
        paylasimRolleri: kPaylasim === 'Roller' ? kPaylasimRolleri : null,   // S27
      };
      const ozet = raporBilgi ? await updateReport(raporBilgi.id, govde) : await addReport(govde);
      setRaporBilgi({ id: ozet.id, ad: ozet.ad, klasorId: ozet.klasorId, paylasim: ozet.paylasim, gorsellestirme: ozet.gorsellestirme, paylasimRolleri: ozet.paylasimRolleri ?? null });
      setKaydetAcik(false);
      setBasari(`"${ozet.ad}" raporu kaydedildi — menüdeki Raporlar'da.`); kadiBasarili();
      // M12: rapor KAYDEDİLDİ → taslak biter. Oturum taslağı silinir ve bu kaydın tetiklediği
      // tek persist yazımı bastırılır — tasarımcıya sonraki gelişte temiz ekran açılır
      // (yalnız YARIM kalan işler geri gelir). Kayıttan sonra düzenlemeye devam edilirse o
      // değişiklikler yeniden taslak olur (yarım güncelleme de korunur).
      sessionStorage.removeItem('kds.tasarimci.durum');
      kayitSonrasiTemiz.current = true;
    } catch (e) { setIslemHata((e as Error).message); kadiHata(); setKaydetAcik(false); }
    finally { kadiBitir(); setKaydediyor(false); }
  }

  const tableName = useCallback((id: string) => {
    const t = detay?.tablolar.find((x) => x.id === id);
    return t ? (t.dostcaAd || t.hamAd) : '?';
  }, [detay]);

  /** M35: tablo sorguya İLK girdiğinde ekleme sırasına yazılır (alan tıklayarak ya da + ile).
   * Böylece "ilk eklediğim tablo base olsun" beklentisi karşılanır: FROM kökü otomatikken
   * sıranın ilki kök olur; Join Yolları satır sırası da gerçek ekleme sırasını gösterir. */
  const sirayaEkle = (tabloId: string) =>
    setTabloSirasi((p) => (p.includes(tabloId) ? p : [...p, tabloId]));

  function addField(tabloId: string, kolon: string, rol: ColumnRole, etiket: string) {
    if (alanlar.some((a) => a.tabloId === tabloId && a.kolon === kolon && a.toplama === 'Yok')) return;
    sirayaEkle(tabloId);
    // Test dönüşü #8: ölçü rollü alan otomatik SUM almasın — her alan "Varsayılan" başlar,
    // toplamayı kullanıcı seçer (her sorgu aggregate değildir).
    setAlanlar((p) => [...p, { anahtar: uuid(), tabloId, kolon, etiket, rol, toplama: 'Yok', takmaAd: '' }]);
  }
  const updateField = (anahtar: string, degis: Partial<EditField>) =>
    setAlanlar((p) => p.map((a) => (a.anahtar === anahtar ? { ...a, ...degis } : a)));
  function removeField(anahtar: string) {
    setAlanlar((p) => p.filter((a) => a.anahtar !== anahtar));
    setSiralar((p) => p.filter((s) => s.alanAnahtar !== anahtar));
  }
  /** Kanvastan tabloyu tümüyle çıkar: alanları + filtreleri + bağlı sıralamaları temizle. */
  function removeTable(tabloId: string) {
    const silinen = new Set(alanlar.filter((a) => a.tabloId === tabloId).map((a) => a.anahtar));
    setAlanlar((p) => p.filter((a) => a.tabloId !== tabloId));
    setFiltreler((p) => p.filter((f) => f.tabloId !== tabloId));
    setSiralar((p) => p.filter((s) => !silinen.has(s.alanAnahtar)));
    setSadeceJoin((p) => p.filter((t) => t !== tabloId)); // M17: alansız eklenmişse onu da çıkar
    setTabloSirasi((p) => p.filter((t) => t !== tabloId)); // M18: sıradan da çıkar
    // M36: tür ezmesi de gitsin (tablo yoksa ezmesi de anlamsız).
    setJoinTurleri((p) => { if (!(tabloId in p)) return p; const n = { ...p }; delete n[tabloId]; return n; });
  }
  // Canvas düğümlerine verilen callback'ler SABİT kalmalı (yoksa düğüm state'i her render'da
  // yeniden kurulur); güncel kapanışlara ref üzerinden ulaşılır.
  const removeFieldRef = useRef(removeField); removeFieldRef.current = removeField;
  const removeTableRef = useRef(removeTable); removeTableRef.current = removeTable;
  const removeFieldOnCanvas = useCallback((k: string) => removeFieldRef.current(k), []);
  const removeTableOnCanvas = useCallback((t: string) => removeTableRef.current(t), []);

  const tumKolonlar = (detay?.tablolar ?? []).flatMap((t) =>
    t.kolonlar.map((k) => ({ tabloId: t.id, kolon: k.hamAd, etiket: `${t.dostcaAd || t.hamAd} · ${k.dostcaAd || k.hamAd}` })));

  function addFilter() {
    const ilk = tumKolonlar[0];
    if (!ilk) return;
    setFiltreler((p) => [...p, { anahtar: uuid(), tabloId: ilk.tabloId, kolon: ilk.kolon, op: 'Esit', d1: '', d2: '' }]);
  }
  const updateFilter = (anahtar: string, degis: Partial<EditFilter>) =>
    setFiltreler((p) => p.map((f) => (f.anahtar === anahtar ? { ...f, ...degis } : f)));
  const removeFilter = (anahtar: string) => setFiltreler((p) => p.filter((f) => f.anahtar !== anahtar));

  // ---- parametreler (çalıştırma-anı) ----
  const addParametre = () => setParametreler((p) => {
    const n = p.length + 1;
    return [...p, { anahtar: `p${n}_${uuid().slice(0, 4)}`, etiket: `Parametre ${n}`, tip: 'Metin', zorunlu: false, varsayilan: '', secenekler: '' }];
  });
  const updateParametre = (anahtar: string, degis: Partial<EditParametre>) =>
    setParametreler((p) => p.map((x) => (x.anahtar === anahtar ? { ...x, ...degis } : x)));
  const removeParametre = (anahtar: string) => {
    setParametreler((p) => p.filter((x) => x.anahtar !== anahtar));
    // Bu parametreye bağlı filtreler sabit değere döner.
    setFiltreler((p) => p.map((f) => (f.paramAnahtar === anahtar ? { ...f, paramAnahtar: undefined } : f)));
  };
  // Panel + değer tohumlama için EditParametre → ReportParameter.
  const parametrelerRapor: ReportParameter[] = useMemo(() => parametreler.map((p) => ({
    anahtar: p.anahtar, etiket: p.etiket, tip: p.tip, zorunlu: p.zorunlu,
    varsayilan: p.tip === 'TarihAralik' ? null : (p.varsayilan.trim() || null),
    secenekler: p.tip === 'Liste'
      ? p.secenekler.split(',').map((s) => s.trim()).filter(Boolean).map((s) => ({ deger: s, etiket: s }))
      : null,
  })), [parametreler]);
  // Parametre tanımı değişince değer sözlüğünü varsayılanlarla tazele (girilmiş değerleri koru).
  useEffect(() => {
    setParametreDegerleri((mevcut) => {
      const yeni = ilkParametreDegerleri(parametrelerRapor);
      for (const k of Object.keys(yeni)) if (k in mevcut) yeni[k] = mevcut[k];
      return yeni;
    });
  }, [parametrelerRapor]);

  // Y2 — ALAN OLMAYAN çıkış kolonları (hesaplanan, özet, koşullu ölçü, CASE, birleşik). Sıralamada
  // ve formül koşullarında [Ad] ile atıf yapılabilsin diye toplanır. Yalnız ADI OLANLAR listelenir:
  // adsız kolonun sunucu tarafındaki otomatik adı (Hesap1, Case2…) burada tahmin edilmez — kullanıcı
  // ad verince listeye düşer (aşağıdaki not bunu söyler).
  const cikisKolonlari = useMemo(() => {
    const ekle = (ad: string, tur: string, out: { ad: string; tur: string }[]) => {
      const duz = ad.trim();
      if (duz && !out.some((x) => x.ad.toLowerCase() === duz.toLowerCase())) out.push({ ad: duz, tur });
    };
    const liste: { ad: string; tur: string }[] = [];
    for (const h of hesaplananlar) ekle(h.takmaAd, 'hesaplanan', liste);
    for (const o of ozetler) ekle(o.takmaAd, 'bağlı tablo özeti', liste);
    for (const k of kosullular) ekle(k.takmaAd, 'koşullu ölçü', liste);
    for (const c of caseAlanlar) ekle(c.takmaAd, 'CASE', liste);
    for (const b of bilesikler) ekle(b.takmaAd, 'birleşik', liste);
    for (const p of pencereler) ekle(p.takmaAd, 'pencere', liste); // Y6
    return liste;
  }, [hesaplananlar, ozetler, kosullular, caseAlanlar, bilesikler, pencereler]);
  // Formül kutularının "＋ kolon" listesi: etiket + {Tablo.Kolon} yer tutucusu (ham tablo adıyla).
  const ifadeKolonlari = useMemo(() => tumKolonlar.map((k) => ({
    ...k, yerTutucu: `{${detay?.tablolar.find((t) => t.id === k.tabloId)?.hamAd ?? ''}.${k.kolon}}`,
  })), [tumKolonlar, detay]);
  const adsizCikisVar =
    hesaplananlar.some((h) => !h.takmaAd.trim()) || ozetler.some((o) => !o.takmaAd.trim())
    || kosullular.some((k) => !k.takmaAd.trim()) || caseAlanlar.some((c) => !c.takmaAd.trim())
    || bilesikler.some((b) => !b.takmaAd.trim()) || pencereler.some((p) => !p.takmaAd.trim());

  const guncelleIfadeKosul = (anahtar: string, degis: Partial<EditIfadeKosul>) =>
    setIfadeKosullari((p) => p.map((k) => (k.anahtar === anahtar ? { ...k, ...degis } : k)));
  /** Formül kutusunun SONUNA metin ekler (kolon/çıkış seçicileri) — araya boşluk konur. */
  const ifadeyeEkle = (anahtar: string, parca: string) =>
    setIfadeKosullari((p) => p.map((k) => (k.anahtar === anahtar
      ? { ...k, ifade: k.ifade.trim() ? `${k.ifade.trimEnd()} ${parca}` : parca }
      : k)));

  const addSort = () => {
    if (alanlar[0]) { setSiralar((p) => [...p, { anahtar: uuid(), alanAnahtar: alanlar[0].anahtar, azalan: true }]); return; }
    // Y2: hiç alan yoksa ama adlandırılmış bir çıkış kolonu varsa sıra onunla başlar.
    const c = cikisKolonlari[0];
    if (c) setSiralar((p) => [...p, { anahtar: uuid(), alanAnahtar: '', azalan: true, cikisAdi: c.ad }]);
  };
  const updateSort = (anahtar: string, degis: Partial<EditSort>) =>
    setSiralar((p) => p.map((s) => (s.anahtar === anahtar ? { ...s, ...degis } : s)));
  const removeSort = (anahtar: string) => setSiralar((p) => p.filter((s) => s.anahtar !== anahtar));

  const aggVar = alanlar.some((a) => a.toplama !== 'Yok') || kosullular.length > 0; // koşullu ölçü de aggregate
  // GROUP BY: otomatik = aggregate-olmayan seçili kolonlar (dedup); kullanıcı düzenlerse gruplamalar
  // (elle liste) devreye girer. etkinGruplama ekranda gösterilen/gönderilen listedir.
  // Y4: kırılımlı kolon GROUP BY'a indirgenmiş hâliyle girer → anahtara da kova katılır (aynı
  // kolonun yıl ve ay kırılımı iki AYRI gruplamadır).
  const autoGruplama: GroupingItem[] = aggVar
    ? [...new Map(alanlar.filter((a) => a.toplama === 'Yok')
        .map((a) => [`${a.tabloId}|${a.kolon}|${a.kova ?? ''}`, { tabloId: a.tabloId, kolon: a.kolon, kova: a.kova ?? null }])).values()]
    : [];
  const etkinGruplama = gruplamalar ?? autoGruplama;
  const findColumnName = (tabloId: string, kolon: string) => {
    const t = detay?.tablolar.find((x) => x.id === tabloId);
    return t?.kolonlar.find((x) => x.hamAd === kolon)?.dostcaAd || kolon;
  };
  const kolonVeriTipi = (tabloId: string, kolon: string) =>
    detay?.tablolar.find((x) => x.id === tabloId)?.kolonlar.find((x) => x.hamAd === kolon)?.veriTipi;
  const removeGrouping = (idx: number) => setGruplamalar(etkinGruplama.filter((_, i) => i !== idx));
  const addGrouping = (tabloId: string, kolon: string) => {
    if (etkinGruplama.some((g) => g.tabloId === tabloId && g.kolon === kolon)) return;
    setGruplamalar([...etkinGruplama, { tabloId, kolon }]);
  };
  const kullanilan = useMemo(() => {
    const ham = [...new Set([...alanlar.map((a) => a.tabloId), ...filtreler.map((f) => f.tabloId), ...sadeceJoin])];
    if (tabloSirasi.length === 0) return ham;
    // M18: kullanıcı sırası öncelikli; sırada olmayan yeniler ekleme sırasıyla sona.
    const idx = (t: string) => { const i = tabloSirasi.indexOf(t); return i === -1 ? 9999 : i; };
    return ham.sort((a, b) => idx(a) - idx(b));
  }, [alanlar, filtreler, sadeceJoin, tabloSirasi]);

  // M35: otomatik kök = SORGUYA İLK GİREN tablo (alan tıklayarak ya da + ile eklenen — ikisi de
  // ekleme sırasına yazılır). Eskiden yalnız ilk ALANIN tablosuna bakılıyordu; tabloyu + ile
  // eklediysen ya da sırayı değiştirdiysen "ilk eklediğim tablo" ile kök tutmuyordu.
  const otomatikKok = kullanilan[0] ?? alanlar[0]?.tabloId ?? '';
  const etkinKok = kokTabloId || otomatikKok;

  // Üreticiyle aynı mantık: KÖKTEN haritanın TÜM ilişkileri üzerinden BFS; kullanılan bir tablo
  // erişilemiyorsa join yolu yok → kanvasta kırmızı + istek atılmadan uyarı.
  const erisilebilen = useMemo(() => {
    const kok = etkinKok;
    if (!kok || !detay) return new Set<string>(kullanilan);
    const komsu = new Map<string, string[]>();
    for (const i of detay.iliskiler) {
      komsu.set(i.kaynakTabloId, [...(komsu.get(i.kaynakTabloId) ?? []), i.hedefTabloId]);
      komsu.set(i.hedefTabloId, [...(komsu.get(i.hedefTabloId) ?? []), i.kaynakTabloId]);
    }
    const gorulen = new Set([kok]);
    const kuyruk = [kok];
    while (kuyruk.length) {
      const u = kuyruk.shift()!;
      for (const v of komsu.get(u) ?? []) if (!gorulen.has(v)) { gorulen.add(v); kuyruk.push(v); }
    }
    return gorulen;
  }, [etkinKok, kullanilan, detay]);
  // D2: kopukluk denetimi yalnız kanvas tablolarını değil, özet/bileşik/koşullu ölçünün
  // çektiği tabloları da kapsar — backend hatasından ÖNCE ekranda uyarı çıkar.
  const kullanilanTum = useMemo(() => {
    const s = new Set(kullanilan);
    for (const o of ozetler) { s.add(o.tabloId); s.add(o.bagTabloId); }
    for (const b of bilesikler) for (const p of b.parcalar) if (p.tur === 'kolon' && p.tabloId) s.add(p.tabloId);
    for (const ko of kosullular) { if (ko.tabloId) s.add(ko.tabloId); for (const f of ko.kosullar) if (f.tabloId) s.add(f.tabloId); }
    for (const c of caseAlanlar) {
      if (c.varsayilanTur === 'kolon' && c.varsayilanTabloId) s.add(c.varsayilanTabloId);
      for (const d of c.dallar) {
        if (d.sonucTur === 'kolon' && d.tabloId) s.add(d.tabloId);
        for (const f of d.kosullar) if (f.tabloId) s.add(f.tabloId);
      }
    }
    return [...s];
  }, [kullanilan, ozetler, bilesikler, kosullular, caseAlanlar]);
  const kopukTablolar = kullanilanTum.filter((id) => id && !erisilebilen.has(id));

  // ---- kanvas: düğümler STATE'te tutulur; React Flow'un boyut/pozisyon değişimleri
  // applyNodeChanges ile uygulanır (v12'de dimensions uygulanmazsa düğüm hiç görünmez!).
  useEffect(() => {
    setDugumler((eski) => {
      // M2: yeni düğüm mevcut kümeye YASLANIR (masonry): 4 sütunlu düzende en erken biten sütunun
      // altına yerleşir. Oluk ORANTILI (kullanıcı: "yatayla dikeyin oranı aynı olsun"): yatay boşluk
      // düğüm genişliğinin %80'i (160/200), dikey boşluk da ÜSTteki düğüm BOYUNUN %80'i — düğümler
      // enine geniş boyuna kısa olduğundan mutlak eşit px orantısız görünüyordu. Eski davranış
      // (orijine çapalı sabit slot) yeni tabloyu çok uzağa düşürüyordu (test turu M2).
      const OLUK = 160;               // yatay düğüm arası boşluk (200 genişliğin %80'i)
      const ORAN = OLUK / 200;        // boşluk/düğüm oranı — dikeyde de aynı oran uygulanır
      const SUTUN_ADIMI = 200 + OLUK; // düğüm genişliği + oluk
      const doluKutular = eski.filter((n) => kullanilan.includes(n.id))
        .map((n) => ({ x: n.position.x, y: n.position.y, w: n.width ?? 200, h: n.height ?? 80 }));
      const yeniKonum = (yeniBoy: number) => {
        const aday = (() => {
          if (doluKutular.length === 0) return { x: 30, y: 35 };
          const minX = Math.min(...doluKutular.map((p) => p.x));
          const minY = Math.min(...doluKutular.map((p) => p.y));
          let enIyi = { x: minX, y: Number.MAX_SAFE_INTEGER };
          for (let sutun = 0; sutun < 4; sutun++) {
            const x = minX + sutun * SUTUN_ADIMI;
            const sutundakiler = doluKutular.filter((p) => p.x < x + SUTUN_ADIMI - 20 && p.x + p.w > x - 20);
            // Dikey boşluk = sütunun en alttaki düğümünün boyu × ORAN (yatayla aynı görsel orantı).
            const y = sutundakiler.length === 0
              ? minY
              : (() => {
                  const enAlt = sutundakiler.reduce((a, p) => (p.y + p.h > a.y + a.h ? p : a));
                  return enAlt.y + enAlt.h + Math.round(enAlt.h * ORAN);
                })();
            if (y < enIyi.y - 1) enIyi = { x, y };
          }
          return enIyi;
        })();
        doluKutular.push({ ...aday, w: 200, h: yeniBoy }); // arka arkaya eklenenler üst üste binmesin
        return { x: aday.x, y: aday.y };
      };
      return kullanilan.map((id) => {
      const onceki = eski.find((n) => n.id === id);
      const satirlar = alanlar.filter((a) => a.tabloId === id)
        .map((a) => ({ anahtar: a.anahtar, renk: roleColor[a.rol], ad: a.takmaAd || a.etiket, ek: aggShort[a.toplama] }));
      // İçeriğe göre otomatik boy (en çok ~8 satır; fazlası düğüm içinde kaydırılır).
      const boy = Math.min(46 + Math.max(satirlar.length, 1) * 19 + (erisilebilen.has(id) ? 0 : 18), 210);
      return {
        id, type: 'tablo' as const,
        // Mevcut düğümün konumu korunur (kullanıcı taşımışsa da); yeniler kümeye yaslanır.
        position: onceki?.position ?? yeniKonum(boy),
        // Kullanıcı boyutlandırdıysa (NodeResizer) koru; yoksa otomatik boy.
        width: elleBoyutlanan.current.has(id) ? onceki?.width : 200,
        height: elleBoyutlanan.current.has(id) ? onceki?.height : boy,
        data: {
          ad: tableName(id),
          satirlar,
          bagli: erisilebilen.has(id),
          removeField: removeFieldOnCanvas,
          removeTable: removeTableOnCanvas,
        },
      };
      });
    });
  }, [kullanilan, alanlar, erisilebilen, tableName, removeFieldOnCanvas, removeTableOnCanvas]);

  // M2 (görünürlük): kanvasa YENİ tablo girince kadrajı animasyonla tüm düğümlere oturt —
  // yeni düğüm, kullanıcı kanvası nereye kaydırmış olursa olsun görünür kalır.
  const akisRef = useRef<{ fitView: (o?: { padding?: number; maxZoom?: number; duration?: number }) => Promise<boolean> } | null>(null);
  const oncekiKullanilanRef = useRef<string[]>([]);
  useEffect(() => {
    const yeniEklendi = kullanilan.some((id) => !oncekiKullanilanRef.current.includes(id));
    oncekiKullanilanRef.current = kullanilan;
    if (!yeniEklendi) return;
    // Düğüm state'i commit olduktan sonra kadrajla; ilk düğümde instance henüz yok → fitView prop'u halleder.
    const z = setTimeout(() => void akisRef.current?.fitView({ padding: 0.3, maxZoom: 1, duration: 250 }), 80);
    return () => clearTimeout(z);
  }, [kullanilan]);

  const onNodeChange = useCallback((degisimler: NodeChange<TabloNode>[]) => {
    for (const d of degisimler)
      if (d.type === 'dimensions' && d.resizing) elleBoyutlanan.current.add(d.id);
    setDugumler((n) => applyNodeChanges(degisimler, n));
  }, []);

  // Kullanılan tablo çiftleri → aralarındaki (bir ya da BİRDEN ÇOK) harita ilişkisi.
  const ciftIliskileri = useMemo(() => {
    const m = new Map<string, DataMapRelationshipDto[]>();
    for (const i of detay?.iliskiler ?? []) {
      if (!kullanilan.includes(i.kaynakTabloId) || !kullanilan.includes(i.hedefTabloId)) continue;
      const k = pairKey(i.kaynakTabloId, i.hedefTabloId);
      m.set(k, [...(m.get(k) ?? []), i]);
    }
    return m;
  }, [detay, kullanilan]);
  // Etkin FROM kökü + JOIN AĞACI — backend JoinPlani'nin istemci yansıması. Canvas artık "ilişkisi
  // olan her çift" yerine yalnız GERÇEKTEN kullanılacak kenarları çizer (test dönüşü #3: çift FK'li
  // tabloda fazladan kenar kafa karıştırıyordu); Join Yolları adımı da otomatik ebeveyni buradan
  // gösterir. Ayar zinciri önce (fixed-point), kalanlar çok-kaynaklı BFS.
  // (Kök tanımı yukarıda — erisilebilen hesabı da AYNI kökü kullanır.)
  // Etkin join ağacı — saf çekirdek tasarimci/model.ts'te (Y4: test edilebilir).
  const agac = useMemo(
    () => buildTree(detay, etkinKok, kullanilan, joinAyarlari, joinSecim, joinTurleri),
    [detay, etkinKok, kullanilan, joinAyarlari, joinSecim, joinTurleri]);

  // Canvas kenarları: ağacın kenarları (tablo başına TEK ebeveyn). Ayarlı kenara tıklama Join
  // Yolları adımına götürür; otomatik kenarda kolon diyaloğu açılır.
  const kenarlar = useMemo<Edge[]>(() => {
    const sonuc: Edge[] = [];
    for (const [tid, k] of agac) {
      if (k.araMi) continue; // ebeveyn kanvasta değil (ara tablo) — çizilecek düğüm yok (O4)
      const cift = pairKey(tid, k.ebeveyn);
      sonuc.push({
        id: cift, source: k.ebeveyn, target: tid, type: 'join',
        data: {
          etiket: (k.tur === 'Left' ? 'LEFT · ' : '') + k.etiket,
          ozel: k.ozel,
          ac: () => {
            if (k.ayarli) { setAdim(1); return; } // elle ayarlı kenar Join Yolları adımında düzenlenir
            const il = joinSecim[cift]
              ?? detay?.iliskiler.find((i) => pairKey(i.kaynakTabloId, i.hedefTabloId) === cift);
            if (il) setJoinDiyalog({ cift, tidA: il.kaynakTabloId, tidB: il.hedefTabloId, kolA: il.kaynakKolon, kolB: il.hedefKolon });
          },
        } satisfies JoinEdgeData,
      });
    }
    return sonuc;
  }, [agac, joinSecim, detay]);

  /** Join Yolları satırı: ayar kur/güncelle; ebeveyn boşalırsa (Otomatik) ayar silinir. */
  const setupSettings = (tid: string, d: Partial<EditJoinSetting>) => setJoinAyarlari((p) => {
    const mevcut = p[tid] ?? { ebeveynTabloId: agac.get(tid)?.ebeveyn ?? '', tur: 'Inner' as JoinType, kosullar: [] };
    const yeni = { ...mevcut, ...d };
    if (!yeni.ebeveynTabloId) { const n = { ...p }; delete n[tid]; return n; }
    return { ...p, [tid]: yeni };
  });
  const tabloKolonlari = (tid: string) => detay?.tablolar.find((t) => t.id === tid)?.kolonlar ?? [];

  /** Y14 — tabloyu ikinci kez ekle: haritaya SANAL bir kopya (yeni kimlik + "(2)" etiketi) ve
   * kaynak tablonun TÜM ilişkilerinin kopyası eklenir. Böylece kopya, palette/kanvasta/join
   * yollarında normal bir tablo gibi davranır; SQL'de ayrı bir takma ad alır. */
  function tabloyuKopyala(kaynakId: string) {
    if (!detay) return;
    const kaynak = detay.tablolar.find((t) => t.id === kaynakId);
    if (!kaynak) return;
    const ornekId = uuid();
    const sayi = tabloOrnekleri.filter((o) => o.kaynakTabloId === kaynakId).length + 2; // ilk kopya "(2)"
    const etiket = `${kaynak.dostcaAd || kaynak.hamAd} (${sayi})`;
    setDetay({
      ...detay,
      tablolar: [...detay.tablolar, { ...kaynak, id: ornekId, dostcaAd: etiket }],
      iliskiler: [
        ...detay.iliskiler,
        ...detay.iliskiler.filter((i) => i.kaynakTabloId === kaynakId).map((i) => ({ ...i, kaynakTabloId: ornekId })),
        ...detay.iliskiler.filter((i) => i.hedefTabloId === kaynakId).map((i) => ({ ...i, hedefTabloId: ornekId })),
      ],
    });
    setTabloOrnekleri((p) => [...p, { ornekId, kaynakTabloId: kaynakId, etiket }]);
    setAcikAyar((p) => ({ ...p, [ornekId]: true })); // kopya açık gelsin: kullanıcı ne olduğunu görsün
  }

  // ---- sürükle-bırak ----
  const onDragStart = (e: DragEvent, veri: { tabloId: string; kolon: string; rol: ColumnRole; etiket: string }) => {
    e.dataTransfer.setData(dragType, JSON.stringify(veri));
    e.dataTransfer.effectAllowed = 'copy';
  };
  const onDrop = (e: DragEvent) => {
    const ham = e.dataTransfer.getData(dragType);
    if (!ham) return;
    e.preventDefault();
    const v = JSON.parse(ham) as { tabloId: string; kolon: string; rol: ColumnRole; etiket: string };
    addField(v.tabloId, v.kolon, v.rol, v.etiket);
  };
  const uzerine = (e: DragEvent) => { if (e.dataTransfer.types.includes(dragType)) { e.preventDefault(); e.dataTransfer.dropEffect = 'copy'; } };

  /** QueryModel kurar — saf çekirdek tasarimci/model.ts'te; burada yalnız hata gösterimi. */
  function buildModelState(): QueryModel | null {
    // M36: otomatik satırdaki tür ezmesi (LEFT), SQL üretilirken O ANKİ otomatik ebeveynle
    // birleştirilir — koşullar boş kalır (haritadaki ilişki kullanılır). Ara tablo üzerinden
    // bağlanan satırda ezme yapılamaz (seçici zaten kapalı), bu yüzden atlanır.
    const ayarlarTurleriyle = { ...joinAyarlari };
    for (const [tid, tur] of Object.entries(joinTurleri)) {
      if (ayarlarTurleriyle[tid]) continue; // kullanıcı zaten elle ayarlamış
      const oto = agac.get(tid);
      if (!oto || oto.araMi) continue;
      ayarlarTurleriyle[tid] = { ebeveynTabloId: oto.ebeveyn, tur, kosullar: [] };
    }
    const sonuc = buildModel({
      haritaId, alanlar, filtreler, siralar, limit, joinSecim, gruplamalar,
      ozetler, bilesikler, kosullular, caseAlanlar, ozetKosullar, hesaplananlar, metrikler, ifadeKosullari, pencereler, ilkN, araKume, capraz, araToplam, varliklar, tekille, atla, tabloOrnekleri, parametreler, elleKosul, elleBaglac, kokTabloId,
      birlesimModelleri: birlesimler.map((b) => b.model), birlesimTekille,
      joinAyarlari: ayarlarTurleriyle,
      sadeceJoinTablolar: sadeceJoin, tabloSirasi, kopukTablolar, tableName,
    });
    if ('hata' in sonuc) { setIslemHata(sonuc.hata); kadiHata(); return null; }
    return sonuc.model;
  }

  // O8: sayfadan ayrılınca uçuştaki üretim/koşum iptal edilir (unmounted setState önlenir).
  const kosumIptalRef = useRef<AbortController | null>(null);
  useEffect(() => () => kosumIptalRef.current?.abort(), []);
  function yeniKosumSinyali(): AbortSignal {
    kosumIptalRef.current?.abort();
    const c = new AbortController();
    kosumIptalRef.current = c;
    return c.signal;
  }

  async function sqlGoster() {
    const m = buildModelState(); if (!m) return;
    setMesgul('sql'); setIslemHata(null); setSekme('sql');
    try { setSql(await generateQuerySql(m, parametreDegerleri, yeniKosumSinyali())); }
    catch (e) { if ((e as Error).name !== 'AbortError') { setIslemHata((e as Error).message); kadiHata(); } }
    finally { setMesgul(''); }
  }
  async function calistir() {
    const m = buildModelState(); if (!m) return;
    setMesgul('calistir'); setIslemHata(null); setSekme('sonuc');
    try { setSonuc(await runQuery(m, parametreDegerleri, yeniKosumSinyali())); kadiBasarili('Sorguyu tamamladım — sonuç aşağıda.'); }
    catch (e) { if ((e as Error).name !== 'AbortError') { setIslemHata((e as Error).message); kadiHata(); } }
    finally { setMesgul(''); }
  }
  /** Y9: çapraz sütun listesi SQL'de sabit olmak zorunda — bu düğme sorguyu (çapraz KAPALI hâliyle)
   * bir kez çalıştırıp seçilen başlık kolonundaki DEĞERLERİ okur ve listeyi doldurur. */
  const [basliklarYukleniyor, setBasliklarYukleniyor] = useState(false);
  async function basliklariGetir() {
    const m = buildModelState();
    if (!m || !capraz.baslikKolonu) return;
    setBasliklarYukleniyor(true); setIslemHata(null);
    try {
      const r = await runQuery({ ...m, capraz: null, satirLimiti: 5000 }, parametreDegerleri, yeniKosumSinyali());
      const idx = r.kolonlar.findIndex((k) => k.ad.toLowerCase() === capraz.baslikKolonu.toLowerCase());
      if (idx < 0) { setIslemHata(`'${capraz.baslikKolonu}' sonuçta bulunamadı.`); kadiHata(); return; }
      const degerler = [...new Set(r.satirlar.map((s) => s[idx]).filter((v) => v != null).map(String))]
        .sort((a, b) => a.localeCompare(b, 'tr')).slice(0, 50);
      if (degerler.length === 0) { setIslemHata('Başlık kolonunda değer bulunamadı.'); kadiHata(); return; }
      setCapraz((p) => ({ ...p, basliklar: degerler.join(', ') }));
    } catch (e) { if ((e as Error).name !== 'AbortError') { setIslemHata((e as Error).message); kadiHata(); } }
    finally { setBasliklarYukleniyor(false); }
  }

  /** M33 "Sorun ne?": mevcut modeli ÇALIŞTIRMADAN tanılatır (tahmini plan yorumu). */
  async function tanila() {
    const m = buildModelState();
    if (!m) throw new Error('Model kurulamadı — üstteki hatayı düzeltin.');
    return diagnoseQueryModel(m, parametreDegerleri, yeniKosumSinyali());
  }

  /** Adım geçişi: son adıma girerken SQL otomatik üretilir (şeffaflık — kullanıcı beklemeden görür). */
  function goToStep(yeni: number) {
    if (yeni > 0 && alanlar.length === 0) { setIslemHata('Önce 1. adımda en az bir alan ekleyin.'); kadiHata(); return; }
    setIslemHata(null);
    setAdim(yeni);
    if (yeni === 3) void sqlGoster();
  }

  if (yukleniyor) {
    return <LoadingCurtain />;
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column' }}>
      <PageHeader ikon={<DesignServicesOutlinedIcon />} baslik="Sorgu Tasarımcısı"
        aciklama="Adım adım rapor sorgusu — SQL yazmadan; join'ler haritadan otomatik.">
        {haritaId && alanlar.length > 0 && (
          <Button size="small" variant="outlined" startIcon={<RestartAltIcon sx={{ fontSize: 16 }} />}
            onClick={() => void sorguyuSifirla()} sx={{ ...bandButton, fontSize: 12 }}>
            Sorguyu Sıfırla
          </Button>
        )}
        <Typography sx={{ fontSize: 12, fontWeight: 700, opacity: 0.9 }}>Veri Haritası</Typography>
        <Select size="small" displayEmpty value={haritaId} onChange={(e) => void selectMap(e.target.value)}
          sx={{ minWidth: 170, ...whiteBox }}>
          <MenuItem value=""><em>Harita seç…</em></MenuItem>
          {haritalar.map((h) => <MenuItem key={h.id} value={h.id}>{h.ad}</MenuItem>)}
        </Select>
      </PageHeader>

      {hata && <Alert severity="error" sx={{ mb: 1.25 }}>{hata}</Alert>}
      {haritalar.length === 0 && <Alert severity="info" sx={{ mb: 1.25 }}>Önce bir <b>Veri Haritası</b> oluşturun — tasarımcı bir harita üzerine kurulur.</Alert>}
      {islemHata && <Alert severity="error" sx={{ mb: 1.25 }} onClose={() => setIslemHata(null)}
        action={islemHata.includes('zaman aşımı') ? <SorunNeButonu tanila={tanila} /> : undefined}>{islemHata}</Alert>}
      {basari && (
        <Alert severity="success" sx={{ mb: 1.25 }} onClose={() => setBasari(null)}
          action={<Button color="inherit" size="small" onClick={() => git('/raporlar')}>Raporlar'a git</Button>}>
          {basari}
        </Alert>
      )}

      {!detay ? (
        <Paper variant="outlined" sx={{ p: 4, borderRadius: boxRadius, borderStyle: 'dashed', textAlign: 'center' }}>
          <Typography color="text.secondary">Başlamak için yukarıdan bir <b>Veri Haritası</b> seç.</Typography>
        </Paper>
      ) : (
        <>
          {/* adım çubuğu — özel (MUI StepButton'ın taşan tıklama alanı yerine) */}
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1.5, flexWrap: 'wrap' }}>
            {stepDefs.map((t, i) => {
              const etkin = adim === i;
              const kilitli = i > 0 && alanlar.length === 0;
              return (
                <Box key={t.ad} sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  {i > 0 && <Box sx={{ width: 22, height: 2, bgcolor: 'var(--kds-dolgu)', borderRadius: 1 }} />}
                  <Box component="button" onClick={() => goToStep(i)} disabled={kilitli}
                    sx={{
                      font: 'inherit', display: 'inline-flex', alignItems: 'center', gap: 0.8,
                      px: 1.5, py: 0.7, borderRadius: smallRadius, cursor: kilitli ? 'default' : 'pointer',
                      border: '1px solid', fontSize: 12.5, fontWeight: 700,
                      ...(etkin
                        ? { background: 'linear-gradient(135deg,#0f2d4e,#0e6f7a)', color: '#fff', borderColor: 'transparent', boxShadow: '0 4px 12px rgba(15,45,78,.22)' }
                        : { bgcolor: 'background.paper', color: kilitli ? 'text.disabled' : 'text.secondary', borderColor: 'divider', '&:hover': kilitli ? {} : { borderColor: 'divider', color: 'primary.main' } }),
                    }}>
                    {t.ikon}
                    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'center', width: 17, height: 17, borderRadius: '50%', fontSize: 10.5, fontWeight: 800, bgcolor: etkin ? 'rgba(255,255,255,.22)' : 'var(--kds-dolgu)', color: etkin ? '#fff' : 'inherit' }}>{i + 1}</Box>
                    {t.ad}
                  </Box>
                </Box>
              );
            })}
          </Box>

          {/* ADIM 1 — sürükle-bırak sorgu oluşturma */}
          {adim === 0 && (
            <Box sx={{ display: 'flex', gap: 1.5, alignItems: 'stretch' }} onDrop={onDrop} onDragOver={uzerine}>
              <Paper variant="outlined" sx={{ width: 232, flexShrink: 0, borderRadius: boxRadius, overflow: 'hidden', bgcolor: 'var(--kds-zemin2)', display: 'flex', flexDirection: 'column' }}>
                <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 1.5, pt: 1.1, pb: 0.8 }}>
                  <Typography sx={{ fontSize: 12, fontWeight: 800, color: 'primary.main' }}>Alanlar</Typography>
                  <Typography sx={{ fontSize: 10.5 }} color="text.disabled">kanvasa sürükle / tıkla</Typography>
                </Box>
                <Box sx={{ px: 1.25, pb: 0.9 }}>
                  <TextField size="small" fullWidth placeholder="tablo / kolon ara…" value={alanArama}
                    onChange={(e) => setAlanArama(e.target.value)}
                    slotProps={{ input: { sx: { fontSize: 12, '& input': { py: 0.55 } } } }} />
                </Box>
                <Divider />
                {/* Çok tablolu haritada uzun düz liste yerine KATLANABİLİR gruplar: tablo başlığına
                    tıkla → alanları açılır. Arama varken yalnız eşleşenler (otomatik açık) görünür;
                    kanvasta kullanılan tablolar zaten açık tutulur. */}
                <Box sx={{ flexGrow: 1, overflowY: 'auto', py: 0.4, bgcolor: 'background.paper' }}>
                  {detay.tablolar.map((t) => {
                    // M3: Türkçe-duyarsız katlama — "ilceler" araması "Ilceler"i bulur (i/ı/İ/I eş).
                    const ara = araKatla(alanArama.trim());
                    const tableLabel = t.dostcaAd || t.hamAd;
                    const tabloEslesti = ara !== '' && araKatla(tableLabel).includes(ara);
                    const gorunenKolonlar = ara === '' || tabloEslesti
                      ? t.kolonlar
                      : t.kolonlar.filter((k) => araKatla(k.dostcaAd || k.hamAd).includes(ara));
                    if (ara !== '' && !tabloEslesti && gorunenKolonlar.length === 0) return null;
                    // M36: tablolar KAPALI başlar — kullanıcı başlığa tıklayıp açar. (Eskiden
                    // "kullanılan tablo açık başlar" varsayılanı vardı: sorguya giren her tablo
                    // kendiliğinden açılıp listeyi şişiriyordu.) Arama sırasında eşleşenler açılır.
                    const acik = ara !== '' || (acikAyar[t.id] ?? false);
                    return (
                      <Box key={t.id} sx={{ mb: 0.2 }}>
                        <Box onClick={() => setAcikAyar((p) => ({ ...p, [t.id]: !acik }))}
                          sx={{ display: 'flex', alignItems: 'center', gap: 0.7, px: 1.25, py: 0.5, cursor: 'pointer', userSelect: 'none', '&:hover': { bgcolor: 'var(--kds-dolgu)' } }}>
                          <ArrowForwardIcon sx={{ fontSize: 12, color: 'text.disabled', transform: acik ? 'rotate(90deg)' : 'none', transition: 'transform .12s' }} />
                          <TableChartOutlinedIcon sx={{ fontSize: 14, color: kullanilan.includes(t.id) ? 'secondary.main' : 'text.disabled' }} />
                          <Typography noWrap sx={{ fontSize: 12, fontWeight: 700, flexGrow: 1 }}>{tableLabel}</Typography>
                          {/* M17: tabloyu ALANSIZ ekle — SELECT'e girmez, yalnız join'e katılır. */}
                          {!kullanilan.includes(t.id) && (
                            <IconButton size="small" title="Tabloyu alansız ekle (yalnız join'e katılır, sonuca kolon eklemez)"
                              onClick={(e) => {
                                e.stopPropagation();
                                setSadeceJoin((p) => [...p, t.id]);
                                sirayaEkle(t.id);
                                // (M36 sonrası tablolar zaten kapalı başlar — burada ayrıca
                                // kapatmaya gerek yok; kullanıcı açtıysa açık kalsın.)
                              }} sx={{ p: 0.15 }}>
                              <AddIcon sx={{ fontSize: 14 }} />
                            </IconButton>
                          )}
                          {/* Y14 — aynı tabloyu İKİNCİ kez: "getiren kişi" ile "danışman" aynı
                              tablodan gelir ama ayrı satırlardır; tek kimlikle ayırt edilemezdi. */}
                          <IconButton size="small"
                            title="Bu tabloyu ikinci kez ekle — aynı tablodan İKİ FARKLI satır lazımsa (ör. “getiren kişi” ve “danışman”)"
                            onClick={(e) => { e.stopPropagation(); tabloyuKopyala(t.id); }} sx={{ p: 0.15 }}>
                            <ContentCopyIcon sx={{ fontSize: 13 }} />
                          </IconButton>
                          <Typography sx={{ fontSize: 10, color: 'text.disabled', bgcolor: 'var(--kds-dolgu)', borderRadius: 999, px: 0.7 }}>{t.kolonlar.length}</Typography>
                        </Box>
                        {acik && gorunenKolonlar.map((k) => (
                          <Box key={k.hamAd} draggable
                            onDragStart={(e) => onDragStart(e, { tabloId: t.id, kolon: k.hamAd, rol: k.rol, etiket: k.dostcaAd || k.hamAd })}
                            onClick={() => addField(t.id, k.hamAd, k.rol, k.dostcaAd || k.hamAd)}
                            sx={{ display: 'flex', alignItems: 'center', gap: 0.8, pl: 3.4, pr: 1.25, py: 0.32, cursor: 'grab', '&:hover': { bgcolor: 'var(--kds-dolgu)' }, '&:hover .ekle': { opacity: 1 } }}>
                            <Box sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: roleColor[k.rol], flexShrink: 0 }} />
                            <Typography noWrap sx={{ fontSize: 12, flexGrow: 1 }}>{k.dostcaAd || k.hamAd}</Typography>
                            <AddIcon className="ekle" sx={{ fontSize: 14, color: 'text.disabled', opacity: 0, transition: 'opacity .12s' }} />
                          </Box>
                        ))}
                      </Box>
                    );
                  })}
                </Box>
                <Divider />
                {/* Türetilmiş alanlar & elle koşul — sürükle-bırak ekranından yönetilir */}
                <Box sx={{ px: 1.25, py: 0.8, display: 'flex', flexDirection: 'column', gap: 0.5 }}>
                  {bilesikler.map((b) => (
                    <Box key={b.anahtar} onClick={() => setBilesikDiyalog({ ...b, parcalar: b.parcalar.map((p) => ({ ...p })) })}
                      title="Düzenlemek için tıkla"
                      sx={{ display: 'flex', alignItems: 'center', gap: 0.5, fontSize: 11.5, cursor: 'pointer', bgcolor: 'var(--kds-tealdolgu)', border: '1px solid #bfe0e5', borderRadius: smallRadius, pl: 0.9, pr: 0.3, py: '1px', '&:hover': { borderColor: 'secondary.main' } }}>
                      <MergeTypeIcon sx={{ fontSize: 13, color: 'secondary.main' }} />
                      <Box component="span" sx={{ flexGrow: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        <b>{b.takmaAd.trim() || 'Birleşik'}</b> · {b.parcalar.filter((p) => p.tur === 'kolon').map((p) => p.kolon).join('+') || '—'}
                      </Box>
                      <IconButton size="small" onClick={(e) => { e.stopPropagation(); setBilesikler((p) => p.filter((x) => x.anahtar !== b.anahtar)); }} sx={{ p: 0.15 }} title="Kaldır"><CloseIcon sx={{ fontSize: 12 }} /></IconButton>
                    </Box>
                  ))}
                  <Box sx={{ display: 'flex', gap: 0.5 }}>
                    <Button size="small" startIcon={<MergeTypeIcon sx={{ fontSize: 14 }} />} sx={{ fontSize: 11, flexGrow: 1, justifyContent: 'flex-start' }}
                      onClick={() => setBilesikDiyalog({ anahtar: '', takmaAd: '', parcalar: [
                        { anahtar: uuid(), tur: 'kolon', tabloId: '', kolon: '', sabit: '' },
                        { anahtar: uuid(), tur: 'sabit', tabloId: '', kolon: '', sabit: ' ' },
                        { anahtar: uuid(), tur: 'kolon', tabloId: '', kolon: '', sabit: '' },
                      ] })}>
                      Birleşik kolon
                    </Button>
                  </Box>
                </Box>
                <Divider />
                <Box sx={{ display: 'flex', gap: 1.25, px: 1.5, py: 0.7, flexWrap: 'wrap' }}>
                  {(['Boyut', 'Olcu', 'Anahtar'] as ColumnRole[]).map((r) => (
                    <Box key={r} sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5, fontSize: 10, color: 'text.secondary' }}>
                      <Box sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: roleColor[r] }} />
                      {r === 'Boyut' ? 'Kırılım' : r === 'Olcu' ? 'Sayısal' : 'Anahtar'}
                    </Box>
                  ))}
                </Box>
              </Paper>

              {/* büyük ilişki kanvası */}
              <Paper variant="outlined" sx={{ flexGrow: 1, minWidth: 0, borderRadius: boxRadius, overflow: 'hidden', height: 'calc(100vh - 305px)', minHeight: 340, position: 'relative' }}>
                <Typography sx={{ position: 'absolute', zIndex: 5, top: 7, left: 12, fontSize: 10.5, color: 'text.disabled' }}>
                  İlişki kanvası — yalnız kullanılacak join çizilir · <b>çizgiye tıkla</b> = kolonları değiştir · yol/tür/koşul = <b>2 · Join Yolları</b>
                </Typography>
                {kullanilan.length === 0 ? (
                  <Box sx={{ height: '100%', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 1, bgcolor: 'var(--kds-zemin2)' }}>
                    <TableChartOutlinedIcon sx={{ fontSize: 40, color: 'text.disabled' }} />
                    <Typography sx={{ fontSize: 13.5, fontWeight: 600 }} color="text.secondary">Soldan alanları <Box component="span" sx={{ color: 'secondary.main' }}>buraya sürükle</Box></Typography>
                    <Typography sx={{ fontSize: 12 }} color="text.disabled">Tablolar düğüm olarak belirir; aralarındaki join'ler haritadan çizilir.</Typography>
                  </Box>
                ) : (
                  <ReactFlow
                    nodes={dugumler} edges={kenarlar} nodeTypes={dugumTurleri} edgeTypes={kenarTurleri}
                    onNodesChange={onNodeChange}
                    onInit={(akis) => { akisRef.current = akis; }}
                    fitView fitViewOptions={{ padding: 0.3, maxZoom: 1 }}
                    nodesConnectable={false} zoomOnScroll={false} panOnScroll
                    proOptions={{ hideAttribution: true }}>
                    <Background gap={16} size={1} />
                  </ReactFlow>
                )}
              </Paper>
            </Box>
          )}

          {/* ADIM 2 — Join Yolları: FROM kökü + tablo başına ebeveyn/tür/koşullar (test dönüşü #3+#9+#14) */}
          {adim === 1 && (
            <Paper variant="outlined" sx={{ borderRadius: boxRadius }}>
              <Box sx={{ px: 1.75, py: 1.4 }}>
                <SectionTitle ikon={<MergeTypeIcon sx={{ fontSize: 15 }} />} baslik="Join Yolları"
                  ek="sorgunun omurgası — hangi tablo neye, hangi kolonlarla, hangi türle bağlanır" />
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1.5, mt: 0.5 }}>
                  <Typography sx={{ fontSize: 12.5, fontWeight: 700 }} color="primary">FROM kökü</Typography>
                  <Select size="small" displayEmpty value={kokTabloId}
                    onChange={(e) => {
                      const v = e.target.value;
                      setKokTabloId(v);
                      // Kök bir ebeveyne bağlanamaz — varsa ayarını temizle.
                      if (v) setJoinAyarlari((p) => { if (!p[v]) return p; const n = { ...p }; delete n[v]; return n; });
                    }}
                    sx={{ minWidth: 220, fontSize: 12.5 }}>
                    <MenuItem value="" sx={{ fontSize: 12.5 }}><em>Otomatik — {tableName(otomatikKok)}</em></MenuItem>
                    {kullanilan.map((tid) => <MenuItem key={tid} value={tid} sx={{ fontSize: 12.5 }}>{tableName(tid)}</MenuItem>)}
                  </Select>
                  <Typography sx={{ fontSize: 11.5 }} color="text.disabled">
                    Genelde en çok tabloya bağlanan "olgu" tablosu (ör. Talepler) kök seçilir.
                  </Typography>
                </Box>
                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.8 }}>
                  {kullanilan.filter((tid) => tid !== etkinKok).map((tid, sira, satirlar) => {
                    const ayar = joinAyarlari[tid];
                    const oto = agac.get(tid);
                    // M18/M35: ▲▼ — satırı GÖRÜNEN listedeki komşusuyla değiştirir. (Eski hâli tam
                    // liste üzerinde çalışıyordu: komşu FROM kökü olduğunda takas sessizce iptal
                    // ediliyor, buton basılıyor ama satır kıpırdamıyordu.) Kök listede gösterilmez,
                    // sıralamaya da katılmaz; öncelik listesine her zaman BAŞTA yazılır.
                    const yerDegistir = (yon: -1 | 1) =>
                      setTabloSirasi(joinSirasiDegistir(kullanilan, etkinKok, tid, yon));
                    return (
                      <Paper key={tid} variant="outlined" sx={{ borderRadius: smallRadius, px: 1.25, py: 0.8, display: 'flex', flexDirection: 'column', gap: 0.6, bgcolor: ayar ? 'var(--kds-zemin2)' : undefined, borderColor: ayar ? '#9fd4db' : undefined }}>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                          <Box sx={{ display: 'flex', flexDirection: 'column', mr: -0.5 }}>
                            <IconButton size="small" disabled={sira === 0} onClick={() => yerDegistir(-1)} sx={{ p: 0, height: 14 }} title="Yukarı taşı">
                              <ArrowUpwardIcon sx={{ fontSize: 12 }} />
                            </IconButton>
                            <IconButton size="small" disabled={sira === satirlar.length - 1} onClick={() => yerDegistir(1)} sx={{ p: 0, height: 14 }} title="Aşağı taşı">
                              <ArrowDownwardIcon sx={{ fontSize: 12 }} />
                            </IconButton>
                          </Box>
                          <Typography sx={{ fontSize: 13, fontWeight: 700, minWidth: 140 }}>{tableName(tid)}</Typography>
                          <Typography sx={{ fontSize: 12 }} color="text.disabled">şuna bağlanır →</Typography>
                          <Select size="small" displayEmpty value={ayar?.ebeveynTabloId ?? ''}
                            onChange={(e) => setupSettings(tid, { ebeveynTabloId: e.target.value })}
                            sx={{ minWidth: 170, fontSize: 12.5 }}>
                            <MenuItem value="" sx={{ fontSize: 12.5 }}><em>Otomatik{oto ? ` — ${tableName(oto.ebeveyn)}` : ''}</em></MenuItem>
                            {kullanilan.filter((x) => x !== tid).map((x) =>
                              <MenuItem key={x} value={x} sx={{ fontSize: 12.5 }}>{tableName(x)}</MenuItem>)}
                          </Select>
                          <Select size="small" value={ayar?.tur ?? joinTurleri[tid] ?? 'Inner'}
                            disabled={!ayar && (!oto || oto.araMi)}
                            title={!ayar && oto?.araMi
                              ? 'Bu satır ara tablo üzerinden bağlanıyor; tür seçmek için önce ebeveyn tabloyu seç (ya da ara tabloyu + ile ekle).'
                              : undefined}
                            onChange={(e) => {
                              const tur = e.target.value as JoinType;
                              // M36: otomatik satırda YALNIZ tür ezilir — satır OTOMATİK KALIR
                              // (ebeveyn her değişiklikte yeniden hesaplanır). Eskiden ebeveyn de
                              // sabitlenip satır "ayarlı"ya dönüyordu; ebeveyn ara tablo olduğunda
                              // satır join'den tümüyle düşüyordu.
                              if (ayar) setupSettings(tid, { tur });
                              else setJoinTurleri((p) => ({ ...p, [tid]: tur }));
                            }}
                            sx={{ minWidth: 150, fontSize: 12.5 }}>
                            <MenuItem value="Inner" sx={{ fontSize: 12.5 }}>INNER — eşleşenler</MenuItem>
                            <MenuItem value="Left" sx={{ fontSize: 12.5 }}>LEFT — hepsi (+boşlar)</MenuItem>
                          </Select>
                          {ayar && (
                            <Box component="button" onClick={() => setJoinAyarlari((p) => { const n = { ...p }; delete n[tid]; return n; })}
                              sx={{ font: 'inherit', fontSize: 10.5, color: 'text.secondary', bgcolor: 'transparent', border: 'none', cursor: 'pointer', textDecoration: 'underline' }}>
                              otomatiğe dön
                            </Box>
                          )}
                          {!ayar && oto && !oto.araMi && <Typography sx={{ fontSize: 11 }} color="text.disabled">otomatik: {oto.etiket}</Typography>}
                          {!ayar && oto?.araMi && <Typography sx={{ fontSize: 11 }} color="text.disabled">otomatik: ara tablo üzerinden — {tableName(oto.ebeveyn)} ({oto.etiket})</Typography>}
                          {!ayar && !oto && <Typography sx={{ fontSize: 11, fontWeight: 700 }} color="error.main">⚠ join yolu bulunamadı — ebeveyn seç</Typography>}
                        </Box>
                        {ayar && (
                          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5, pl: 0.5 }}>
                            <Typography sx={{ fontSize: 10.5, fontWeight: 800, textTransform: 'uppercase', letterSpacing: 0.4 }} color="text.secondary">
                              Koşullar — boş bırakılırsa haritadaki ilişki kullanılır; birden çoğu VE ile bağlanır (bileşik join)
                            </Typography>
                            {ayar.kosullar.map((ks) => {
                              // M5: koşul, ana ebeveyn DIŞINDA sorgudaki başka bir tabloya da başvurabilir
                              // (ör. ServisStoklari: ServisId = Talepler.ServisId VE ParcaId = TalepParcalari.ParcaId).
                              const kosulTablo = ks.ebeveynTabloId ?? ayar.ebeveynTabloId;
                              return (
                              <Box key={ks.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.8, flexWrap: 'wrap' }}>
                                <Select size="small" displayEmpty value={ks.tabloKolon}
                                  onChange={(e) => setupSettings(tid, { kosullar: ayar.kosullar.map((x) => x.anahtar === ks.anahtar ? { ...x, tabloKolon: e.target.value } : x) })}
                                  sx={{ minWidth: 150, fontSize: 12.5 }}>
                                  <MenuItem value="" disabled><em>{tableName(tid)} kolonu…</em></MenuItem>
                                  {tabloKolonlari(tid).map((kk) => <MenuItem key={kk.hamAd} value={kk.hamAd} sx={{ fontSize: 12.5 }}>{kk.hamAd}</MenuItem>)}
                                </Select>
                                <Typography sx={{ fontSize: 14, fontWeight: 700, color: 'secondary.main' }}>=</Typography>
                                <Select size="small" value={kosulTablo}
                                  onChange={(e) => setupSettings(tid, { kosullar: ayar.kosullar.map((x) => x.anahtar === ks.anahtar
                                    ? { ...x, ebeveynTabloId: e.target.value === ayar.ebeveynTabloId ? undefined : e.target.value, ebeveynKolon: '' }
                                    : x) })}
                                  sx={{ minWidth: 140, fontSize: 12.5 }}>
                                  {kullanilan.filter((x) => x !== tid).map((x) =>
                                    <MenuItem key={x} value={x} sx={{ fontSize: 12.5 }}>{tableName(x)}{x === ayar.ebeveynTabloId ? ' (ebeveyn)' : ''}</MenuItem>)}
                                </Select>
                                <Select size="small" displayEmpty value={ks.ebeveynKolon}
                                  onChange={(e) => setupSettings(tid, { kosullar: ayar.kosullar.map((x) => x.anahtar === ks.anahtar ? { ...x, ebeveynKolon: e.target.value } : x) })}
                                  sx={{ minWidth: 150, fontSize: 12.5 }}>
                                  <MenuItem value="" disabled><em>{tableName(kosulTablo)} kolonu…</em></MenuItem>
                                  {tabloKolonlari(kosulTablo).map((kk) => <MenuItem key={kk.hamAd} value={kk.hamAd} sx={{ fontSize: 12.5 }}>{kk.hamAd}</MenuItem>)}
                                </Select>
                                <IconButton size="small" onClick={() => setupSettings(tid, { kosullar: ayar.kosullar.filter((x) => x.anahtar !== ks.anahtar) })} sx={{ p: 0.4 }}><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                              </Box>
                              );
                            })}
                            <Box>
                              <Button size="small" startIcon={<AddIcon />} sx={{ fontSize: 12 }}
                                onClick={() => setupSettings(tid, { kosullar: [...ayar.kosullar, { anahtar: uuid(), tabloKolon: '', ebeveynKolon: '' }] })}>
                                Koşul ekle
                              </Button>
                            </Box>
                            {/* Y13 — ON'a SABİT şart. LEFT join'de bu, filtreye yazmaktan farklıdır:
                                eşleşmeyi daraltır ama ana satırı ELEMEZ (kolonlar boş gelir). */}
                            <Box sx={{ mt: 0.8, pt: 0.8, borderTop: '1px dashed', borderColor: 'divider' }}>
                              <Typography sx={{ fontSize: 11, fontWeight: 800, textTransform: 'uppercase', letterSpacing: .4, color: 'text.secondary', mb: 0.4 }}>
                                Bu bağlantıya sabit şart {ayar.tur === 'Left' && <Box component="span" sx={{ color: 'secondary.main' }}>— LEFT'te filtreden FARKLI çalışır</Box>}
                              </Typography>
                              <Typography sx={{ fontSize: 11, mb: 0.5 }} color="text.disabled">
                                {ayar.tur === 'Left'
                                  ? 'Buraya yazılan şart yalnız EŞLEŞMEYİ daraltır — ana satır yine gelir, kolonlar boş kalır. Filtreye yazsaydınız satır tamamen elenirdi. Ör. “yalnız aktif adresi getir”.'
                                  : 'INNER bağlantıda buraya ya da filtreye yazmak aynı sonucu verir; LEFT’e çevirirseniz fark ortaya çıkar.'}
                              </Typography>
                              {(ayar.ekKosullar ?? []).map((f) => (
                                <Box key={f.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.4, flexWrap: 'wrap' }}>
                                  <Select size="small" displayEmpty value={f.kolon} sx={{ minWidth: 160, fontSize: 12 }}
                                    onChange={(e) => setupSettings(tid, { ekKosullar: (ayar.ekKosullar ?? []).map((x) => (x.anahtar === f.anahtar ? { ...x, tabloId: tid, kolon: e.target.value as string } : x)) })}>
                                    <MenuItem value="" disabled><em>{tableName(tid)} kolonu…</em></MenuItem>
                                    {tabloKolonlari(tid).map((kk) => <MenuItem key={kk.hamAd} value={kk.hamAd} sx={{ fontSize: 12.5 }}>{kk.hamAd}</MenuItem>)}
                                  </Select>
                                  <Select size="small" value={f.op} sx={{ minWidth: 96, fontSize: 12 }}
                                    onChange={(e) => setupSettings(tid, { ekKosullar: (ayar.ekKosullar ?? []).map((x) => (x.anahtar === f.anahtar ? { ...x, op: e.target.value as ComparisonOp } : x)) })}>
                                    {opOrder.map((o) => <MenuItem key={o} value={o} sx={{ fontSize: 12.5 }}>{opLabel[o]}</MenuItem>)}
                                  </Select>
                                  {!isValueless(f.op) && (
                                    <TextField size="small" placeholder="değer" value={f.d1} sx={{ width: 110 }}
                                      onChange={(e) => setupSettings(tid, { ekKosullar: (ayar.ekKosullar ?? []).map((x) => (x.anahtar === f.anahtar ? { ...x, d1: e.target.value } : x)) })} />
                                  )}
                                  <IconButton size="small" onClick={() => setupSettings(tid, { ekKosullar: (ayar.ekKosullar ?? []).filter((x) => x.anahtar !== f.anahtar) })} sx={{ p: 0.3 }}><CloseIcon sx={{ fontSize: 13 }} /></IconButton>
                                </Box>
                              ))}
                              <Button size="small" startIcon={<AddIcon />} sx={{ fontSize: 12 }}
                                onClick={() => setupSettings(tid, { ekKosullar: [...(ayar.ekKosullar ?? []), { anahtar: uuid(), tabloId: tid, kolon: '', op: 'Esit', d1: '', d2: '' }] })}>
                                Bağlantıya şart ekle
                              </Button>
                            </Box>
                          </Box>
                        )}
                      </Paper>
                    );
                  })}
                  {kullanilan.filter((tid) => tid !== etkinKok).length === 0 && (
                    <InfoNote>Tek tablo var — join gerekmez. 1. adımda başka tablodan alan ekleyince yollar burada görünür.</InfoNote>
                  )}
                </Box>
              </Box>
            </Paper>
          )}

          {/* ADIM 3 — kolonlar & ölçüler + filtre/sıra/limit */}
          {adim === 2 && (
            <Paper variant="outlined" sx={{ borderRadius: boxRadius }}>
              <Box sx={{ px: 1.75, py: 1.4 }}>
                <SectionTitle ikon={<ViewColumnOutlinedIcon sx={{ fontSize: 15 }} />} baslik="Kolonlar & Ölçüler"
                  ek="hangi alan, nasıl özetlensin, hangi adla görünsün" />
                {alanlar.length === 0 ? <InfoNote>1. adımda alan ekleyin.</InfoNote> : (
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.6 }}>
                    {alanlar.map((a) => (
                      <Box key={a.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.8, flexWrap: 'wrap' }}>
                        <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: roleColor[a.rol], flexShrink: 0 }} />
                        {/* aynı adlı kolonlar (iki tablodan "Ad") karışmasın: tablo adı her satırda */}
                        <Box sx={{ width: 170, minWidth: 0 }}>
                          <Typography noWrap sx={{ fontSize: 12.5, fontWeight: 600, lineHeight: 1.2 }} title={`${tableName(a.tabloId)} · ${a.kolon}`}>{a.etiket}</Typography>
                          <Typography noWrap sx={{ fontSize: 10, lineHeight: 1.1 }} color="text.disabled">{tableName(a.tabloId)}</Typography>
                        </Box>
                        <Select size="small" value={a.toplama} disabled={!!a.kova}
                          title={a.kova ? 'Tarih kırılımı bir gruplama kolonudur — üzerine toplama uygulanmaz' : undefined}
                          onChange={(e) => updateField(a.anahtar, { toplama: e.target.value as AggregationType })} sx={{ minWidth: 122, fontSize: 12.5 }}>
                          {aggOrder.map((t) => <MenuItem key={t} value={t} sx={{ fontSize: 12.5 }}>{aggLabel[t]}</MenuItem>)}
                        </Select>
                        {/* Y4 — tarih kırılımı: yalnız TARİH kolonlarında çıkar. Seçilince alan bir
                            boyuttur (toplama kapanır) ve GROUP BY'a indirgenmiş hâliyle girer. */}
                        {tarihTipiMi(kolonVeriTipi(a.tabloId, a.kolon)) && (
                          <Select size="small" displayEmpty value={a.kova ?? ''}
                            title="Tarihi yıla/aya/haftaya indirger — “aylık kırılım” için ayrı kolon üretmeye gerek kalmaz · ör. Sipariş Tarihi + “Ay” = aylık ciro"
                            onChange={(e) => {
                              const v = (e.target.value as string) as TarihKovasi | '';
                              updateField(a.anahtar, v ? { kova: v, toplama: 'Yok' } : { kova: undefined });
                            }}
                            renderValue={(v) => (v ? `⤵ ${kovaEtiketi(v as TarihKovasi)}` : 'kırılım yok')}
                            sx={{ minWidth: 168, fontSize: 12, color: a.kova ? 'secondary.main' : 'text.disabled' }}>
                            <MenuItem value="" sx={{ fontSize: 12 }}><em>kırılım yok (tam tarih)</em></MenuItem>
                            <ListSubheader sx={{ fontSize: 10.5, lineHeight: 2, fontWeight: 800, textTransform: 'uppercase', letterSpacing: .4 }}>
                              Dönem başlangıcı — kronolojik sıralanır
                            </ListSubheader>
                            {tarihKovalari.filter((k) => k.aile === 'baslangic')
                              .map((k) => <MenuItem key={k.deger} value={k.deger} sx={{ fontSize: 12 }}>{k.etiket}</MenuItem>)}
                            <ListSubheader sx={{ fontSize: 10.5, lineHeight: 2, fontWeight: 800, textTransform: 'uppercase', letterSpacing: .4 }}>
                              Dönem parçası — mevsimsellik (yıldan bağımsız)
                            </ListSubheader>
                            {tarihKovalari.filter((k) => k.aile === 'parca')
                              .map((k) => <MenuItem key={k.deger} value={k.deger} sx={{ fontSize: 12 }}>{k.etiket}</MenuItem>)}
                          </Select>
                        )}
                        <TextField size="small" placeholder="isim (ops.)" value={a.takmaAd} onChange={(e) => updateField(a.anahtar, { takmaAd: e.target.value })} sx={{ width: 160 }} />
                        <IconButton size="small" onClick={() => removeField(a.anahtar)} sx={{ p: 0.4 }} title="Alanı kaldır"><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                      </Box>
                    ))}
                    {/* Birleşik kolonlar da çıktı kolonudur — burada listelenir (test dönüşü #7);
                        parça/ad düzenlemesi aynı diyalogda yapılır. */}
                    {bilesikler.map((b) => (
                      <Box key={b.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.8, flexWrap: 'wrap' }}>
                        <MergeTypeIcon sx={{ fontSize: 15, color: 'secondary.main', flexShrink: 0, ml: '-3px' }} />
                        <Box sx={{ width: 170, minWidth: 0 }}>
                          <Typography noWrap sx={{ fontSize: 12.5, fontWeight: 600, lineHeight: 1.2 }}>{b.takmaAd.trim() || 'Birleşik kolon'}</Typography>
                          <Typography noWrap sx={{ fontSize: 10, lineHeight: 1.1 }} color="text.disabled">
                            {b.parcalar.map((p) => p.tur === 'sabit' ? (p.sabit === ' ' ? '␣' : `"${p.sabit}"`) : p.kolon).join(' + ')}
                          </Typography>
                        </Box>
                        <Button size="small" sx={{ fontSize: 11.5 }}
                          onClick={() => setBilesikDiyalog({ ...b, parcalar: b.parcalar.map((p) => ({ ...p })) })}>
                          Düzenle
                        </Button>
                        <IconButton size="small" onClick={() => setBilesikler((p) => p.filter((x) => x.anahtar !== b.anahtar))} sx={{ p: 0.4 }} title="Birleşik kolonu kaldır"><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                      </Box>
                    ))}
                    {/* Koşullu ölçüler (CASE WHEN) — test dönüşü #13: SUM(CASE WHEN Garanti=1 THEN 1 …) */}
                    {kosullular.map((ko) => (
                      <Box key={ko.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.8, flexWrap: 'wrap' }}>
                        <FunctionsIcon sx={{ fontSize: 15, color: 'warning.main', flexShrink: 0, ml: '-3px' }} />
                        <Box sx={{ width: 170, minWidth: 0 }}>
                          <Typography noWrap sx={{ fontSize: 12.5, fontWeight: 600, lineHeight: 1.2 }}>{ko.takmaAd.trim() || 'Koşullu ölçü'}</Typography>
                          <Typography noWrap sx={{ fontSize: 10, lineHeight: 1.1 }} color="text.disabled">
                            {aggLabel[ko.toplama]} · koşullu ({ko.kosullar.length} şart)
                          </Typography>
                        </Box>
                        <Button size="small" sx={{ fontSize: 11.5 }}
                          onClick={() => setKosulluDiyalog({ ...ko, kosullar: ko.kosullar.map((k) => ({ ...k })) })}>
                          Düzenle
                        </Button>
                        <IconButton size="small" onClick={() => setKosullular((p) => p.filter((x) => x.anahtar !== ko.anahtar))} sx={{ p: 0.4 }} title="Koşullu ölçüyü kaldır"><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                      </Box>
                    ))}
                    {/* M29: satır-düzeyi CASE kolonları — CASE WHEN … THEN … ELSE … END (özet yok) */}
                    {caseAlanlar.map((c) => (
                      <Box key={c.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.8, flexWrap: 'wrap' }}>
                        <FunctionsIcon sx={{ fontSize: 15, color: 'var(--kds-mor)', flexShrink: 0, ml: '-3px' }} />
                        <Box sx={{ width: 170, minWidth: 0 }}>
                          <Typography noWrap sx={{ fontSize: 12.5, fontWeight: 600, lineHeight: 1.2 }}>{c.takmaAd.trim() || 'CASE kolonu'}</Typography>
                          <Typography noWrap sx={{ fontSize: 10, lineHeight: 1.1 }} color="text.disabled">
                            CASE · {c.dallar.length} dal{c.varsayilanTur !== 'yok' ? ' + else' : ''}
                          </Typography>
                        </Box>
                        <Button size="small" sx={{ fontSize: 11.5 }}
                          onClick={() => setCaseDiyalog({ ...c, dallar: c.dallar.map((d) => ({ ...d, kosullar: d.kosullar.map((k) => ({ ...k })) })) })}>
                          Düzenle
                        </Button>
                        <IconButton size="small" onClick={() => setCaseAlanlar((p) => p.filter((x) => x.anahtar !== c.anahtar))} sx={{ p: 0.4 }} title="CASE kolonunu kaldır"><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                      </Box>
                    ))}
                    <Box>
                      <Button size="small" startIcon={<AddIcon />} disabled={tumKolonlar.length === 0} sx={{ fontSize: 12 }}
                        onClick={() => setKosulluDiyalog({ anahtar: '', toplama: 'Say', tabloId: '', kolon: '', takmaAd: '', kosullar: [] })}>
                        Koşullu ölçü
                      </Button>
                      <Button size="small" startIcon={<AddIcon />} disabled={tumKolonlar.length === 0} sx={{ fontSize: 12 }}
                        onClick={() => setCaseDiyalog({
                          anahtar: '', takmaAd: '',
                          dallar: [{ anahtar: uuid(), kosullar: [{ anahtar: uuid(), tabloId: '', kolon: '', op: 'Esit', d1: '', d2: '' }], sonucTur: 'sabit', tabloId: '', kolon: '', sabit: '' }],
                          varsayilanTur: 'yok', varsayilanTabloId: '', varsayilanKolon: '', varsayilanSabit: '',
                        })}>
                        CASE kolonu
                      </Button>
                    </Box>
                  </Box>
                )}
              </Box>
              <Divider />
              <Box sx={{ px: 1.75, py: 1.4 }}>
                <SectionTitle ikon={<TuneOutlinedIcon sx={{ fontSize: 15 }} />} baslik="Parametreler" ek="çalıştırırken sorulur (ör. tarih aralığı); filtreler değere ⟶ ile bağlanır" />
                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.6, mb: 1.75 }}>
                  {parametreler.map((p) => (
                    <Box key={p.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.6, flexWrap: 'wrap' }}>
                      <TextField size="small" placeholder="Etiket" value={p.etiket} onChange={(e) => updateParametre(p.anahtar, { etiket: e.target.value })} sx={{ width: 175 }} />
                      <Select size="small" value={p.tip} onChange={(e) => updateParametre(p.anahtar, { tip: e.target.value as ParametreTipi })} sx={{ minWidth: 130, fontSize: 12.5 }}>
                        {paramTipListe.map((t) => <MenuItem key={t} value={t} sx={{ fontSize: 12.5 }}>{paramTipEtiket[t]}</MenuItem>)}
                      </Select>
                      {p.tip === 'Liste'
                        ? <TextField size="small" placeholder="seçenekler: a, b, c" value={p.secenekler} onChange={(e) => updateParametre(p.anahtar, { secenekler: e.target.value })} sx={{ width: 180 }} />
                        : p.tip !== 'TarihAralik' && <TextField size="small" placeholder="varsayılan" value={p.varsayilan} onChange={(e) => updateParametre(p.anahtar, { varsayilan: e.target.value })} sx={{ width: 135 }} />}
                      <FormControlLabel sx={{ m: 0 }} control={<Checkbox size="small" checked={p.zorunlu} onChange={(e) => updateParametre(p.anahtar, { zorunlu: e.target.checked })} />} label={<Typography sx={{ fontSize: 12 }}>zorunlu</Typography>} />
                      <IconButton size="small" onClick={() => removeParametre(p.anahtar)} sx={{ p: 0.4 }} title="Parametreyi kaldır"><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                    </Box>
                  ))}
                  <Box><Button size="small" startIcon={<AddIcon />} onClick={addParametre} sx={{ fontSize: 12 }}>Parametre ekle</Button></Box>
                </Box>
                <SectionTitle ikon={<FilterAltOutlinedIcon sx={{ fontSize: 15 }} />} baslik="Filtreler"
                  ek="satırlar VE / VEYA ile bağlanır — soldan sağa uygulanır" />
                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.6 }}>
                  {filtreler.map((f, fi) => (
                    <Box key={f.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.8, flexWrap: 'wrap' }}>
                      {/* M36: bağlaç satırın BİR ÖNCEKİYLE ilişkisi; ilk satırda yoktur. */}
                      {fi === 0
                        ? <Box sx={{ width: 74, fontSize: 11.5, color: 'text.disabled', pl: 0.5 }}>nerede</Box>
                        : (
                          <Select size="small" value={f.baglac ?? 'Ve'}
                            onChange={(e) => updateFilter(f.anahtar, { baglac: e.target.value as LogicConnector })}
                            sx={{ width: 74, fontSize: 12, fontWeight: 700,
                              color: (f.baglac ?? 'Ve') === 'Veya' ? 'warning.dark' : 'secondary.main' }}>
                            <MenuItem value="Ve" sx={{ fontSize: 12.5 }}>VE</MenuItem>
                            <MenuItem value="Veya" sx={{ fontSize: 12.5 }}>VEYA</MenuItem>
                          </Select>
                        )}
                      <Select size="small" value={`${f.tabloId}|${f.kolon}`} onChange={(e) => { const [ti, ko] = (e.target.value as string).split('|'); updateFilter(f.anahtar, { tabloId: ti, kolon: ko }); }} sx={{ minWidth: 190, fontSize: 12.5 }}>
                        {tumKolonlar.map((k) => <MenuItem key={`${k.tabloId}|${k.kolon}`} value={`${k.tabloId}|${k.kolon}`} sx={{ fontSize: 12.5 }}>{k.etiket}</MenuItem>)}
                      </Select>
                      <Select size="small" value={f.op} onChange={(e) => updateFilter(f.anahtar, { op: e.target.value as ComparisonOp })} sx={{ minWidth: 112, fontSize: 12.5 }}>
                        {opOrder.map((o) => <MenuItem key={o} value={o} sx={{ fontSize: 12.5 }}>{opLabel[o]}</MenuItem>)}
                      </Select>
                      {/* Y5: değer dinamik tarih belirteciyse ham "$bugun-6ay" yerine ETİKETİ gösterilir
                          ve her çalıştırmada kaydığı yazılır; normal değerde eski metin kutusu. */}
                      {!isValueless(f.op) && !f.paramAnahtar && (dinamikTarihMi(f.d1)
                        ? (
                          <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5, bgcolor: 'var(--kds-tealdolgu)', border: '1px solid #bfe0e5', borderRadius: smallRadius, px: 0.9, py: '3px' }}
                            title="Her çalıştırmada o günün tarihine göre hesaplanır (kayan tarih)">
                            <EventRepeatIcon sx={{ fontSize: 14, color: 'secondary.main' }} />
                            <Box component="span" sx={{ fontSize: 11.5, fontWeight: 700, color: 'secondary.main' }}>{dinamikTarihEtiketi(f.d1)}</Box>
                            <IconButton size="small" title="Sabit değere dön" onClick={() => updateFilter(f.anahtar, { d1: '' })} sx={{ p: 0.15 }}><CloseIcon sx={{ fontSize: 12 }} /></IconButton>
                          </Box>
                        )
                        : <TextField size="small" placeholder={f.op === 'Icinde' ? 'a, b, c' : 'değer'} value={f.d1} onChange={(e) => updateFilter(f.anahtar, { d1: e.target.value })} sx={{ width: 140 }} />)}
                      {/* Aralığın BİTİŞ ucu — o da kayan tarih olabilir ("son 1 yıl": $bugun-1yil … $bugun) */}
                      {f.op === 'Arasinda' && !f.paramAnahtar && (
                        <Select size="small" displayEmpty title="Aralığın bitişi: sabit ya da kayan tarih"
                          value={dinamikTarihMi(f.d2) ? f.d2 : ''}
                          onChange={(e) => updateFilter(f.anahtar, { d2: e.target.value as string })}
                          renderValue={(v) => (v ? `🗓 ${dinamikTarihEtiketi(v as string)}` : 've …')}
                          sx={{ minWidth: 132, fontSize: 12, color: dinamikTarihMi(f.d2) ? 'secondary.main' : undefined }}>
                          <MenuItem value="" sx={{ fontSize: 12 }}><em>sabit değer (yaz)</em></MenuItem>
                          {dinamikTarihler.map((d) => <MenuItem key={d.deger} value={d.deger} sx={{ fontSize: 12 }}>🗓 {d.etiket}</MenuItem>)}
                        </Select>
                      )}
                      {f.op === 'Arasinda' && !f.paramAnahtar && !dinamikTarihMi(f.d2) && (
                        <TextField size="small" placeholder="ve" value={f.d2} onChange={(e) => updateFilter(f.anahtar, { d2: e.target.value })} sx={{ width: 100 }} />
                      )}
                      {/* Değer kaynağı: sabit · KAYAN TARİH (Y5) · çalıştırma parametresi */}
                      {!isValueless(f.op) && (
                        <Select size="small" displayEmpty title="Değer kaynağı: sabit, kayan tarih ya da parametre"
                          value={f.paramAnahtar ?? (dinamikTarihMi(f.d1) ? f.d1 : '')}
                          onChange={(e) => {
                            const v = e.target.value as string;
                            if (v.startsWith('$')) { updateFilter(f.anahtar, { d1: v, paramAnahtar: undefined }); return; }
                            const p = parametreler.find((x) => x.anahtar === v);
                            updateFilter(f.anahtar, {
                              paramAnahtar: v || undefined,
                              ...(dinamikTarihMi(f.d1) ? { d1: '' } : {}),
                              ...(p?.tip === 'TarihAralik' ? { op: 'Arasinda' as ComparisonOp } : {}),
                            });
                          }}
                          sx={{ minWidth: 150, fontSize: 12, color: f.paramAnahtar || dinamikTarihMi(f.d1) ? 'secondary.main' : undefined }}>
                          <MenuItem value="" sx={{ fontSize: 12 }}><em>sabit değer</em></MenuItem>
                          <ListSubheader sx={{ fontSize: 10.5, lineHeight: 2, fontWeight: 800, textTransform: 'uppercase', letterSpacing: .4 }}>
                            Kayan tarih — her çalıştırmada yeniden hesaplanır (ör. “son 6 ay” raporu kendi kendine kayar)
                          </ListSubheader>
                          {dinamikTarihler.map((d) => <MenuItem key={d.deger} value={d.deger} sx={{ fontSize: 12 }}>🗓 {d.etiket}</MenuItem>)}
                          {parametreler.length > 0 && (
                            <ListSubheader sx={{ fontSize: 10.5, lineHeight: 2, fontWeight: 800, textTransform: 'uppercase', letterSpacing: .4 }}>
                              Çalıştırma parametresi — kullanıcı değeri girer
                            </ListSubheader>
                          )}
                          {parametreler.map((p) => <MenuItem key={p.anahtar} value={p.anahtar} sx={{ fontSize: 12 }}>⟶ {p.etiket}</MenuItem>)}
                        </Select>
                      )}
                      <IconButton size="small" onClick={() => removeFilter(f.anahtar)} sx={{ p: 0.4 }} title="Filtreyi kaldır"><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                    </Box>
                  ))}
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                    <Button size="small" startIcon={<AddIcon />} onClick={addFilter} disabled={tumKolonlar.length === 0} sx={{ fontSize: 12 }}>Filtre ekle</Button>
                    {/* Elle SQL koşulu buraya taşındı (test dönüşü #6) — WHERE dünyası tek yerde */}
                    <Button size="small" startIcon={<CodeIcon sx={{ fontSize: 14 }} />} color={elleKosul.trim() ? 'secondary' : 'primary'}
                      sx={{ fontSize: 12, fontWeight: elleKosul.trim() ? 700 : 500 }}
                      onClick={() => setElleDiyalog(elleKosul)}>
                      SQL koşulu{elleKosul.trim() ? ` ✓ (${elleBaglac === 'Veya' ? 'VEYA' : 'VE'})` : ''}
                    </Button>
                  </Box>
                </Box>
              </Box>
              <Divider />
              <Box sx={{ px: 1.75, py: 1.4, display: 'flex', gap: 4, flexWrap: 'wrap', alignItems: 'flex-start' }}>
                {aggVar && (
                  <Box>
                    <SectionTitle ikon={<LayersOutlinedIcon sx={{ fontSize: 15 }} />} baslik="Gruplama"
                      ek={gruplamalar === null ? 'otomatik — düzenlenebilir' : 'elle'} />
                    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, alignItems: 'center' }}>
                      {etkinGruplama.length === 0 && <InfoNote>tek grup (kırılım yok)</InfoNote>}
                      {etkinGruplama.map((g, idx) => (
                        <Box key={`${g.tabloId}|${g.kolon}|${g.kova ?? ''}`} sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5, fontSize: 11.5, bgcolor: 'var(--kds-dolgu)', border: '1px solid', borderColor: 'divider', borderRadius: smallRadius, pl: 0.9, pr: 0.3, py: '1px' }}>
                          {tableName(g.tabloId)} · {findColumnName(g.tabloId, g.kolon)}
                          {/* Y4: kırılımlı gruplama ham kolondan farklıdır — çipte açıkça yazılır. */}
                          {g.kova && <Box component="span" sx={{ color: 'secondary.main', fontWeight: 700 }}>⤵ {kovaEtiketi(g.kova)}</Box>}
                          <IconButton size="small" onClick={() => removeGrouping(idx)} sx={{ p: 0.15 }} title="Gruplamadan çıkar"><CloseIcon sx={{ fontSize: 12 }} /></IconButton>
                        </Box>
                      ))}
                      <Select size="small" value="" displayEmpty
                        onChange={(e) => { const [ti, ko] = (e.target.value as string).split('|'); if (ti) addGrouping(ti, ko); }}
                        sx={{ fontSize: 11.5, minWidth: 130, '& .MuiSelect-select': { py: 0.3 } }}
                        renderValue={() => <Box component="span" sx={{ color: 'secondary.main', fontWeight: 600 }}>＋ kolon</Box>}>
                        {tumKolonlar
                          .filter((k) => !etkinGruplama.some((g) => g.tabloId === k.tabloId && g.kolon === k.kolon))
                          .map((k) => <MenuItem key={`${k.tabloId}|${k.kolon}`} value={`${k.tabloId}|${k.kolon}`} sx={{ fontSize: 12.5 }}>{k.etiket}</MenuItem>)}
                      </Select>
                      {gruplamalar !== null && (
                        <Box component="button" onClick={() => setGruplamalar(null)}
                          sx={{ font: 'inherit', fontSize: 10.5, color: 'text.secondary', bgcolor: 'transparent', border: 'none', cursor: 'pointer', textDecoration: 'underline' }}>otomatiğe dön</Box>
                      )}
                    </Box>
                  </Box>
                )}
                {/* S22 — katalog metriği: merkezi tanımlı ölçüt REFERANSLA girer. Formül burada
                    görünür ama düzenlenemez (kilit) — düzenlemenin yeri Metrikler kataloğu; tanım
                    değişince bu sorgu bir sonraki koşumda yeni tanımı kullanır. */}
                <Box>
                  <SectionTitle ikon={<FunctionsIcon sx={{ fontSize: 15 }} />} baslik="Katalog Metriği (Σ)"
                    ek="merkezî tanımlı ölçütü rapora ekler — formül katalogdan gelir, tanım değişince tüm raporlar yeni tanımı kullanır · gruplamayla birleşir (ör. Markalar·Ad + Toplam Ciro = marka başına ciro)" />
                  <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, alignItems: 'center' }}>
                    {metrikler.map((m) => {
                      const katalog = metrikKatalogu.find((k) => k.id === m.metrikId);
                      return (
                        <Box key={m.metrikId} title={katalog ? `${katalog.ifade} — formül katalogdan gelir` : 'Katalogda bulunamadı (silinmiş olabilir)'}
                          sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5, fontSize: 11.5, bgcolor: 'var(--kds-tealdolgu)', border: '1px solid #9fd4d9', borderRadius: smallRadius, pl: 0.9, pr: 0.3, py: '1px' }}>
                          <Box component="span" sx={{ fontWeight: 800, color: 'secondary.main' }}>Σ</Box>
                          <b>{m.ad || katalog?.ad || '…'}</b>
                          <Box component="span" sx={{ fontSize: 10.5, color: 'text.disabled', maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                            {katalog?.ifade ?? 'katalogda yok!'}
                          </Box>
                          <IconButton size="small" onClick={() => setMetrikler((p) => p.filter((x) => x.metrikId !== m.metrikId))} sx={{ p: 0.15 }} title="Metriği sorgudan çıkar"><CloseIcon sx={{ fontSize: 12 }} /></IconButton>
                        </Box>
                      );
                    })}
                    <Select size="small" value="" displayEmpty sx={{ fontSize: 12, minWidth: 150 }}
                      disabled={metrikKatalogu.filter((k) => k.aktif && !metrikler.some((m) => m.metrikId === k.id)).length === 0}
                      onChange={(e) => {
                        const k = metrikKatalogu.find((x) => x.id === e.target.value);
                        if (k) setMetrikler((p) => [...p, { metrikId: k.id, ad: k.ad }]);
                      }}>
                      <MenuItem value="" disabled sx={{ fontSize: 12 }}>Σ Metrik ekle…</MenuItem>
                      {metrikKatalogu.filter((k) => k.aktif && !metrikler.some((m) => m.metrikId === k.id)).map((k) => (
                        <MenuItem key={k.id} value={k.id} sx={{ fontSize: 12.5 }}>{k.ad}</MenuItem>
                      ))}
                    </Select>
                  </Box>
                </Box>
                {/* Y1 — hesaplanan (formül) kolon: kolonlar arası hesap (fark, oran, yüzde…) */}
                <Box>
                  <SectionTitle ikon={<CalculateOutlinedIcon sx={{ fontSize: 15 }} />} baslik="Hesaplanan Kolon (formül)"
                    ek="kolonlar arasında hesap yapıp yeni kolon üretir — fark, oran, yüzde, yuvarlama; SUM/COUNT yazarsan grup düzeyinde çalışır · ör. “{Faturalar.Tutar} − {Odemeler.Tutar}” = kalan borç" />
                  <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, alignItems: 'center' }}>
                    {hesaplananlar.map((h) => (
                      <Box key={h.anahtar} onClick={() => setHesapDiyalog({ ...h })} title="Düzenlemek için tıkla"
                        sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5, fontSize: 11.5, cursor: 'pointer', bgcolor: 'var(--kds-mordolgu)', border: '1px solid #d9cdf0', borderRadius: smallRadius, pl: 0.9, pr: 0.3, py: '1px', '&:hover': { borderColor: 'var(--kds-mor)' } }}>
                        <b>{h.takmaAd.trim() || 'Hesap'}</b>
                        <Box component="span" sx={{ fontSize: 10.5, color: 'text.disabled', maxWidth: 220, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                          {h.ifade}
                        </Box>
                        <IconButton size="small" onClick={(e) => { e.stopPropagation(); setHesaplananlar((p) => p.filter((x) => x.anahtar !== h.anahtar)); }} sx={{ p: 0.15 }} title="Hesaplanan kolonu kaldır"><CloseIcon sx={{ fontSize: 12 }} /></IconButton>
                      </Box>
                    ))}
                    <Button size="small" startIcon={<AddIcon />} disabled={tumKolonlar.length === 0} sx={{ fontSize: 12 }}
                      onClick={() => setHesapDiyalog({ anahtar: '', ifade: '', takmaAd: '' })}>
                      Formül ekle
                    </Button>
                  </Box>
                </Box>
                {/* Y6 — pencere kolonu: satırı KOMŞULARINA göre değerlendirir (sıra, önceki dönem,
                    kümülatif, bölüm toplamı). Hesaplanan kolondan ÖNCE gelir çünkü hesap, pencere
                    sonucuna atıf yapabilir ("bölüm toplamına oran"). */}
                <Box>
                  <SectionTitle ikon={<TimelineOutlinedIcon sx={{ fontSize: 15 }} />} baslik="Pencere Kolonu (satırı komşularıyla karşılaştır)"
                    ek="sıra no · önceki/sonraki dönemin değeri · kümülatif toplam · bölümün toplamı (paya oran) · hareketli ortalama — satır ELEMEZ, yeni kolon ekler · ör. “geçen ayki ciro”, “her markada ciro sırası”" />
                  <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, alignItems: 'center' }}>
                    {pencereler.map((p) => (
                      <Box key={p.anahtar} onClick={() => setPencereDiyalog({ ...p, bolum: [...p.bolum], sira: p.sira.map((s) => ({ ...s })) })}
                        title="Düzenlemek için tıkla"
                        sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5, fontSize: 11.5, cursor: 'pointer', bgcolor: 'var(--kds-dolgu)', border: '1px solid #c6d9ef', borderRadius: smallRadius, pl: 0.9, pr: 0.3, py: '1px', '&:hover': { borderColor: '#2f6df6' } }}>
                        <b>{p.takmaAd.trim() || pencereTanim(p.islev).etiket}</b>
                        <Box component="span" sx={{ fontSize: 10.5, color: 'text.disabled', maxWidth: 240, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                          {pencereTanim(p.islev).etiket}{p.deger.trim() ? ` · ${p.deger}` : ''}
                          {p.bolum.filter(Boolean).length > 0 ? ` · her ${p.bolum.filter(Boolean).join(', ')} içinde` : ''}
                        </Box>
                        <IconButton size="small" onClick={(e) => { e.stopPropagation(); setPencereler((x) => x.filter((y) => y.anahtar !== p.anahtar)); }} sx={{ p: 0.15 }} title="Pencere kolonunu kaldır"><CloseIcon sx={{ fontSize: 12 }} /></IconButton>
                      </Box>
                    ))}
                    <Button size="small" startIcon={<AddIcon />} disabled={tumKolonlar.length === 0} sx={{ fontSize: 12 }}
                      onClick={() => setPencereDiyalog({ anahtar: '', islev: 'SiraNo', deger: '', bolum: [], sira: [{ anahtar: uuid(), ifade: '', azalan: false }], sayi: '', takmaAd: '' })}>
                      Pencere kolonu ekle
                    </Button>
                  </Box>
                </Box>
                {/* Y2 — formül koşulu: sol taraf bir HESAP ya da bir ÇIKIŞ KOLONU atfı. Sıradan
                    filtre tek bir kolona bakar; buradaki koşul hesabın SONUCUNA bakar. WHERE mi
                    HAVING mi olacağını sistem seçer (toplama içeriyorsa gruplama sonrası). */}
                <Box>
                  <SectionTitle ikon={<RuleOutlinedIcon sx={{ fontSize: 15 }} />} baslik="Formül Filtresi (hesabın sonucuna göre ele)"
                    ek="tek kolona değil bir HESABA göre eler: “{Tutar} * 1,18 > 5000” ya da bir çıkış kolonuna atıf “[Kâr Marjı] ≥ 20”; SUM/COUNT içerirse gruplama sonrası (HAVING), içermezse satır bazında (WHERE) uygulanır — seçimi sistem yapar" />
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
                    {ifadeKosullari.map((k, ki) => (
                      <Box key={k.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.6, flexWrap: 'wrap' }}>
                        {ki === 0
                          ? <Box sx={{ width: 62, fontSize: 11.5, color: 'text.disabled', pl: 0.5 }}>şu ki</Box>
                          : (
                            <Select size="small" value={k.baglac ?? 'Ve'}
                              onChange={(e) => guncelleIfadeKosul(k.anahtar, { baglac: e.target.value as LogicConnector })}
                              sx={{ width: 62, fontSize: 11.5, fontWeight: 700 }}>
                              <MenuItem value="Ve" sx={{ fontSize: 12.5 }}>VE</MenuItem>
                              <MenuItem value="Veya" sx={{ fontSize: 12.5 }}>VEYA</MenuItem>
                            </Select>
                          )}
                        <TextField size="small" placeholder="formül — ör. {Siparisler.Tutar} * 1.18" value={k.ifade}
                          onChange={(e) => guncelleIfadeKosul(k.anahtar, { ifade: e.target.value })}
                          sx={{ flex: '1 1 260px', minWidth: 240, '& input': { fontFamily: 'ui-monospace, Menlo, Consolas, monospace', fontSize: 12 } }} />
                        {/* Yazım kolaylığı: kolon ve çıkış kolonu adlarını doğru biçimde EKLER. */}
                        <Select size="small" displayEmpty value="" sx={{ minWidth: 108, fontSize: 11.5, '& .MuiSelect-select': { py: 0.55 } }}
                          title="Formüle kolon ekle — {Tablo.Kolon} biçiminde yazılır"
                          renderValue={() => <Box component="span" sx={{ color: 'secondary.main', fontWeight: 600 }}>＋ kolon</Box>}
                          onChange={(e) => ifadeyeEkle(k.anahtar, `{${e.target.value as string}}`)}>
                          {tumKolonlar.map((c) => {
                            const t = detay?.tablolar.find((x) => x.id === c.tabloId);
                            return <MenuItem key={`${c.tabloId}|${c.kolon}`} value={`${t?.hamAd ?? ''}.${c.kolon}`} sx={{ fontSize: 12.5 }}>{c.etiket}</MenuItem>;
                          })}
                        </Select>
                        {cikisKolonlari.length > 0 && (
                          <Select size="small" displayEmpty value="" sx={{ minWidth: 122, fontSize: 11.5, '& .MuiSelect-select': { py: 0.55 } }}
                            title="Sonuçtaki bir kolona atıf — [Ad] biçiminde yazılır (hesaplanan, özet, ölçü kolonları)"
                            renderValue={() => <Box component="span" sx={{ color: 'var(--kds-mor)', fontWeight: 600 }}>＋ çıkış kolonu</Box>}
                            onChange={(e) => ifadeyeEkle(k.anahtar, `[${e.target.value as string}]`)}>
                            {cikisKolonlari.map((c) => (
                              <MenuItem key={c.ad} value={c.ad} sx={{ fontSize: 12.5 }}>
                                {c.ad} <Box component="span" sx={{ fontSize: 10.5, color: 'text.disabled', ml: 0.5 }}>({c.tur})</Box>
                              </MenuItem>
                            ))}
                          </Select>
                        )}
                        <Select size="small" value={k.op}
                          onChange={(e) => guncelleIfadeKosul(k.anahtar, { op: e.target.value as ComparisonOp })}
                          sx={{ minWidth: 96, fontSize: 12 }}>
                          {opOrder.filter((o) => o !== 'Icerir').map((o) => <MenuItem key={o} value={o} sx={{ fontSize: 12.5 }}>{opLabel[o]}</MenuItem>)}
                        </Select>
                        {!isValueless(k.op) && !k.paramAnahtar && (
                          <TextField size="small" placeholder={k.op === 'Icinde' ? 'a, b, c' : 'değer'} value={k.d1}
                            onChange={(e) => guncelleIfadeKosul(k.anahtar, { d1: e.target.value })} sx={{ width: 110 }} />
                        )}
                        {k.op === 'Arasinda' && !k.paramAnahtar && (
                          <TextField size="small" placeholder="ve" value={k.d2}
                            onChange={(e) => guncelleIfadeKosul(k.anahtar, { d2: e.target.value })} sx={{ width: 90 }} />
                        )}
                        {!isValueless(k.op) && parametreler.length > 0 && (
                          <Select size="small" displayEmpty title="Değer kaynağı: sabit ya da çalıştırma parametresi"
                            value={k.paramAnahtar ?? ''}
                            onChange={(e) => guncelleIfadeKosul(k.anahtar, { paramAnahtar: (e.target.value as string) || undefined })}
                            sx={{ minWidth: 140, fontSize: 12, color: k.paramAnahtar ? 'secondary.main' : undefined }}>
                            <MenuItem value="" sx={{ fontSize: 12 }}><em>sabit değer</em></MenuItem>
                            {parametreler.map((p) => <MenuItem key={p.anahtar} value={p.anahtar} sx={{ fontSize: 12 }}>⟶ {p.etiket}</MenuItem>)}
                          </Select>
                        )}
                        <IconButton size="small" onClick={() => setIfadeKosullari((p) => p.filter((x) => x.anahtar !== k.anahtar))} sx={{ p: 0.3 }} title="Formül koşulunu kaldır"><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                      </Box>
                    ))}
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                      <Button size="small" startIcon={<AddIcon />} disabled={tumKolonlar.length === 0} sx={{ fontSize: 12 }}
                        onClick={() => setIfadeKosullari((p) => [...p, { anahtar: uuid(), ifade: '', op: 'Buyuk', d1: '', d2: '', baglac: 'Ve' }])}>
                        Formül filtresi ekle
                      </Button>
                      {ifadeKosullari.length === 0 && (
                        <Typography sx={{ fontSize: 11, color: 'text.disabled' }}>
                          Sıradan filtre “kolon = değer” içindir; burası “hesabın sonucu şu olsun” içindir.
                        </Typography>
                      )}
                    </Box>
                  </Box>
                </Box>
                {/* Y3 — HAVING: gruplama SONRASI koşul. Filtre (WHERE) satırları gruplamadan ÖNCE
                    eler; buradaki koşul grubun ÖZETİNE bakar ("3'ten çok markası olan servisler"). */}
                {aggVar && (
                  <Box>
                    <SectionTitle ikon={<FilterAltOutlinedIcon sx={{ fontSize: 15 }} />}
                      baslik="Özet Filtresi (gruplama sonrası)"
                      ek="grubun ÖZETİNE göre eler — “3'ten çok markası olan servisler” gibi; SQL'de HAVING (filtreler ise satır bazlıdır, gruplamadan önce çalışır)" />
                    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
                      {ozetKosullar.map((k, ki) => (
                        <Box key={k.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.6, flexWrap: 'wrap' }}>
                          {ki === 0
                            ? <Box sx={{ width: 62, fontSize: 11.5, color: 'text.disabled', pl: 0.5 }}>grupta</Box>
                            : (
                              <Select size="small" value={k.baglac ?? 'Ve'}
                                onChange={(e) => setOzetKosullar((p) => p.map((x) => x.anahtar === k.anahtar ? { ...x, baglac: e.target.value as LogicConnector } : x))}
                                sx={{ width: 62, fontSize: 11.5, fontWeight: 700 }}>
                                <MenuItem value="Ve" sx={{ fontSize: 12.5 }}>VE</MenuItem>
                                <MenuItem value="Veya" sx={{ fontSize: 12.5 }}>VEYA</MenuItem>
                              </Select>
                            )}
                          <Select size="small" value={k.toplama}
                            onChange={(e) => setOzetKosullar((p) => p.map((x) => x.anahtar === k.anahtar ? { ...x, toplama: e.target.value as AggregationType } : x))}
                            sx={{ minWidth: 150, fontSize: 12 }}>
                            {(['Say', 'BenzersizSay', 'Topla', 'Ortalama', 'EnAz', 'EnCok'] as AggregationType[])
                              .map((t) => <MenuItem key={t} value={t} sx={{ fontSize: 12.5 }}>{aggLabel[t]}</MenuItem>)}
                          </Select>
                          <Select size="small" displayEmpty value={k.tabloId ? `${k.tabloId}|${k.kolon}` : ''}
                            onChange={(e) => { const [ti, ko] = (e.target.value as string).split('|'); setOzetKosullar((p) => p.map((x) => x.anahtar === k.anahtar ? { ...x, tabloId: ti ?? '', kolon: ko ?? '' } : x)); }}
                            sx={{ minWidth: 180, fontSize: 12 }}>
                            <MenuItem value="" disabled><em>Kolon…</em></MenuItem>
                            {tumKolonlar.map((c) => <MenuItem key={`${c.tabloId}|${c.kolon}`} value={`${c.tabloId}|${c.kolon}`} sx={{ fontSize: 12.5 }}>{c.etiket}</MenuItem>)}
                          </Select>
                          <Select size="small" value={k.op}
                            onChange={(e) => setOzetKosullar((p) => p.map((x) => x.anahtar === k.anahtar ? { ...x, op: e.target.value as ComparisonOp } : x))}
                            sx={{ minWidth: 96, fontSize: 12 }}>
                            {opOrder.filter((o) => !isValueless(o) && o !== 'Icerir')
                              .map((o) => <MenuItem key={o} value={o} sx={{ fontSize: 12.5 }}>{opLabel[o]}</MenuItem>)}
                          </Select>
                          <TextField size="small" placeholder="değer" value={k.d1}
                            onChange={(e) => setOzetKosullar((p) => p.map((x) => x.anahtar === k.anahtar ? { ...x, d1: e.target.value } : x))}
                            sx={{ width: 100 }} />
                          {k.op === 'Arasinda' && (
                            <TextField size="small" placeholder="ve" value={k.d2}
                              onChange={(e) => setOzetKosullar((p) => p.map((x) => x.anahtar === k.anahtar ? { ...x, d2: e.target.value } : x))}
                              sx={{ width: 90 }} />
                          )}
                          <IconButton size="small" onClick={() => setOzetKosullar((p) => p.filter((x) => x.anahtar !== k.anahtar))} sx={{ p: 0.3 }} title="Özet koşulunu kaldır"><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                        </Box>
                      ))}
                      <Box>
                        <Button size="small" startIcon={<AddIcon />} disabled={tumKolonlar.length === 0} sx={{ fontSize: 12 }}
                          onClick={() => setOzetKosullar((p) => [...p, { anahtar: uuid(), toplama: 'Say', tabloId: '', kolon: '', op: 'BuyukEsit', d1: '', d2: '', baglac: 'Ve' }])}>
                          Özet filtresi ekle (HAVING)
                        </Button>
                      </Box>
                    </Box>
                  </Box>
                )}
                <Box>
                  <SectionTitle ikon={<FunctionsIcon sx={{ fontSize: 15 }} />} baslik="Bağlı Tablo Özeti (alt sorgu)"
                    ek="SELECT'e alt sorgu eklemenizi sağlar — sonuca özet KOLON gelir (WHERE değildir); satır filtrelemez/çoğaltmaz — örnek: diyaloğun içinde" />
                  <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, alignItems: 'center' }}>
                    {ozetler.map((o) => (
                      <Box key={o.anahtar} onClick={() => setOzetDiyalog({ ...o, filtreler: o.filtreler.map((f) => ({ ...f })) })}
                        title="Düzenlemek için tıkla"
                        sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5, fontSize: 11.5, cursor: 'pointer', bgcolor: 'var(--kds-tealdolgu)', border: '1px solid #bfe0e5', borderRadius: smallRadius, pl: 0.9, pr: 0.3, py: '1px', '&:hover': { borderColor: 'secondary.main' } }}>
                        <b>{o.takmaAd.trim() || `${aggLabel[o.toplama]} ${o.kolon}`}</b>
                        &nbsp;— {tableName(o.bagTabloId)} başına {tableName(o.tabloId)}.{o.kolon}
                        {o.filtreler.length > 0 && <Box component="span" sx={{ fontSize: 10, color: 'secondary.main', fontWeight: 700 }}>ᶠ</Box>}
                        <IconButton size="small" onClick={(e) => { e.stopPropagation(); setOzetler((p) => p.filter((x) => x.anahtar !== o.anahtar)); }} sx={{ p: 0.15 }} title="Özeti kaldır"><CloseIcon sx={{ fontSize: 12 }} /></IconButton>
                      </Box>
                    ))}
                    <Button size="small" startIcon={<AddIcon />} disabled={kullanilan.length === 0}
                      onClick={() => setOzetDiyalog({ anahtar: '', tabloId: '', kolon: '', toplama: 'Say', bagTabloId: kullanilan[0] ?? '', takmaAd: '', filtreler: [] })}
                      sx={{ fontSize: 12 }}>Özet alan</Button>
                  </Box>
                </Box>
                <Box>
                  <SectionTitle ikon={<SwapVertIcon sx={{ fontSize: 15 }} />} baslik="Sıralama"
                    ek="seçili alanların yanı sıra HESAPLANAN / ÖZET / ÖLÇÜ kolonlarına göre de sıralar (ad verilmiş olması yeter) · ör. “Kâr Marjı”na göre azalan" />
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.6 }}>
                    {siralar.map((s) => (
                      <Box key={s.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.8 }}>
                        {/* Y2: değer ya bir alan anahtarı ya da "cikis:<Ad>" — ikinci grup alanlardan
                            gelmeyen çıkış kolonlarıdır (hesaplanan, özet, koşullu ölçü, CASE, birleşik). */}
                        <Select size="small" value={s.cikisAdi ? `cikis:${s.cikisAdi}` : s.alanAnahtar}
                          onChange={(e) => {
                            const v = e.target.value;
                            updateSort(s.anahtar, v.startsWith('cikis:')
                              ? { cikisAdi: v.slice(6), alanAnahtar: '' }
                              : { cikisAdi: undefined, alanAnahtar: v });
                          }}
                          sx={{ minWidth: 175, fontSize: 12.5 }}>
                          {alanlar.map((a) => <MenuItem key={a.anahtar} value={a.anahtar} sx={{ fontSize: 12.5 }}>{tableName(a.tabloId)} · {a.etiket}{a.toplama !== 'Yok' ? ` · ${aggLabel[a.toplama]}` : ''}</MenuItem>)}
                          {cikisKolonlari.length > 0 && (
                            <ListSubheader sx={{ fontSize: 10.5, lineHeight: 2, fontWeight: 800, textTransform: 'uppercase', letterSpacing: .4 }}>
                              Hesaplanan / özet kolonlar
                            </ListSubheader>
                          )}
                          {cikisKolonlari.map((c) => (
                            <MenuItem key={c.ad} value={`cikis:${c.ad}`} sx={{ fontSize: 12.5 }}>
                              {c.ad} <Box component="span" sx={{ fontSize: 10.5, color: 'text.disabled', ml: 0.5 }}>({c.tur})</Box>
                            </MenuItem>
                          ))}
                        </Select>
                        <Select size="small" value={s.azalan ? 'desc' : 'asc'} onChange={(e) => updateSort(s.anahtar, { azalan: e.target.value === 'desc' })} sx={{ minWidth: 96, fontSize: 12.5 }}>
                          <MenuItem value="asc" sx={{ fontSize: 12.5 }}>Artan ↑</MenuItem>
                          <MenuItem value="desc" sx={{ fontSize: 12.5 }}>Azalan ↓</MenuItem>
                        </Select>
                        <IconButton size="small" onClick={() => removeSort(s.anahtar)} sx={{ p: 0.4 }}><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                      </Box>
                    ))}
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                      <Button size="small" startIcon={<AddIcon />} onClick={addSort}
                        disabled={alanlar.length === 0 && cikisKolonlari.length === 0} sx={{ fontSize: 12 }}>Sıra ekle</Button>
                      {adsizCikisVar && (
                        <Typography sx={{ fontSize: 11, color: 'text.disabled' }}>
                          Adı olmayan hesaplanan/özet kolonlar listede görünmez — sıralayabilmek için o kolona bir ad verin.
                        </Typography>
                      )}
                    </Box>
                  </Box>
                </Box>
                <Box>
                  <SectionTitle ikon={<SpeedIcon sx={{ fontSize: 15 }} />} baslik="Satır Limiti & Sayfalama"
                    ek="limit sonucun TAMAMINI kırpar (ilk N satır); “atla” dolduğunda limit SAYFA BOYU olur — ör. 2. sayfa = atla 50, limit 50 · “her grupta ilk N” için aşağıdaki bölüm" />
                  <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1.2, flexWrap: 'wrap' }}>
                    <TextField size="small" type="number" label="Limit / sayfa boyu" value={limit}
                      onChange={(e) => setLimit(e.target.value)} sx={{ width: 150 }} />
                    {/* Y16: sunucu tarafı sayfalama (OFFSET/FETCH) — sıralama zorunludur. */}
                    <TextField size="small" type="number" label="Atla (kaçıncı satırdan)" value={atla}
                      onChange={(e) => setAtla(e.target.value)} sx={{ width: 190 }}
                      helperText={atla.trim() ? 'sayfalama açık — sıralama zorunlu' : 'boş = sayfalama yok'} />
                    {/* Y12: yinelenen satırları ele. */}
                    <FormControlLabel sx={{ mt: 0.5 }}
                      control={<Switch size="small" checked={tekille} onChange={(e) => setTekille(e.target.checked)} />}
                      label={<Typography sx={{ fontSize: 12.5 }}>Yinelenen satırları ele (DISTINCT) — <Box component="span" sx={{ color: 'text.disabled' }}>join yüzünden çoğalan listelerde işe yarar</Box></Typography>} />
                  </Box>
                </Box>
                {/* Y15 — birleşim (UNION): iki AYRI sorgunun sonucunu ALT ALTA getirir (join yan
                    yana getirir). Kolon sayıları uyuşmalı; sıralama/limit bütüne uygulanır. */}
                <Box>
                  <SectionTitle ikon={<PlaylistAddIcon sx={{ fontSize: 15 }} />} baslik="Birleşim (başka sorguyu ALT ALTA ekle)"
                    ek="join yan yana getirir, bu ALT ALTA — “bu yılki + arşivdeki kayıtlar tek listede” gibi · kolon SAYILARI eşit olmalı; sıralama/limit bu sorgudan gelir ve tamamına uygulanır" />
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.6 }}>
                    {birlesimler.map((b, i) => (
                      <Box key={b.id + i} sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.6, fontSize: 12, bgcolor: 'var(--kds-mavidolgu)', border: '1px solid #cfdcf5', borderRadius: smallRadius, px: 1, py: 0.3, alignSelf: 'flex-start' }}>
                        <Box component="span" sx={{ color: 'text.disabled', fontSize: 11 }}>{i + 2}. sorgu:</Box>
                        <b>{b.ad}</b>
                        <Box component="span" sx={{ fontSize: 10.5, color: 'text.disabled' }}>
                          {b.model.alanlar.length} kolon
                        </Box>
                        <IconButton size="small" onClick={() => setBirlesimler((p) => p.filter((_, xi) => xi !== i))} sx={{ p: 0.15 }} title="Birleşimden çıkar"><CloseIcon sx={{ fontSize: 12 }} /></IconButton>
                      </Box>
                    ))}
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                      <Select size="small" displayEmpty value="" sx={{ minWidth: 230, fontSize: 12 }}
                        onChange={async (e) => {
                          const rid = e.target.value as string;
                          if (!rid) return;
                          try {
                            const r = await getReport(rid);
                            if (!r.model) { setIslemHata('Seçilen rapor görsel tasarımla kurulmamış (ham SQL) — birleştirilemez.'); kadiHata(); return; }
                            setBirlesimler((p) => [...p, { id: rid, ad: r.ad, model: r.model! }]);
                          } catch (err) { setIslemHata((err as Error).message); kadiHata(); }
                        }}
                        renderValue={() => <Box component="span" sx={{ color: 'secondary.main', fontWeight: 600 }}>＋ kayıtlı rapor ekle</Box>}>
                        {raporListesi.filter((r) => r.id !== raporBilgi?.id).map((r) => (
                          <MenuItem key={r.id} value={r.id} sx={{ fontSize: 12.5 }}>{r.ad}</MenuItem>
                        ))}
                      </Select>
                      {birlesimler.length > 0 && (
                        <FormControlLabel
                          control={<Switch size="small" checked={birlesimTekille} onChange={(e) => setBirlesimTekille(e.target.checked)} />}
                          label={<Typography sx={{ fontSize: 11.5 }} color="text.secondary">Aynı satır iki listede varsa TEK göster (UNION) — kapalıyken hepsi gelir (UNION ALL)</Typography>} />
                      )}
                    </Box>
                  </Box>
                </Box>
                {/* Y11 — kaydı var/yok: ilişkili tabloda eşleşme arar. Join ile yapılsaydı çoklu
                    eşleşme satırı ÇOĞALTIRDI; "hiç yok" sorusu ise join'le sorulamaz. */}
                <Box>
                  <SectionTitle ikon={<PlaylistAddCheckIcon sx={{ fontSize: 15 }} />} baslik="Kaydı Var / Yok (ilişkili tabloda ara)"
                    ek="“siparişi olan müşteriler”, “hiç ödemesi olmayan faturalar” — o tablodan KOLON EKLEMEDEN, satır çoğaltmadan eler (SQL'de EXISTS / NOT EXISTS)" />
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
                    {varliklar.map((v, vi) => (
                      <Box key={v.anahtar} sx={{ display: 'flex', flexDirection: 'column', gap: 0.4, bgcolor: 'var(--kds-zemin2)', border: '1px solid', borderColor: 'divider', borderRadius: smallRadius, p: 0.8 }}>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.6, flexWrap: 'wrap' }}>
                          {vi === 0
                            ? <Box sx={{ width: 62, fontSize: 11.5, color: 'text.disabled', pl: 0.5 }}>ve ayrıca</Box>
                            : (
                              <Select size="small" value={v.baglac ?? 'Ve'} sx={{ width: 62, fontSize: 11.5, fontWeight: 700 }}
                                onChange={(e) => setVarliklar((p) => p.map((x) => (x.anahtar === v.anahtar ? { ...x, baglac: e.target.value as LogicConnector } : x)))}>
                                <MenuItem value="Ve" sx={{ fontSize: 12.5 }}>VE</MenuItem>
                                <MenuItem value="Veya" sx={{ fontSize: 12.5 }}>VEYA</MenuItem>
                              </Select>
                            )}
                          <Select size="small" value={v.yok ? 'yok' : 'var'} sx={{ minWidth: 210, fontSize: 12 }}
                            onChange={(e) => setVarliklar((p) => p.map((x) => (x.anahtar === v.anahtar ? { ...x, yok: e.target.value === 'yok' } : x)))}>
                            <MenuItem value="var" sx={{ fontSize: 12.5 }}>eşleşen kaydı OLANLAR</MenuItem>
                            <MenuItem value="yok" sx={{ fontSize: 12.5 }}>eşleşen kaydı OLMAYANLAR</MenuItem>
                          </Select>
                          <Select size="small" displayEmpty value={v.tabloId} sx={{ minWidth: 190, fontSize: 12 }}
                            onChange={(e) => setVarliklar((p) => p.map((x) => (x.anahtar === v.anahtar ? { ...x, tabloId: e.target.value as string, filtreler: [] } : x)))}>
                            <MenuItem value="" disabled><em>hangi tabloda…</em></MenuItem>
                            {(detay?.tablolar ?? []).map((t) => <MenuItem key={t.id} value={t.id} sx={{ fontSize: 12.5 }}>{t.dostcaAd || t.hamAd}</MenuItem>)}
                          </Select>
                          <IconButton size="small" onClick={() => setVarliklar((p) => p.filter((x) => x.anahtar !== v.anahtar))} sx={{ p: 0.3 }} title="Koşulu kaldır"><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                        </Box>
                        {v.tabloId && (
                          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.4, pl: 8 }}>
                            {v.filtreler.map((f) => (
                              <Box key={f.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.5, flexWrap: 'wrap' }}>
                                <Typography sx={{ fontSize: 11.5 }} color="text.disabled">şu şartla:</Typography>
                                <Select size="small" displayEmpty value={f.kolon} sx={{ minWidth: 150, fontSize: 12 }}
                                  onChange={(e) => setVarliklar((p) => p.map((x) => (x.anahtar === v.anahtar ? { ...x, filtreler: x.filtreler.map((y) => (y.anahtar === f.anahtar ? { ...y, kolon: e.target.value as string } : y)) } : x)))}>
                                  <MenuItem value="" disabled><em>kolon…</em></MenuItem>
                                  {(detay?.tablolar.find((t) => t.id === v.tabloId)?.kolonlar ?? [])
                                    .map((k) => <MenuItem key={k.hamAd} value={k.hamAd} sx={{ fontSize: 12.5 }}>{k.dostcaAd || k.hamAd}</MenuItem>)}
                                </Select>
                                <Select size="small" value={f.op} sx={{ minWidth: 96, fontSize: 12 }}
                                  onChange={(e) => setVarliklar((p) => p.map((x) => (x.anahtar === v.anahtar ? { ...x, filtreler: x.filtreler.map((y) => (y.anahtar === f.anahtar ? { ...y, op: e.target.value as ComparisonOp } : y)) } : x)))}>
                                  {opOrder.map((o) => <MenuItem key={o} value={o} sx={{ fontSize: 12.5 }}>{opLabel[o]}</MenuItem>)}
                                </Select>
                                {!isValueless(f.op) && (
                                  <TextField size="small" placeholder="değer" value={f.d1} sx={{ width: 110 }}
                                    onChange={(e) => setVarliklar((p) => p.map((x) => (x.anahtar === v.anahtar ? { ...x, filtreler: x.filtreler.map((y) => (y.anahtar === f.anahtar ? { ...y, d1: e.target.value } : y)) } : x)))} />
                                )}
                                {f.op === 'Arasinda' && (
                                  <TextField size="small" placeholder="ve" value={f.d2} sx={{ width: 90 }}
                                    onChange={(e) => setVarliklar((p) => p.map((x) => (x.anahtar === v.anahtar ? { ...x, filtreler: x.filtreler.map((y) => (y.anahtar === f.anahtar ? { ...y, d2: e.target.value } : y)) } : x)))} />
                                )}
                                <IconButton size="small" onClick={() => setVarliklar((p) => p.map((x) => (x.anahtar === v.anahtar ? { ...x, filtreler: x.filtreler.filter((y) => y.anahtar !== f.anahtar) } : x)))} sx={{ p: 0.25 }}><CloseIcon sx={{ fontSize: 13 }} /></IconButton>
                              </Box>
                            ))}
                            <Box>
                              <Button size="small" startIcon={<AddIcon />} sx={{ fontSize: 11.5 }}
                                onClick={() => setVarliklar((p) => p.map((x) => (x.anahtar === v.anahtar ? { ...x, filtreler: [...x.filtreler, { anahtar: uuid(), kolon: '', op: 'Esit', d1: '', d2: '' }] } : x)))}>
                                Aranan kayda şart ekle
                              </Button>
                              <Typography component="span" sx={{ fontSize: 11, ml: 1 }} color="text.disabled">
                                ör. “iptal edilmemiş” siparişi olanlar
                              </Typography>
                            </Box>
                          </Box>
                        )}
                      </Box>
                    ))}
                    <Box>
                      <Button size="small" startIcon={<AddIcon />} sx={{ fontSize: 12 }} disabled={(detay?.tablolar.length ?? 0) === 0}
                        onClick={() => setVarliklar((p) => [...p, { anahtar: uuid(), tabloId: '', yok: false, baglac: 'Ve', filtreler: [] }])}>
                        Var/yok koşulu ekle
                      </Button>
                      <Typography component="span" sx={{ fontSize: 11, ml: 1 }} color="text.disabled">
                        bağlantı kolonları Veri Haritası'ndaki ilişkiden bulunur
                      </Typography>
                    </Box>
                  </Box>
                </Box>
                {/* Y8 — ara küme (ikinci kat): sonucun ÜSTÜNDE yeniden kırılım/özet. Tek katta
                    yazılamayan "özetin özeti" soruları buradan çıkar. */}
                <Box>
                  <SectionTitle ikon={<LayersOutlinedIcon sx={{ fontSize: 15 }} />} baslik="Ara Küme (sonucun üstünde ikinci kat)"
                    ek="“her müşterinin toplamını bul, sonra bunların ORTALAMASINI al” gibi iki aşamalı sorular için — üstteki sonuç alt sorgu olur, burada onun üstünde yeniden gruplanır" />
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.7 }}>
                    <FormControlLabel sx={{ ml: 0 }}
                      control={<Switch size="small" checked={araKume.etkin} onChange={(e) => setAraKume((p) => ({ ...p, etkin: e.target.checked }))} />}
                      label={<Typography sx={{ fontSize: 12.5 }}>İkinci katı kullan</Typography>} />
                    {araKume.etkin && (
                      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.7, pl: 1, borderLeft: '2px solid', borderColor: 'divider' }}>
                        <Typography sx={{ fontSize: 11.5 }} color="text.secondary">
                          İkinci kat yalnız aşağıda seçtiklerinizi gösterir; üstteki diğer kolonlar sonuçtan düşer.
                        </Typography>
                        <Box>
                          <Typography sx={{ fontSize: 11, fontWeight: 800, textTransform: 'uppercase', letterSpacing: .4, color: 'text.secondary', mb: 0.4 }}>
                            İkinci katın kırılımı (boşsa tek satır döner) — ör. Şehir
                          </Typography>
                          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, alignItems: 'center' }}>
                            {araKume.gruplar.map((g, i) => (
                              <Box key={i} sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.4, fontSize: 11.5, bgcolor: 'var(--kds-dolgu)', border: '1px solid', borderColor: 'divider', borderRadius: smallRadius, pl: 0.9, pr: 0.3, py: '1px' }}>
                                {g}
                                <IconButton size="small" onClick={() => setAraKume((p) => ({ ...p, gruplar: p.gruplar.filter((_, xi) => xi !== i) }))} sx={{ p: 0.15 }}><CloseIcon sx={{ fontSize: 12 }} /></IconButton>
                              </Box>
                            ))}
                            <Select size="small" value="" displayEmpty sx={{ fontSize: 11.5, minWidth: 150, '& .MuiSelect-select': { py: 0.3 } }}
                              onChange={(e) => { const v = e.target.value as string; if (v) setAraKume((p) => ({ ...p, gruplar: [...p.gruplar, v] })); }}
                              renderValue={() => <Box component="span" sx={{ color: 'secondary.main', fontWeight: 600 }}>＋ çıkış kolonu</Box>}>
                              {cikisKolonlari.filter((c) => !araKume.gruplar.includes(c.ad))
                                .map((c) => <MenuItem key={c.ad} value={c.ad} sx={{ fontSize: 12.5 }}>{c.ad}</MenuItem>)}
                            </Select>
                          </Box>
                        </Box>
                        <Box>
                          <Typography sx={{ fontSize: 11, fontWeight: 800, textTransform: 'uppercase', letterSpacing: .4, color: 'text.secondary', mb: 0.4 }}>
                            İkinci katın özetleri — ör. Ortalama([Müşteri Toplamı])
                          </Typography>
                          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
                            {araKume.ozetler.map((o) => (
                              <Box key={o.anahtar} sx={{ display: 'flex', gap: 0.6, alignItems: 'center', flexWrap: 'wrap' }}>
                                <Select size="small" value={o.toplama} sx={{ minWidth: 170, fontSize: 12 }}
                                  onChange={(e) => setAraKume((p) => ({ ...p, ozetler: p.ozetler.map((x) => (x.anahtar === o.anahtar ? { ...x, toplama: e.target.value as AggregationType } : x)) }))}>
                                  {(['Ortalama', 'Topla', 'Say', 'BenzersizSay', 'EnAz', 'EnCok'] as AggregationType[])
                                    .map((t) => <MenuItem key={t} value={t} sx={{ fontSize: 12.5 }}>{aggLabel[t]}</MenuItem>)}
                                </Select>
                                <Select size="small" displayEmpty value={o.cikisAdi} sx={{ minWidth: 170, fontSize: 12 }}
                                  onChange={(e) => setAraKume((p) => ({ ...p, ozetler: p.ozetler.map((x) => (x.anahtar === o.anahtar ? { ...x, cikisAdi: e.target.value as string } : x)) }))}>
                                  <MenuItem value="" disabled><em>üstteki kolon…</em></MenuItem>
                                  {cikisKolonlari.map((c) => <MenuItem key={c.ad} value={c.ad} sx={{ fontSize: 12.5 }}>{c.ad}</MenuItem>)}
                                </Select>
                                <TextField size="small" placeholder="isim (ops.)" value={o.takmaAd} sx={{ width: 160 }}
                                  onChange={(e) => setAraKume((p) => ({ ...p, ozetler: p.ozetler.map((x) => (x.anahtar === o.anahtar ? { ...x, takmaAd: e.target.value } : x)) }))} />
                                <IconButton size="small" onClick={() => setAraKume((p) => ({ ...p, ozetler: p.ozetler.filter((x) => x.anahtar !== o.anahtar) }))} sx={{ p: 0.3 }}><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                              </Box>
                            ))}
                            <Box>
                              <Button size="small" startIcon={<AddIcon />} sx={{ fontSize: 12 }} disabled={cikisKolonlari.length === 0}
                                onClick={() => setAraKume((p) => ({ ...p, ozetler: [...p.ozetler, { anahtar: uuid(), toplama: 'Ortalama', cikisAdi: '', takmaAd: '' }] }))}>Özet ekle</Button>
                              {cikisKolonlari.length === 0 && (
                                <Typography component="span" sx={{ fontSize: 11, ml: 1 }} color="text.disabled">
                                  önce üstteki kolonlara ad verin — ikinci kat kolonları ADIYLA kullanır
                                </Typography>
                              )}
                            </Box>
                          </Box>
                        </Box>
                      </Box>
                    )}
                  </Box>
                </Box>
                {/* Y10 — ara toplam satırları: normalde her grup bir satırdır ve TOPLAM satırı yoktur. */}
                <Box>
                  <SectionTitle ikon={<FunctionsIcon sx={{ fontSize: 15 }} />} baslik="Ara Toplam / Genel Toplam Satırı"
                    ek="sonuca ÖZET SATIRLARI ekler — normalde tabloda “TOPLAM” satırı olmaz; buradan eklenir · ör. marka bazında ciro listesinin en sonunda “(Tümü) — genel toplam” satırı" />
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.6 }}>
                    <FormControlLabel sx={{ ml: 0 }}
                      control={<Switch size="small" checked={araToplam.etkin} onChange={(e) => setAraToplam((p) => ({ ...p, etkin: e.target.checked }))} />}
                      label={<Typography sx={{ fontSize: 12.5 }}>Toplam satırı ekle</Typography>} />
                    {araToplam.etkin && (
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.8, flexWrap: 'wrap', pl: 1, borderLeft: '2px solid', borderColor: 'divider' }}>
                        <Select size="small" value={araToplam.tur} sx={{ minWidth: 300, fontSize: 12 }}
                          onChange={(e) => setAraToplam((p) => ({ ...p, tur: e.target.value as AraToplamTuru }))}>
                          <MenuItem value="GenelToplam" sx={{ fontSize: 12.5 }}>Yalnız GENEL toplam (en sona tek satır)</MenuItem>
                          <MenuItem value="Kademeli" sx={{ fontSize: 12.5 }}>Kademeli — her kırılım düzeyi için ara toplam</MenuItem>
                        </Select>
                        <FormControlLabel
                          control={<Switch size="small" checked={araToplam.etiketle} onChange={(e) => setAraToplam((p) => ({ ...p, etiketle: e.target.checked }))} />}
                          label={<Typography sx={{ fontSize: 11.5 }} color="text.secondary">Özet satırlarındaki boş hücreleri etiketle (kırılım kolonları METNE döner)</Typography>} />
                        {araToplam.etiketle && (
                          <TextField size="small" label="Etiket" value={araToplam.etiket} sx={{ width: 130 }}
                            onChange={(e) => setAraToplam((p) => ({ ...p, etiket: e.target.value }))} />
                        )}
                      </Box>
                    )}
                  </Box>
                </Box>
                {/* Y9 — çapraz tablo: bir kolonun DEĞERLERİ sütun başlığına çıkar. */}
                <Box>
                  <SectionTitle ikon={<ViewColumnOutlinedIcon sx={{ fontSize: 15 }} />} baslik="Çapraz Tablo (değerleri sütuna çevir)"
                    ek="“aylar sütun olsun, şehirler satır” — seçtiğiniz kolonun DEĞERLERİ başlık olur, hücreye toplama gelir; kalan kolonlar satır eksenidir" />
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.6 }}>
                    <FormControlLabel sx={{ ml: 0 }}
                      control={<Switch size="small" checked={capraz.etkin} onChange={(e) => setCapraz((p) => ({ ...p, etkin: e.target.checked }))} />}
                      label={<Typography sx={{ fontSize: 12.5 }}>Çapraz tabloya çevir</Typography>} />
                    {capraz.etkin && (
                      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.6, pl: 1, borderLeft: '2px solid', borderColor: 'divider' }}>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.6, flexWrap: 'wrap' }}>
                          <Typography sx={{ fontSize: 12.5 }}>Sütun başlığı olacak kolon:</Typography>
                          <Select size="small" displayEmpty value={capraz.baslikKolonu} sx={{ minWidth: 170, fontSize: 12 }}
                            onChange={(e) => setCapraz((p) => ({ ...p, baslikKolonu: e.target.value as string }))}>
                            <MenuItem value="" disabled><em>kolon…</em></MenuItem>
                            {cikisKolonlari.map((c) => <MenuItem key={c.ad} value={c.ad} sx={{ fontSize: 12.5 }}>{c.ad}</MenuItem>)}
                          </Select>
                          <Typography sx={{ fontSize: 12.5 }}>hücre:</Typography>
                          <Select size="small" value={capraz.toplama} sx={{ minWidth: 150, fontSize: 12 }}
                            onChange={(e) => setCapraz((p) => ({ ...p, toplama: e.target.value as AggregationType }))}>
                            {(['Topla', 'Say', 'Ortalama', 'EnAz', 'EnCok'] as AggregationType[])
                              .map((t) => <MenuItem key={t} value={t} sx={{ fontSize: 12.5 }}>{aggLabel[t]}</MenuItem>)}
                          </Select>
                          <Select size="small" displayEmpty value={capraz.degerKolonu} sx={{ minWidth: 170, fontSize: 12 }}
                            onChange={(e) => setCapraz((p) => ({ ...p, degerKolonu: e.target.value as string }))}>
                            <MenuItem value="" disabled><em>değer kolonu…</em></MenuItem>
                            {cikisKolonlari.map((c) => <MenuItem key={c.ad} value={c.ad} sx={{ fontSize: 12.5 }}>{c.ad}</MenuItem>)}
                          </Select>
                        </Box>
                        <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 0.8, flexWrap: 'wrap' }}>
                          <TextField size="small" label="Sütuna dönecek değerler (virgülle)" value={capraz.basliklar}
                            placeholder="Ocak, Şubat, Mart" sx={{ flex: '1 1 320px', minWidth: 260 }}
                            onChange={(e) => setCapraz((p) => ({ ...p, basliklar: e.target.value }))} />
                          <Button size="small" variant="outlined" sx={{ fontSize: 12, mt: 0.4 }}
                            disabled={!capraz.baslikKolonu || basliklarYukleniyor}
                            onClick={basliklariGetir}>
                            {basliklarYukleniyor ? 'Getiriliyor…' : 'Başlıkları veriden getir'}
                          </Button>
                        </Box>
                        <Typography sx={{ fontSize: 11 }} color="text.disabled">
                          SQL'de çapraz sütunların listesi SABİT olmak zorundadır — bu yüzden başlıklar
                          burada durur. “Başlıkları veriden getir” sorguyu bir kez çalıştırıp o kolondaki
                          değerleri (ilk 50) buraya yazar; veri değişirse listeyi güncelleyin.
                        </Typography>
                      </Box>
                    )}
                  </Box>
                </Box>
                {/* Y7 — grup içi ilk N: satır limitinin BÖLÜM BAZLI karşılığı. Sıra numarası bir
                    pencere işlevidir ve WHERE'de kullanılamaz → backend sorguyu sarmalar. */}
                <Box>
                  <SectionTitle ikon={<FilterListIcon sx={{ fontSize: 15 }} />} baslik="Her Grupta İlk N (grup içi kırpma)"
                    ek="“her marka için en çok satan 3 ürün”, “her ilde en yeni 5 kayıt” — satır limitinden farkı: kırpma HER BÖLÜM için ayrı yapılır" />
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.7 }}>
                    <FormControlLabel sx={{ ml: 0 }}
                      control={<Switch size="small" checked={ilkN.etkin} onChange={(e) => setIlkN((p) => ({ ...p, etkin: e.target.checked }))} />}
                      label={<Typography sx={{ fontSize: 12.5 }}>Grup içi kırpmayı uygula</Typography>} />
                    {ilkN.etkin && (
                      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.7, pl: 1, borderLeft: '2px solid', borderColor: 'divider' }}>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.8, flexWrap: 'wrap' }}>
                          <Typography sx={{ fontSize: 12.5 }}>Her bölümden</Typography>
                          <TextField size="small" type="number" value={ilkN.adet} placeholder="3"
                            onChange={(e) => setIlkN((p) => ({ ...p, adet: e.target.value }))} sx={{ width: 84 }} />
                          <Typography sx={{ fontSize: 12.5 }}>satır kalsın</Typography>
                          <FormControlLabel sx={{ ml: 1 }}
                            control={<Switch size="small" checked={ilkN.esitleriDeAl} onChange={(e) => setIlkN((p) => ({ ...p, esitleriDeAl: e.target.checked }))} />}
                            label={<Typography sx={{ fontSize: 11.5 }} color="text.secondary">Sondaki beraberlikte eşitlerin hepsi kalsın (N'den çok satır dönebilir)</Typography>} />
                        </Box>
                        <Box>
                          <Typography sx={{ fontSize: 11, fontWeight: 800, textTransform: 'uppercase', letterSpacing: .4, color: 'text.secondary', mb: 0.4 }}>
                            Bölüm — “her … için” (boşsa tüm sonuçtan ilk N) — ör. Marka
                          </Typography>
                          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
                            {ilkN.bolum.map((b, i) => (
                              <Box key={i} sx={{ display: 'flex', gap: 0.5, alignItems: 'center' }}>
                                <IfadeGirisi deger={b} ipucu="ör. [Marka]" tumKolonlar={ifadeKolonlari} cikisKolonlari={cikisKolonlari}
                                  yaz={(v) => setIlkN((p) => ({ ...p, bolum: p.bolum.map((x, xi) => (xi === i ? v : x)) }))} />
                                <IconButton size="small" onClick={() => setIlkN((p) => ({ ...p, bolum: p.bolum.filter((_, xi) => xi !== i) }))} sx={{ p: 0.3 }}><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                              </Box>
                            ))}
                            <Box><Button size="small" startIcon={<AddIcon />} sx={{ fontSize: 12 }}
                              onClick={() => setIlkN((p) => ({ ...p, bolum: [...p.bolum, ''] }))}>Bölüm ekle</Button></Box>
                          </Box>
                        </Box>
                        <Box>
                          <Typography sx={{ fontSize: 11, fontWeight: 800, textTransform: 'uppercase', letterSpacing: .4, color: 'text.secondary', mb: 0.4 }}>
                            Neye göre “ilk”? (zorunlu) — ör. [Ciro] azalan
                          </Typography>
                          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
                            {ilkN.sira.map((s) => (
                              <Box key={s.anahtar} sx={{ display: 'flex', gap: 0.5, alignItems: 'center' }}>
                                <IfadeGirisi deger={s.ifade} ipucu="ör. [Ciro]" tumKolonlar={ifadeKolonlari} cikisKolonlari={cikisKolonlari}
                                  yaz={(v) => setIlkN((p) => ({ ...p, sira: p.sira.map((x) => (x.anahtar === s.anahtar ? { ...x, ifade: v } : x)) }))} />
                                <Select size="small" value={s.azalan ? 'desc' : 'asc'} sx={{ minWidth: 140, fontSize: 12 }}
                                  onChange={(e) => setIlkN((p) => ({ ...p, sira: p.sira.map((x) => (x.anahtar === s.anahtar ? { ...x, azalan: e.target.value === 'desc' } : x)) }))}>
                                  <MenuItem value="desc" sx={{ fontSize: 12.5 }}>Azalan ↓ (en büyük)</MenuItem>
                                  <MenuItem value="asc" sx={{ fontSize: 12.5 }}>Artan ↑ (en küçük)</MenuItem>
                                </Select>
                                <IconButton size="small" onClick={() => setIlkN((p) => ({ ...p, sira: p.sira.filter((x) => x.anahtar !== s.anahtar) }))} sx={{ p: 0.3 }}><DeleteOutlineIcon sx={{ fontSize: 16 }} /></IconButton>
                              </Box>
                            ))}
                            <Box><Button size="small" startIcon={<AddIcon />} sx={{ fontSize: 12 }}
                              onClick={() => setIlkN((p) => ({ ...p, sira: [...p.sira, { anahtar: uuid(), ifade: '', azalan: true }] }))}>Sıra ekle</Button></Box>
                          </Box>
                        </Box>
                      </Box>
                    )}
                  </Box>
                </Box>
              </Box>
            </Paper>
          )}

          {/* ADIM 4 — sonuç + üretilen SQL */}
          {adim === 3 && (
            <Box>
              <Box sx={{ display: 'flex', gap: 0.5, mb: '-1px', alignItems: 'flex-end' }}>
                <TabItem etkin={sekme === 'sonuc'} onClick={() => setSekme('sonuc')}>Sonuç</TabItem>
                <TabItem etkin={sekme === 'sql'} onClick={() => setSekme('sql')}>Üretilen SQL</TabItem>
                <Box sx={{ flexGrow: 1 }} />
                <Box sx={{ display: 'flex', gap: 1, mb: 0.75 }}>
                  <Button variant="outlined" startIcon={mesgul === 'sql' ? <CircularProgress size={14} /> : <CodeIcon />} onClick={() => void sqlGoster()} disabled={!!mesgul}>SQL'i Yenile</Button>
                  <Button variant="contained" startIcon={mesgul === 'calistir' ? <CircularProgress size={14} color="inherit" /> : <PlayArrowIcon />} onClick={() => void calistir()} disabled={!!mesgul}>Çalıştır</Button>
                  <Button variant="contained" color="secondary" startIcon={<SaveOutlinedIcon />} onClick={() => void openSave()} disabled={!!mesgul || alanlar.length === 0}>
                    {raporBilgi ? 'Raporu Güncelle' : 'Rapor Olarak Kaydet'}
                  </Button>
                </Box>
              </Box>
              {parametrelerRapor.length > 0 && (
                <Box sx={{ mb: 1.25, p: 1.5, border: '1px solid', borderColor: 'divider', borderRadius: boxRadius, bgcolor: 'var(--kds-zemin2)' }}>
                  <Typography sx={{ fontSize: 11.5, fontWeight: 700, color: 'text.secondary', mb: 1 }}>Parametre değerleri (test)</Typography>
                  <ParametrePanel parametreler={parametrelerRapor} degerler={parametreDegerleri} onChange={setParametreDegerleri} />
                </Box>
              )}
              <Paper variant="outlined" sx={{ borderRadius: boxRadius, borderTopLeftRadius: 0, overflow: 'hidden' }}>
                {sekme === 'sql' ? (
                  sql ? (
                    <Box>
                      <Box component="pre" sx={{ m: 0, p: 1.75, overflowX: 'auto', fontFamily: 'Consolas, ui-monospace, monospace', fontSize: 12, lineHeight: 1.6, bgcolor: 'var(--kds-zemin2)' }}>{sql.sql}</Box>
                      {Object.keys(sql.parametreler).length > 0 && (
                        <Box sx={{ px: 1.75, py: 0.8, borderTop: '1px solid', borderColor: 'divider', fontSize: 11.5 }}>
                          <b>Parametreler:</b> {Object.entries(sql.parametreler).map(([k, v]) => `${k} = ${JSON.stringify(v)}`).join('  ·  ')}
                        </Box>
                      )}
                    </Box>
                  ) : <EmptyState>{mesgul === 'sql' ? 'SQL üretiliyor…' : 'SQL üretilemedi — üstteki hatayı giderip "SQL\'i Yenile" ile tekrar deneyin.'}</EmptyState>
                ) : (
                  sonuc ? (
                    <>
                      {/* M33: belirgin yavaş koşumda tanı butonu */}
                      {sonuc.sureMs > yavasEsik && (
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, px: 1.5, py: 0.8, borderBottom: '1px solid', borderColor: 'divider', bgcolor: 'var(--kds-amberdolgu)' }}>
                          <Typography sx={{ fontSize: 12.5 }} color="text.secondary">
                            Sorgu <b>{Math.round(sonuc.sureMs / 1000)} sn</b> sürdü — beklenenden yavaş.
                          </Typography>
                          <SorunNeButonu tanila={tanila} />
                        </Box>
                      )}
                      <ResultTable sonuc={sonuc} />
                    </>
                  ) : <EmptyState>"Çalıştır" ile sonuçları burada gör.</EmptyState>
                )}
              </Paper>
            </Box>
          )}

          {/* alt gezinme */}
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mt: 1.5 }}>
            <Button startIcon={<ArrowBackIcon />} onClick={() => goToStep(adim - 1)} disabled={adim === 0} color="inherit">Geri</Button>
            {alanlar.length > 0 && (
              <Button startIcon={<RestartAltIcon sx={{ fontSize: 16 }} />} color="error"
                onClick={() => void sorguyuSifirla()} sx={{ fontSize: 12.5 }}>
                Sorguyu Sıfırla
              </Button>
            )}
            <Box sx={{ flexGrow: 1 }} />
            {adim === 0 && <Typography sx={{ fontSize: 12 }} color="text.secondary">{alanlar.length > 0 ? `${alanlar.length} alan seçildi` : 'Alan ekleyince devam edebilirsin'}</Typography>}
            {adim < 3 && (
              <Button variant="contained" endIcon={<ArrowForwardIcon />} onClick={() => goToStep(adim + 1)} disabled={alanlar.length === 0}>
                {`${adim + 2} · ${stepDefs[adim + 1].ad}`}
              </Button>
            )}
          </Box>

          {/* Join-kolon diyaloğu — kanvasta bir kenara (çizgiye) tıklayınca açılır. Bu iki tablonun
              hangi kolonlardan birleşeceğini elle seçtirir; haritada tanımlı ilişkiler "önerilen"
              olarak hızlı seçilebilir ama kullanıcı istediği kolon çiftini de kurabilir. */}
          {joinDiyalog && (() => { const jd = joinDiyalog; return (
            <JoinColumnDialog taslak={jd} detay={detay}
              oneriler={ciftIliskileri.get(jd.cift) ?? []}
              seciliMi={!!joinSecim[jd.cift]} tableName={tableName}
              change={(d) => setJoinDiyalog((p) => p && ({ ...p, ...d }))}
              close={() => setJoinDiyalog(null)}
              apply={() => {
                setJoinSecim((p) => ({ ...p, [jd.cift]: {
                  kaynakTabloId: jd.tidA, kaynakKolon: jd.kolA,
                  hedefTabloId: jd.tidB, hedefKolon: jd.kolB,
                } }));
                setJoinDiyalog(null);
              }}
              otomatige={() => {
                setJoinSecim((p) => { const n = { ...p }; delete n[jd.cift]; return n; });
                setJoinDiyalog(null);
              }} />
          ); })()}

          {/* Rapor Olarak Kaydet diyaloğu */}
          <Dialog open={kaydetAcik} onClose={() => setKaydetAcik(false)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { borderRadius: boxRadius } } }}>
            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 3, pt: 2 }}>
              <Typography variant="h6" color="primary">{raporBilgi ? 'Raporu Güncelle' : 'Rapor Olarak Kaydet'}</Typography>
              <IconButton size="small" onClick={() => setKaydetAcik(false)}><CloseIcon fontSize="small" /></IconButton>
            </Box>
            <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.75 }}>
              <Box>
                <DialogLabel>Rapor adı</DialogLabel>
                <TextField size="small" fullWidth autoFocus value={kAd} onChange={(e) => setKAd(e.target.value)} placeholder="ör. Şehir bazında satış" />
              </Box>
              <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 1.5 }}>
                <Box>
                  <DialogLabel>Klasör</DialogLabel>
                  <Select size="small" fullWidth displayEmpty value={kKlasor} onChange={(e) => setKKlasor(e.target.value)}>
                    <MenuItem value=""><em>Klasörsüz</em></MenuItem>
                    {klasorler.map((k) => <MenuItem key={k.id} value={k.id}>{k.ad}</MenuItem>)}
                    <MenuItem value="__yeni">＋ Yeni klasör…</MenuItem>
                  </Select>
                  {kKlasor === '__yeni' && (
                    <TextField size="small" fullWidth sx={{ mt: 0.75 }} placeholder="Yeni klasör adı" value={kYeniKlasor} onChange={(e) => setKYeniKlasor(e.target.value)} />
                  )}
                </Box>
                <Box>
                  <DialogLabel>Paylaşım</DialogLabel>
                  {/* S27: Özel · Ekip · Belirli roller — rol çipleri ortak seçiciden. */}
                  <PaylasimSecici paylasim={kPaylasim} roller={kPaylasimRolleri}
                    degisti={(p, r) => { setKPaylasim(p); setKPaylasimRolleri(r); }} />
                </Box>
              </Box>
              <Box>
                <DialogLabel>Görselleştirme</DialogLabel>
                <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 0.75 }}>
                  {(Object.keys(vizDisplay) as VisualizationType[]).map((t) => {
                    const g = vizDisplay[t];
                    const secili = kTur === t;
                    return (
                      <Box key={t} onClick={() => setKTur(t)}
                        sx={{ border: '1.5px solid', borderColor: secili ? 'secondary.main' : 'divider', borderRadius: smallRadius, py: 0.9, textAlign: 'center', cursor: 'pointer', bgcolor: secili ? 'var(--kds-tealdolgu)' : 'var(--kds-zemin2)' }}>
                        <Box sx={{ color: secili ? g.renk : 'text.disabled', display: 'flex', justifyContent: 'center' }}>{g.ikon}</Box>
                        <Typography sx={{ fontSize: 9.5, fontWeight: 700, mt: 0.4, color: secili ? 'secondary.main' : 'text.secondary' }}>{g.ad}</Typography>
                      </Box>
                    );
                  })}
                </Box>
              </Box>
            </DialogContent>
            <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1, px: 3, pb: 2 }}>
              <Button color="inherit" onClick={() => setKaydetAcik(false)}>İptal</Button>
              <Button variant="contained" onClick={() => void saveReport()}
                disabled={!kAd.trim() || kaydediyor || (kKlasor === '__yeni' && !kYeniKlasor.trim())}
                startIcon={kaydediyor ? <CircularProgress size={14} color="inherit" /> : <SaveOutlinedIcon />}>
                Kaydet
              </Button>
            </Box>
          </Dialog>
        </>
      )}
      {/* İlişkili Özet Alan diyaloğu — farklı tablodan, kendi filtreli özet kolon tanımı.
          Bağ adayları: kanvasta kullanılan VE özet tablosuyla haritada ilişkisi olan tablolar. */}
      {ozetDiyalog && (() => { const oz = ozetDiyalog; return (
        <SummaryDialog taslak={oz} detay={detay} kullanilan={kullanilan} tableName={tableName}
          change={(d) => setOzetDiyalog((p) => p && ({ ...p, ...d }))}
          close={() => setOzetDiyalog(null)}
          apply={() => {
            setOzetler((p) => oz.anahtar
              ? p.map((x) => (x.anahtar === oz.anahtar ? oz : x))
              : [...p, { ...oz, anahtar: uuid() }]);
            setOzetDiyalog(null);
          }} />
      ); })()}

      {/* Koşullu Ölçü diyaloğu — AGG(CASE WHEN koşullar THEN değer …) yapısal kurulum */}
      {kosulluDiyalog && (() => { const ko = kosulluDiyalog; return (
        <ConditionalDialog taslak={ko} tumKolonlar={tumKolonlar}
          change={(d) => setKosulluDiyalog((p) => p && ({ ...p, ...d }))}
          close={() => setKosulluDiyalog(null)}
          apply={() => {
            setKosullular((p) => ko.anahtar
              ? p.map((x) => (x.anahtar === ko.anahtar ? ko : x))
              : [...p, { ...ko, anahtar: uuid() }]);
            setKosulluDiyalog(null);
          }} />
      ); })()}

      {/* Y1 Hesaplanan Kolon diyaloğu — SELECT'e formül ({Tablo.Kolon} yer tutucularıyla) */}
      {hesapDiyalog && (() => { const h = hesapDiyalog; return (
        <HesapDialog taslak={h}
          tumKolonlar={tumKolonlar.map((k) => ({
            ...k,
            // Formüle {Tablo.Kolon} olarak girer — backend haritadan çözer (ham ad tercih edilir).
            yerTutucu: `{${detay?.tablolar.find((t) => t.id === k.tabloId)?.hamAd ?? ''}.${k.kolon}}`,
          }))}
          change={(d) => setHesapDiyalog((p) => p && ({ ...p, ...d }))}
          close={() => setHesapDiyalog(null)}
          apply={() => {
            setHesaplananlar((p) => h.anahtar
              ? p.map((x) => (x.anahtar === h.anahtar ? h : x))
              : [...p, { ...h, anahtar: uuid() }]);
            setHesapDiyalog(null);
          }} />
      ); })()}

      {/* Y6 Pencere Kolonu diyaloğu — OVER(...) kolonları */}
      {pencereDiyalog && (() => { const pd = pencereDiyalog; return (
        <PencereDialog taslak={pd}
          tumKolonlar={tumKolonlar.map((k) => ({
            ...k,
            yerTutucu: `{${detay?.tablolar.find((t) => t.id === k.tabloId)?.hamAd ?? ''}.${k.kolon}}`,
          }))}
          cikisKolonlari={cikisKolonlari}
          change={(d) => setPencereDiyalog((p) => p && ({ ...p, ...d }))}
          close={() => setPencereDiyalog(null)}
          apply={() => {
            setPencereler((p) => pd.anahtar
              ? p.map((x) => (x.anahtar === pd.anahtar ? pd : x))
              : [...p, { ...pd, anahtar: uuid() }]);
            setPencereDiyalog(null);
          }} />
      ); })()}

      {/* M29 CASE Kolonu diyaloğu — satır-düzeyi CASE WHEN … THEN … ELSE … END */}
      {caseDiyalog && (() => { const ca = caseDiyalog; return (
        <CaseDialog taslak={ca} tumKolonlar={tumKolonlar}
          change={(d) => setCaseDiyalog((p) => p && ({ ...p, ...d }))}
          close={() => setCaseDiyalog(null)}
          apply={() => {
            setCaseAlanlar((p) => ca.anahtar
              ? p.map((x) => (x.anahtar === ca.anahtar ? ca : x))
              : [...p, { ...ca, anahtar: uuid() }]);
            setCaseDiyalog(null);
          }} />
      ); })()}

      {/* Birleşik Kolon diyaloğu — kolonlar + sabit metin ayraçları tek çıktı kolonunda */}
      {bilesikDiyalog && (() => { const bl = bilesikDiyalog; return (
        <CompositeDialog taslak={bl} detay={detay}
          change={(d) => setBilesikDiyalog((p) => p && ({ ...p, ...d }))}
          close={() => setBilesikDiyalog(null)}
          apply={() => {
            setBilesikler((p) => bl.anahtar
              ? p.map((x) => (x.anahtar === bl.anahtar ? bl : x))
              : [...p, { ...bl, anahtar: uuid() }]);
            setBilesikDiyalog(null);
          }} />
      ); })()}

      {/* Elle SQL koşulu diyaloğu — WHERE'e serbest koşul (alt sorgu dahil) */}
      {elleDiyalog !== null && (
        <RawConditionDialog taslak={elleDiyalog} baglac={elleBaglac}
          kaldirilebilir={!!elleKosul.trim()}
          change={setElleDiyalog} baglacDegistir={setElleBaglac}
          close={() => setElleDiyalog(null)}
          apply={() => { setElleKosul(elleDiyalog); setElleDiyalog(null); }}
          kaldir={() => { setElleKosul(''); setElleDiyalog(null); }} />
      )}

      {confirmDialog}
      {/* Sorgu koşarken hafif gösterge: içerik görünür kalır, etkileşim kilitlenir */}
      {mesgul && <RunningIndicator mesaj={mesgul === 'sql' ? 'SQL’i üretiyorum…' : 'Sorguyu çalıştırıyorum…'} />}
    </Box>
  );
}
