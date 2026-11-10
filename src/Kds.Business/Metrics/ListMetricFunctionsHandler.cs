using Kds.Contracts.Metrics;
using Kds.Execution;
using MediatR;

namespace Kds.Business.Metrics;

/// <summary>S47 — formül düzenleyicinin fonksiyon ipucu. İçerik sabittir ve bekçinin yanındaki
/// <see cref="IfadeYardimi"/>'ndan gelir (tek kaynak); DB'ye inilmez.</summary>
public sealed class ListMetricFunctionsHandler
    : IRequestHandler<ListMetricFunctionsRequest, IReadOnlyList<MetricFunctionGroup>>
{
    public Task<IReadOnlyList<MetricFunctionGroup>> Handle(
        ListMetricFunctionsRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(IfadeYardimi.Gruplar);
}
