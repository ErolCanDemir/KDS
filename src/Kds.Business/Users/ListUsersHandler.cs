using Kds.Contracts.Users;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Users;

public sealed class ListUsersHandler(KdsDbContext context)
    : IRequestHandler<ListUsersRequest, IReadOnlyList<UserSummary>>
{
    public async Task<IReadOnlyList<UserSummary>> Handle(ListUsersRequest request, CancellationToken cancellationToken) =>
        await context.Kullanicilar
            .OrderBy(k => k.KullaniciAdi)
            .Select(k => new UserSummary(k.KullaniciAdi, k.EPosta, k.AdSoyad, k.SonGirisTarihi))
            .ToListAsync(cancellationToken);
}
