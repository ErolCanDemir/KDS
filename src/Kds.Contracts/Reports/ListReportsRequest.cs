using Kds.Common.Cqrs;

namespace Kds.Contracts.Reports;

/// <summary>Mevcut kullanıcının GÖREBİLDİĞİ raporları listeler (Ekip + kendi Özel'leri; admin hepsini).</summary>
public sealed record ListReportsRequest : IQuery<IReadOnlyList<ReportSummary>>;
