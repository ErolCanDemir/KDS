namespace Kds.Contracts.Reports;

/// <summary>Liste kartı için rapor özeti. <see cref="BenimMi"/>/<see cref="FavoriMi"/> isteği yapan
/// kullanıcıya görelidir.</summary>
public sealed record ReportSummary(
    Guid Id,
    string Ad,
    Guid? KlasorId,
    string? KlasorAd,
    VisualizationType Gorsellestirme,
    ReportSharing Paylasim,
    string? SahipAd,
    bool BenimMi,
    bool FavoriMi,
    DateTimeOffset? GuncellemeTarihi,
    /// <summary>S27 — Roller paylaşımında görebilen roller (diğerlerinde null).</summary>
    IReadOnlyList<string>? PaylasimRolleri = null);
