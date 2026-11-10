using Kds.Contracts.Ingest;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Ingest;

/// <summary>İçe alım işini tablolarıyla getirir; yok → null (404).</summary>
public sealed class GetIngestJobHandler(KdsDbContext context)
    : IRequestHandler<GetIngestJobRequest, IngestJobDetail?>
{
    public async Task<IngestJobDetail?> Handle(GetIngestJobRequest request, CancellationToken cancellationToken)
    {
        var j = await context.IceAlimIsleri.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        return j is null
            ? null
            : new IngestJobDetail(j.Id, j.Ad, j.BaglantiId, IngestCommon.Deserialize(j.TablolarJson), j.Zamanlama, j.Aktif);
    }
}
