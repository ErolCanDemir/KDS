using Kds.Contracts.Reports;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Reports;

/// <summary>Klasörü siler; raporları klasörsüze düşürür (tek transaction'da).</summary>
public sealed class DeleteFolderHandler(KdsDbContext context)
    : IRequestHandler<DeleteFolderRequest, bool>
{
    public async Task<bool> Handle(DeleteFolderRequest request, CancellationToken cancellationToken)
    {
        var klasor = await context.RaporKlasorleri.FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken);
        if (klasor is null) return false;

        await context.IslemdeCalistirAsync(async ic =>
        {
            var raporlar = await context.Raporlar.Where(r => r.KlasorId == request.Id).ToListAsync(ic);
            foreach (var r in raporlar) r.KlasorId = null;
            context.RaporKlasorleri.Remove(klasor);
            // SaveChanges + commit IslemdeCalistirAsync'te.
        }, cancellationToken);
        return true;
    }
}
