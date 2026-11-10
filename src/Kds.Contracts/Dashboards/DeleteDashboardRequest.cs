using Kds.Common.Cqrs;

namespace Kds.Contracts.Dashboards;

/// <summary>Panoyu siler. Yalnız sahibi ya da admin. Yoksa/yetkisizse false.</summary>
public sealed record DeleteDashboardRequest(Guid Id) : ICommand<bool>;
