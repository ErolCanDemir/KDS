using Kds.Business.Identity;
using Kds.Business.Reports;
using Kds.Contracts.Dashboards;
using Kds.Contracts.Reports;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Dashboards;

/// <summary>Panoyu rapor id'leriyle getirir; görünmez/yok → null (404).</summary>
public sealed class GetDashboardHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<GetDashboardRequest, DashboardDetail?>
{
    public async Task<DashboardDetail?> Handle(GetDashboardRequest request, CancellationToken cancellationToken)
    {
        var myId = await ReportCommon.MyIdAsync(context, kb, cancellationToken);
        var p = await context.Panolar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        // S27: tekil erişim liste kuralıyla aynı — kural PanoGorunurluk'ta tek yerde.
        if (p is null || !PanoGorunurluk.Gorebilir(kb, p.Paylasim, p.PaylasimRolleri, p.SahipId, myId))
            return null;

        var sahipAd = await context.Kullanicilar.Where(u => u.Id == p.SahipId)
            .Select(u => u.AdSoyad ?? u.KullaniciAdi).FirstOrDefaultAsync(cancellationToken);
        var anaSayfaPanoId = await context.Kullanicilar.Where(u => u.Id == myId)
            .Select(u => u.AnaSayfaPanoId).FirstOrDefaultAsync(cancellationToken);
        return new DashboardDetail(p.Id, p.Ad, p.Paylasim,
            DashboardCommon.DeserializeReports(p.RaporlarJson),
            DashboardCommon.DeserializeLayout(p.YerlesimJson), sahipAd, p.SahipId == myId,
            p.PaylasimRolleri?.Split(',', StringSplitOptions.RemoveEmptyEntries),
            p.Id == anaSayfaPanoId);
    }
}
