using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Connections;

public sealed class ListConnectionsHandler(KdsDbContext context)
    : IRequestHandler<ListConnectionsRequest, IReadOnlyList<ConnectionSummary>>
{
    public async Task<IReadOnlyList<ConnectionSummary>> Handle(ListConnectionsRequest request, CancellationToken cancellationToken) =>
        await context.Baglantilar
            .OrderBy(b => b.Ad)
            .Select(b => new ConnectionSummary(b.Id, b.Ad, b.Sunucu, b.Veritabani, b.KullaniciAdi, b.Mod,
                b.SonTestBasarili, b.SonTestMs, b.SonTestTarihi))
            .ToListAsync(cancellationToken);
}
