import type { ReactNode } from 'react';
import HomeIcon from '@mui/icons-material/Home';
import TuneOutlinedIcon from '@mui/icons-material/TuneOutlined';
import DashboardIcon from '@mui/icons-material/Dashboard';
import AssessmentIcon from '@mui/icons-material/Assessment';
import FunctionsIcon from '@mui/icons-material/Functions';
import AccountTreeIcon from '@mui/icons-material/AccountTree';
import CodeIcon from '@mui/icons-material/Code';
import SchemaIcon from '@mui/icons-material/Schema';
import StorageIcon from '@mui/icons-material/Storage';
import CloudSyncIcon from '@mui/icons-material/CloudSync';
import PeopleIcon from '@mui/icons-material/People';
import AdminPanelSettingsIcon from '@mui/icons-material/AdminPanelSettings';
import BusinessIcon from '@mui/icons-material/Business';
import HistoryIcon from '@mui/icons-material/History';
import InsightsIcon from '@mui/icons-material/Insights';
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome';
import ScheduleSendIcon from '@mui/icons-material/ScheduleSend';
import NotificationsActiveIcon from '@mui/icons-material/NotificationsActive';
import ShieldIcon from '@mui/icons-material/Shield';
import IntegrationInstructionsIcon from '@mui/icons-material/IntegrationInstructions';
import RouterIcon from '@mui/icons-material/Router';
import { Roles } from '../identity/roles';

const GA = [Roles.Gelistirici, Roles.Admin];
const A = [Roles.Admin];

export type AltOge = { yol: string; ad: string; aciklama?: string };
export type MenuOgesi = { yol: string; ad: string; ikon: ReactNode; aciklama?: string; roller?: string[]; alt?: AltOge[] };
export type MenuGrubu = { baslik: string; ogeler: MenuOgesi[] };

/** Sol menü — gruplu (grup başlıklı) düzen (05-menu-arastirma §6 görev tanımı). roller yoksa
 * herkese açık; varsa yalnız o rollerden birine sahip kullanıcı görür (grup, görünür öğesi
 * kalmazsa başlığıyla gizlenir). aciklama = rayda tooltip; her rol yalnız kendi öğelerinin
 * açıklamasını görür. Gerçek yetki backend'de; bu UI temizliği. */
