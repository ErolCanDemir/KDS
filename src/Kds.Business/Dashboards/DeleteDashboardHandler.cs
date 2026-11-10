using Kds.Business.Identity;
using Kds.Business.Reports;
using Kds.Contracts.Dashboards;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Dashboards;

/// <summary>Panoyu siler. Yalnız sahibi ya da admin; aksi/yok → false.</summary>
public sealed class DeleteDashboardHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<DeleteDashboardRequest, bool>
{
    public async Task<bool> Handle(DeleteDashboardRequest request, CancellationToken cancellationToken)
    {
        var myId = await ReportCommon.MyIdAsync(context, kb, cancellationToken);
        var pano = await context.Panolar.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (pano is null || (pano.SahipId != myId && !kb.IsAdmin)) return false;
        context.Panolar.Remove(pano);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
