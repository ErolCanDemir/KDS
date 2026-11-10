using Kds.Business.Identity;
using Kds.Contracts.Reports;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Reports;

/// <summary>Mevcut kullanıcının yıldızını ekler/kaldırır (idempotent). Görünmez rapor → false.</summary>
public sealed class SetReportFavoriteHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<SetReportFavoriteRequest, bool>
{
    public async Task<bool> Handle(SetReportFavoriteRequest request, CancellationToken cancellationToken)
    {
        var myId = await ReportCommon.MyIdAsync(context, kb, cancellationToken);
        if (myId is not Guid ben) return false;

        var rapor = await context.Raporlar.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (rapor is null || (rapor.Paylasim == ReportSharing.Ozel && rapor.SahipId != ben && !kb.IsAdmin))
            return false;

        var existing = await context.RaporFavorileri
            .FirstOrDefaultAsync(f => f.RaporId == request.Id && f.KullaniciId == ben, cancellationToken);
        if (request.Favori && existing is null)
            context.RaporFavorileri.Add(new ReportFavorite { RaporId = request.Id, KullaniciId = ben });
        else if (!request.Favori && existing is not null)
            context.RaporFavorileri.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
