using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Connections;

public sealed class UpdateConnectionHandler(KdsDbContext context, IPasswordProtector koruma)
    : IRequestHandler<UpdateConnectionRequest, ConnectionSummary?>
{
    public async Task<ConnectionSummary?> Handle(UpdateConnectionRequest request, CancellationToken cancellationToken)
    {
        var b = await context.Baglantilar.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (b is null) return null;

        b.Ad = request.Ad.Trim();
        b.Sunucu = request.Sunucu.Trim();
        b.Veritabani = request.Veritabani.Trim();
        b.KullaniciAdi = string.IsNullOrWhiteSpace(request.KullaniciAdi) ? null : request.KullaniciAdi.Trim();
        // Parola yalnız newEntity bir değer geldiyse değişir; boşsa existing şifreli parola korunur.
        if (!string.IsNullOrEmpty(request.Sifre))
            b.SifreSifreli = koruma.Protect(request.Sifre);
        b.Mod = request.Mod;
        b.DepoBaglantiId = await ConnectionDepoHedefi.DogrulaAsync(
            context, request.Mod, request.DepoBaglantiId, request.Id, cancellationToken);
        b.AjanId = await ConnectionAjani.DogrulaAsync(
            context, request.Mod, request.AjanId, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        return ConnectionMapping.Ozet(b);
    }
}
