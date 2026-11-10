using Kds.Common.Cqrs;

namespace Kds.Contracts.Reports;

/// <summary>Raporu kaydeder: <see cref="Id"/> boşsa newEntity (sahip = existing kullanıcı), doluysa günceller.
/// İki tasarım türünden TAM BİRİ verilmeli: <see cref="Model"/> (dataMap, modelin HaritaId'sinden
/// doğrulanır) ya da <see cref="HamSql"/>+<see cref="BaglantiId"/> (SQL Editörü raporu; kayıtta
/// SELECT-only bekçisinden geçer).</summary>
public sealed record SaveReportRequest(
    Guid? Id,
    string Ad,
    Guid? KlasorId,
    QueryModel.QueryModel? Model,
    VisualizationType Gorsellestirme,
    ReportSharing Paylasim,
    string? HamSql = null,
    Guid? BaglantiId = null,
    /// <summary>S27 — Paylasim=Roller iken zorunlu: görebilecek rol adları.</summary>
    IReadOnlyList<string>? PaylasimRolleri = null) : ICommand<ReportSummary>;
