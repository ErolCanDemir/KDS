using Kds.Common.Cqrs;
using Kds.Contracts.Reports;

namespace Kds.Contracts.Dashboards;

/// <summary>Panoyu kaydeder: <see cref="Id"/> boşsa newEntity (sahip = existing kullanıcı), doluysa
/// günceller (yalnız sahibi + admin). <see cref="Raporlar"/> sıralı rapor id listesidir.
/// <see cref="Yerlesim"/> null ise mevcut widget yerleşimi KORUNUR (editör raporu değiştirir,
/// yerleşimi bozmaz); dolu ise yerleşim güncellenir (görüntüleyicideki sürükle-bırak).</summary>
public sealed record SaveDashboardRequest(
    Guid? Id,
    string Ad,
    ReportSharing Paylasim,
    IReadOnlyList<Guid> Raporlar,
    IReadOnlyList<DashboardWidget>? Yerlesim = null,
    /// <summary>S27 — Paylasim=Roller iken zorunlu: görebilecek rol adları.</summary>
    IReadOnlyList<string>? PaylasimRolleri = null) : ICommand<DashboardSummary>;
