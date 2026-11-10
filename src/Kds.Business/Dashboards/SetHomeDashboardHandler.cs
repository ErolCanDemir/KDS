using Kds.Business.Identity;
using Kds.Business.Reports;
using Kds.Contracts.Dashboards;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Dashboards;

/// <summary>Ana sayfa panosunu seçer/kaldırır (idempotent). GÖRÜNMEYEN pano seçilemez: aksi hâlde
/// kullanıcı başkasının özel panosunu ana sayfasına yazıp varlığını öğrenirdi.</summary>
public sealed class SetHomeDashboardHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<SetHomeDashboardRequest, bool>
{
    public async Task<bool> Handle(SetHomeDashboardRequest request, CancellationToken cancellationToken)
    {
        var myId = await ReportCommon.MyIdAsync(context, kb, cancellationToken);
        if (myId is not Guid ben) return false;

        if (request.PanoId is Guid panoId)
        {
            var p = await context.Panolar.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == panoId, cancellationToken);
            if (p is null || !PanoGorunurluk.Gorebilir(kb, p.Paylasim, p.PaylasimRolleri, p.SahipId, ben))
                return false;
        }

        var kullanici = await context.Kullanicilar.FirstOrDefaultAsync(u => u.Id == ben, cancellationToken);
        if (kullanici is null) return false;

        kullanici.AnaSayfaPanoId = request.PanoId;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
