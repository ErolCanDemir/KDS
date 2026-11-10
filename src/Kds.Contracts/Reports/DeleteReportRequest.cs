using Kds.Common.Cqrs;

namespace Kds.Contracts.Reports;

/// <summary>Raporu siler (favorileri cascade). Yalnız sahibi ya da admin. Yoksa/yetkisizse false.</summary>
public sealed record DeleteReportRequest(Guid Id) : ICommand<bool>;
