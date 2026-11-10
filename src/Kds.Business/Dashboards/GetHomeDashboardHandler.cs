using Kds.Business.Identity;
using Kds.Business.Reports;
using Kds.Contracts.Dashboards;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Dashboards;

/// <summary>Ana sayfa panosunun kimliği. Seçim SAKLANDIKTAN SONRA pano silinmiş ya da paylaşımı
/// daralmış olabilir; bu yüzden her okumada görünürlük yeniden denetlenir ve düşen seçim null
/// döner (kayıt temizlenmez — paylaşım geri açılırsa tercih yerinde durur).</summary>
public sealed class GetHomeDashboardHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<GetHomeDashboardRequest, HomeDashboardInfo>
{
    private static readonly HomeDashboardInfo Yok = new((Guid?)null);

    public async Task<HomeDashboardInfo> Handle(GetHomeDashboardRequest request, CancellationToken cancellationToken)
    {
        var myId = await ReportCommon.MyIdAsync(context, kb, cancellationToken);
        if (myId is not Guid ben) return Yok;

        var panoId = await context.Kullanicilar.Where(u => u.Id == ben)
            .Select(u => u.AnaSayfaPanoId).FirstOrDefaultAsync(cancellationToken);
        if (panoId is not Guid id) return Yok;

        var p = await context.Panolar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return p is not null && PanoGorunurluk.Gorebilir(kb, p.Paylasim, p.PaylasimRolleri, p.SahipId, ben)
            ? new HomeDashboardInfo(id)
            : Yok;
    }
}
