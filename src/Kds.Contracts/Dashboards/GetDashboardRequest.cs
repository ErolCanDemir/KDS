using Kds.Common.Cqrs;

namespace Kds.Contracts.Dashboards;

/// <summary>Panoyu tam içeriğiyle getirir (rapor id'leri dahil). Görünür değilse/yoksa null.</summary>
public sealed record GetDashboardRequest(Guid Id) : IQuery<DashboardDetail?>;