export const menu: MenuGrubu[] = [
  {
    baslik: 'Çalışma Alanı',
    ogeler: [
      { yol: '/', ad: 'Ana Sayfa', ikon: <HomeIcon />, aciklama: 'Kişisel açılış: kayıtlı panoların, son sorguların ve genel KPI özeti.' },
      { yol: '/soru-sor', ad: 'Soru Sor', ikon: <AutoAwesomeIcon />, aciklama: 'Türkçe soru sor; YEREL model tanımlı metriklere dayalı sorgu kurar — veri dışarı çıkmaz.' },
      { yol: '/panolar', ad: 'Panolar', ikon: <DashboardIcon />, aciklama: 'Dashboard listesi/galerisi — aç, oluştur, paylaş, favorile.' },
      {
        yol: '/raporlar', ad: 'Raporlar', ikon: <AssessmentIcon />,
        aciklama: 'Kayıtlı raporlar ve organizasyon (klasör, favori, paylaşılan).',
        alt: [
          { yol: '/raporlar', ad: 'Tüm Raporlar', aciklama: 'Erişebildiğin tüm raporlar (tablo).' },
          { yol: '/raporlar/klasorler', ad: 'Klasörlerim', aciklama: 'Rapor klasör ağacın.' },
          { yol: '/raporlar/favoriler', ad: 'Favorilerim', aciklama: 'Sabitlediğin raporlar.' },
          { yol: '/raporlar/paylasilan', ad: 'Benimle Paylaşılanlar', aciklama: 'Başkalarının sana paylaştığı raporlar.' },
        ],
      },
      { yol: '/metrikler', ad: 'Metrikler', ikon: <FunctionsIcon />, aciklama: 'Merkezi ölçüt kataloğu — bir kez tanımla, her yerde aynı kullan.' },
    ],
  },
  {
    baslik: 'Oluştur',
    ogeler: [
      { yol: '/tasarimci', ad: 'Sorgu Tasarımcısı', ikon: <AccountTreeIcon />, roller: GA, aciklama: 'Sürükle-bırak ile SQL yazmadan sorgu tasarla.' },
      { yol: '/sql', ad: 'SQL Editörü', ikon: <CodeIcon />, roller: GA, aciklama: 'Ham SQL yaz (Monaco) — yalnız SELECT çalışır.' },
      { yol: '/veri-haritalari', ad: 'Veri Haritaları', ikon: <SchemaIcon />, roller: GA, aciklama: 'Ham tabloları dostça ad, ilişki (join) ve boyut/ölçü ile kullanıma hazır harita.' },
    ],
  },
  {
    baslik: 'Veri',
    ogeler: [
      { yol: '/baglantilar', ad: 'Bağlantılar', ikon: <StorageIcon />, roller: GA, aciklama: 'Kaynak veritabanı profilleri (şifreli kimlik + Test Et).' },
      { yol: '/ice-alim', ad: 'İçe Alım İşleri', ikon: <CloudSyncIcon />, roller: GA, aciklama: 'Zamanlanmış veri senkronu (Import modu) ve durumu.' },
    ],
  },
  {
    baslik: 'Otomasyon',
    ogeler: [
      // Açıklamalar SÖZÜ TUTULABİLİR olanı söyler: pano aboneliği kapsam dışı (18 Ağu 2026 kararı),
      // v1 uyarısı EŞİK uyarısıdır — anomali (istatistiksel sapma) yok. Menüde vaat edilip ekranda
      // karşılığı olmayan özellik, ürüne güveni bitiren şeydir.
      { yol: '/zamanlanmis-raporlar', ad: 'Zamanlanmış Raporlar', ikon: <ScheduleSendIcon />, aciklama: 'Abonelik: raporu periyodik e-posta ile al (PDF/Excel/CSV).' },
      { yol: '/uyarilar', ad: 'Uyarılar', ikon: <NotificationsActiveIcon />, aciklama: 'Eşik uyarısı: izlenen raporun ya da metriğin değeri sınırı aşınca e-posta gelir (yalnız durum değişince).' },
    ],
  },
  {
    baslik: 'Yönetim',
    ogeler: [
      { yol: '/yonetim', ad: 'Kullanıcılar', ikon: <PeopleIcon />, roller: A, aciklama: 'Tenant kullanıcıları — davet ve rol atama.' },
      { yol: '/yonetim/roles', ad: 'Roles & Yetkiler', ikon: <AdminPanelSettingsIcon />, roller: A, aciklama: 'Rapor/klasör bazlı ince yetki (kim görür/düzenler) — nesne izni.' },
      { yol: '/yonetim/satir-guvenligi', ad: 'Satır Güvenliği', ikon: <ShieldIcon />, roller: A, aciklama: 'Satır-düzeyi güvenlik (RLS): kullanıcı/kiracı özniteliğine göre veri satırını kısıtla.' },
      // Limitler burada DEĞİL Sistem Ayarları'nda düzenlenir; bu ekran onları yalnız özetler.
      { yol: '/yonetim/kurulus', ad: 'Kuruluş Ayarları', ikon: <BusinessIcon />, roller: A, aciklama: 'Kuruluşun adı, planı ve markası (logo, vurgu rengi).' },
      { yol: '/yonetim/gomme', ad: 'Gömme & Marka', ikon: <IntegrationInstructionsIcon />, roller: A, aciklama: 'Raporu dış siteye göm: imzalı anahtar, izinli alan adları, beyaz-etiket marka.' },
      { yol: '/yonetim/ajanlar', ad: 'Ajanlar', ikon: <RouterIcon />, roller: A, aciklama: 'Müşteri ağına kurulan KDS Ajanı: içe alım işlerini kendi tarafında koşturur, veriyi depoya iter (S45).' },
      { yol: '/yonetim/ayarlar', ad: 'Sistem Ayarları', ikon: <TuneOutlinedIcon />, roller: A, aciklama: 'Sorgu limitleri ve tanı eşikleri — kod değişmeden yönetilir.' },
      { yol: '/yonetim/denetim', ad: 'Denetim', ikon: <HistoryIcon />, roller: A, aciklama: 'Kim hangi sorguyu ne zaman çalıştırdı — denetim kaydı.' },
      // Kaynak denetim kayıtları (S5) — yeni ölçüm altyapısı yok; pano kullanımı raporlar üzerinden dolaylı.
      { yol: '/yonetim/kullanim', ad: 'Kullanım', ikon: <InsightsIcon />, roller: A, aciklama: 'Hangi rapor ne sıklıkta çalışıyor, ne yavaş — denetim kayıtlarından.' },
    ],
  },
];
