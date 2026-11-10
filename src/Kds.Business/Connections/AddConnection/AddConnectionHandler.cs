using Kds.Data;
using Kds.Data.Entities;
using MediatR;

namespace Kds.Business.Connections;

public sealed class AddConnectionHandler(KdsDbContext context, IPasswordProtector koruma)
    : IRequestHandler<AddConnectionRequest, ConnectionSummary>
{
    public async Task<ConnectionSummary> Handle(AddConnectionRequest request, CancellationToken cancellationToken)
    {
        var newEntity = new Connection
        {
            Ad = request.Ad.Trim(),
            Sunucu = request.Sunucu.Trim(),
            Veritabani = request.Veritabani.Trim(),
            KullaniciAdi = string.IsNullOrWhiteSpace(request.KullaniciAdi) ? null : request.KullaniciAdi.Trim(),
            SifreSifreli = string.IsNullOrEmpty(request.Sifre) ? string.Empty : koruma.Protect(request.Sifre),
            Mod = request.Mod,
            DepoBaglantiId = await ConnectionDepoHedefi.DogrulaAsync(
                context, request.Mod, request.DepoBaglantiId, null, cancellationToken),
            AjanId = await ConnectionAjani.DogrulaAsync(
                context, request.Mod, request.AjanId, cancellationToken),
        };
        context.Baglantilar.Add(newEntity);
        await context.SaveChangesAsync(cancellationToken);
        return ConnectionMapping.Ozet(newEntity);
    }
}
