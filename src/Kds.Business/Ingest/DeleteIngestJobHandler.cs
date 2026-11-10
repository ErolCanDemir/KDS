using Kds.Contracts.Ingest;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Ingest;

/// <summary>İçe alım işini siler (depo tablolarına dokunmaz). Bulunmayan → false.</summary>
public sealed class DeleteIngestJobHandler(KdsDbContext context)
    : IRequestHandler<DeleteIngestJobRequest, bool>
{
    public async Task<bool> Handle(DeleteIngestJobRequest request, CancellationToken cancellationToken)
    {
        var j = await context.IceAlimIsleri.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (j is null) return false;
        context.IceAlimIsleri.Remove(j);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
