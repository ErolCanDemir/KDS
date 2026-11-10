import { StrictMode, Suspense, lazy } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { TemaSaglayici } from './temaContext';
import { IdentityProvider } from './identity/IdentityProvider';
import { KurulumKapisi } from './KurulumKapisi';
import { ProtectedArea } from './identity/ProtectedArea';
import { AppShell } from './layout/AppShell';
import { HomePage } from './pages/HomePage';
import { ConnectionsPage } from './pages/ConnectionsPage';
import { IngestJobsPage } from './pages/IngestJobsPage';
import { SubscriptionsPage } from './pages/SubscriptionsPage';
import { DataMapsPage } from './pages/DataMapsPage';
import { ReportsPage } from './pages/ReportsPage';
import { ReportViewPage } from './pages/ReportViewPage';
import { DashboardsPage } from './pages/DashboardsPage';
import { DashboardViewPage } from './pages/DashboardViewPage';
// Monaco ağırdır: SQL Editörü tembel yüklenir — chunk yalnız /sql'e girilince iner.
// oxlint-disable-next-line react/only-export-components -- giriş dosyası, fast refresh dışı
const RawSqlPage = lazy(() =>
  import('./pages/RawSqlPage').then((m) => ({ default: m.RawSqlPage })));
import AlertsPage from './pages/AlertsPage';
import MetricsPage from './pages/MetricsPage';
import SoruSorPage from './pages/SoruSorPage';
import RolesPage from './pages/RolesPage';
import { QueryDesignerPage } from './pages/QueryDesignerPage';
import { AdminPage } from './pages/AdminPage';
import { AuditPage } from './pages/AuditPage';
import { SettingsPage } from './pages/SettingsPage';
import { KurulusAyarlariPage } from './pages/KurulusAyarlariPage';
import { KullanimPage } from './pages/KullanimPage';
import { GommePage } from './pages/GommePage';
import { AjanlarPage } from './pages/AjanlarPage';
import { GomuluPage } from './pages/GomuluPage';
import { RowSecurityPage } from './pages/RowSecurityPage';
import { LoadingCurtain } from './components/Loading';
import './index.css';

const yonlendirici = createBrowserRouter([
  {
    path: '/',
    element: (
      <ProtectedArea>
        <AppShell />
      </ProtectedArea>
    ),
    children: [
      // Çalışma Alanı
      { index: true, element: <HomePage /> },
      { path: 'soru-sor', element: <SoruSorPage /> },
      { path: 'panolar', element: <DashboardsPage /> },
      { path: 'panolar/:id', element: <DashboardViewPage /> },
      { path: 'raporlar', element: <ReportsPage /> },
      { path: 'raporlar/klasorler', element: <ReportsPage baslangic="klasorler" /> },
      { path: 'raporlar/favoriler', element: <ReportsPage baslangic="favoriler" /> },
      { path: 'raporlar/paylasilan', element: <ReportsPage baslangic="paylasilan" /> },
      { path: 'raporlar/:id', element: <ReportViewPage /> },
      { path: 'metrikler', element: <MetricsPage /> },
      // Oluştur
      { path: 'tasarimci', element: <QueryDesignerPage /> },
      { path: 'sql', element: <Suspense fallback={<LoadingCurtain mesaj="Editör yükleniyor…" />}><RawSqlPage /></Suspense> },
      { path: 'veri-haritalari', element: <DataMapsPage /> },
      // Veri
      { path: 'baglantilar', element: <ConnectionsPage /> },
      { path: 'ice-alim', element: <IngestJobsPage /> },
      // Otomasyon
      { path: 'zamanlanmis-raporlar', element: <SubscriptionsPage /> },
      { path: 'uyarilar', element: <AlertsPage /> },
      // Yönetim
      { path: 'yonetim', element: <AdminPage /> },
      { path: 'yonetim/ayarlar', element: <SettingsPage /> },
      { path: 'yonetim/roles', element: <RolesPage /> },
      { path: 'yonetim/satir-guvenligi', element: <RowSecurityPage /> },
      { path: 'yonetim/kurulus', element: <KurulusAyarlariPage /> },
      { path: 'yonetim/gomme', element: <GommePage /> },
      { path: 'yonetim/ajanlar', element: <AjanlarPage /> },
      { path: 'yonetim/denetim', element: <AuditPage /> },
      { path: 'yonetim/kullanim', element: <KullanimPage /> },
    ],
  },
  /* S42 — GÖMÜLÜ GÖRÜNÜM: bilerek ProtectedArea ve AppShell DIŞINDA. Dış sitenin iframe'inde
     açılır; oturumu olmayan ziyaretçiyi korumalı alan Keycloak'a atardı ve menü/kimlik kabuğu
     müşterinin sayfasında yeri olmayan öğeler getirirdi. Yetki tamamen anahtarla doğrulanır. */
  { path: '/gomulu', element: <GomuluPage /> },
]);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    {/* Açık/koyu tema (21 Ağu): tek kimlik Lacivert & Teal; tercih localStorage'da kalıcı. */}
    <TemaSaglayici>
      {/* S16/A2: uygulama kurulmamışsa kimlik sağlayıcıya HİÇ gidilmez — sihirbaz açılır. */}
      <KurulumKapisi>
        <IdentityProvider>
          <RouterProvider router={yonlendirici} />
        </IdentityProvider>
      </KurulumKapisi>
    </TemaSaglayici>
  </StrictMode>,
);
