using Kds.Contracts.Users;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Users;

public sealed class ProvisionUserHandler(KdsDbContext context)
    : IRequestHandler<ProvisionUserRequest, Guid>
{
    public async Task<Guid> Handle(ProvisionUserRequest request, CancellationToken cancellationToken)
    {
        // KeycloakId global benzersiz — tenant filtresini yok sayarak bul.
        var existing = await context.Kullanicilar
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.KeycloakId == request.KeycloakId, cancellationToken);

        if (existing is not null)
        {
            existing.KullaniciAdi = request.KullaniciAdi;
            existing.EPosta = request.EPosta;
            existing.AdSoyad = request.AdSoyad;
            existing.SonGirisTarihi = DateTimeOffset.Now;
            await context.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var newEntity = new User
        {
            KeycloakId = request.KeycloakId,
            KullaniciAdi = request.KullaniciAdi,
            EPosta = request.EPosta,
            AdSoyad = request.AdSoyad,
            TenantId = request.TenantId,
            SonGirisTarihi = DateTimeOffset.Now,
        };
        context.Kullanicilar.Add(newEntity);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return newEntity.Id;
        }
        catch (DbUpdateException)
        {
            // Yarış: eşzamanlı başka bir request aynı KeycloakId'yi ekledi (unique ihlali).
            // Kullanıcı artık var; eklemeyi geri al ve existing kaydı döndür.
            // (IO adaptörü / eşzamanlılık çakışması — AGENTS §4.6 izinli bölge.)
            context.Entry(newEntity).State = EntityState.Detached;
            var eklenen = await context.Kullanicilar
                .IgnoreQueryFilters()
                .FirstAsync(k => k.KeycloakId == request.KeycloakId, cancellationToken);
            return eklenen.Id;
        }
    }
}
