namespace Kds.Contracts.Reports;

/// <summary>Görüntüleyici/tasarımcı için raporun tam içeriği. Model raporunda
/// <see cref="HaritaId"/>+<see cref="Model"/>, SQL raporunda <see cref="BaglantiId"/>+
/// <see cref="HamSql"/> doludur.</summary>
public sealed record ReportDetail(
    Guid Id,
    string Ad,
    Guid? KlasorId,
    Guid? HaritaId,
    QueryModel.QueryModel? Model,
    string? HamSql,
    Guid? BaglantiId,
    VisualizationType Gorsellestirme,
    ReportSharing Paylasim,
    string? SahipAd,
    bool BenimMi,
    /// <summary>S27 — Roller paylaşımında görebilen roller (diğerlerinde null).</summary>
    IReadOnlyList<string>? PaylasimRolleri = null);
