namespace Kds.Contracts.Dashboards;

/// <summary>Pano widget yerleşimi (react-grid-layout birimi): bir raporun ızgaradaki konumu ve
/// boyutu. <see cref="X"/>/<see cref="Y"/> = sütun/satır; <see cref="W"/>/<see cref="H"/> =
/// genişlik/yükseklik (sütun/satır cinsinden). Rapor yumuşak referans; yerleşimi olmayan rapor
/// görüntüleyicide varsayılan akış konumuna düşer.</summary>
public sealed record DashboardWidget(Guid RaporId, int X, int Y, int W, int H);
