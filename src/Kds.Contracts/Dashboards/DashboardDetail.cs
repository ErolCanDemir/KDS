using Kds.Contracts.Reports;

namespace Kds.Contracts.Dashboards;

/// <summary>Pano görüntüleyici/editör için tam içerik: sıralı rapor id'leri + widget yerleşimi
/// (<see cref="Yerlesim"/> boşsa görüntüleyici varsayılan ızgara uygular).</summary>
public sealed record DashboardDetail(
    Guid Id,
    string Ad,
    ReportSharing Paylasim,
    IReadOnlyList<Guid> Raporlar,
    IReadOnlyList<DashboardWidget> Yerlesim,
    string? SahipAd,
    bool BenimMi,
    /// <summary>S27 — Roller paylaşımında görebilen roller (diğerlerinde null).</summary>
    IReadOnlyList<string>? PaylasimRolleri = null,
    /// <summary>S43 — isteği yapan kullanıcının ana sayfasında bu pano mu açılıyor.</summary>
    bool AnaSayfamMi = false);
