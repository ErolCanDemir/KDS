using Kds.Common.Cqrs;

namespace Kds.Contracts.Reports;

/// <summary>Raporu tam içeriğiyle getirir (model dahil). Görünür değilse/yoksa null.</summary>
public sealed record GetReportRequest(Guid Id) : IQuery<ReportDetail?>;
