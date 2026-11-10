using Kds.Contracts.SystemStatus;
using MediatR;

namespace Kds.Business.SystemStatus;

public sealed class SystemStatusHandler : IRequestHandler<SystemStatusRequest, SystemStatusResponse>
{
    public Task<SystemStatusResponse> Handle(SystemStatusRequest request, CancellationToken cancellationToken)
    {
        var surum = typeof(SystemStatusHandler).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        return Task.FromResult(new SystemStatusResponse(surum, DateTimeOffset.Now));
    }
}
