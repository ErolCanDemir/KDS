using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Connections;

public sealed class DeleteConnectionHandler(KdsDbContext context)
    : IRequestHandler<DeleteConnectionRequest, bool>
{
    public async Task<bool> Handle(DeleteConnectionRequest request, CancellationToken cancellationToken)
    {
        var b = await context.Baglantilar.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (b is null) return false;
        context.Baglantilar.Remove(b);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
