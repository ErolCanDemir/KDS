using Kds.Business.Identity;
using Kds.Contracts.Reports;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Reports;

/// <summary>Raporu siler (favoriler cascade). Yalnız sahibi ya da admin; aksi/yok → false.</summary>
public sealed class DeleteReportHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<DeleteReportRequest, bool>
{
    public async Task<bool> Handle(DeleteReportRequest request, CancellationToken cancellationToken)
    {
        var myId = await ReportCommon.MyIdAsync(context, kb, cancellationToken);
        var rapor = await context.Raporlar.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (rapor is null || (rapor.SahipId != myId && !kb.IsAdmin)) return false;
        context.Raporlar.Remove(rapor);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
