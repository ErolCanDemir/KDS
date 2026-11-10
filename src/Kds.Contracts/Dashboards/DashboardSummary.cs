using Kds.Contracts.Reports;

namespace Kds.Contracts.Dashboards;

/// <summary>Pano galerisi kartı için özet. <see cref="BenimMi"/> isteği yapan kullanıcıya görelidir.</summary>
public sealed record DashboardSummary(
    Guid Id,
    string Ad,
    ReportSharing Paylasim,
    int RaporSayisi,
    string? SahipAd,
    bool BenimMi,
    DateTimeOffset? GuncellemeTarihi,
    /// <summary>S27 — Roller paylaşımında görebilen roller (diğerlerinde null).</summary>
    IReadOnlyList<string>? PaylasimRolleri = null,
    /// <summary>S43 — isteği yapan kullanıcının ana sayfasında bu pano mu açılıyor.</summary>
    bool AnaSayfamMi = false);
