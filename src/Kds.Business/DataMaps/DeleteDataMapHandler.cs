using Kds.Contracts.DataMaps;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.DataMaps;

public sealed class DeleteDataMapHandler(KdsDbContext context)
    : IRequestHandler<DeleteDataMapRequest, bool>
{
    public async Task<bool> Handle(DeleteDataMapRequest request, CancellationToken cancellationToken)
    {
        var h = await context.Haritalar.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (h is null) return false;
        context.Haritalar.Remove(h); // cascade: tablolar/kolonlar/ilişkiler
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
