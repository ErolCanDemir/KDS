using Kds.Business.Identity;
using Kds.Business.Reports;
using Kds.Contracts.Dashboards;
using Kds.Contracts.Reports;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Dashboards;

/// <summary>Görünür panolar (S27): Ekip + kendi Özel'lerim + rol kesişimli Roller paylaşımlılar; admin hepsi.
/// Son güncellenen üstte (sıralama bellekte — rapor sayısı JSON'dan çözülür).</summary>
public sealed class ListDashboardsHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<ListDashboardsRequest, IReadOnlyList<DashboardSummary>>
{
    public async Task<IReadOnlyList<DashboardSummary>> Handle(ListDashboardsRequest request, CancellationToken cancellationToken)
    {
        var myId = await ReportCommon.MyIdAsync(context, kb, cancellationToken) ?? Guid.Empty;

        // S27: kaba süzgeç DB'de (Özel'ler başkasına inmez); rol kesişimi bellekte.
        var sorgu = context.Panolar.AsQueryable();
        if (!kb.IsAdmin)
            sorgu = sorgu.Where(p => p.Paylasim != ReportSharing.Ozel || p.SahipId == myId);

        var liste = await sorgu.Select(p => new
        {
            p.Id, p.Ad, p.Paylasim, p.PaylasimRolleri, p.RaporlarJson, p.SahipId, p.GuncellemeTarihi, p.OlusturmaTarihi,
            SahipAd = context.Kullanicilar.Where(u => u.Id == p.SahipId)
                .Select(u => u.AdSoyad ?? u.KullaniciAdi).FirstOrDefault(),
        }).ToListAsync(cancellationToken);

        // S43: hangi pano "ana sayfam" — kartta işaretlenebilsin diye özete taşınır.
        var anaSayfaPanoId = await context.Kullanicilar.Where(u => u.Id == myId)
            .Select(u => u.AnaSayfaPanoId).FirstOrDefaultAsync(cancellationToken);

        return liste
            .Where(p => PanoGorunurluk.Gorebilir(kb, p.Paylasim, p.PaylasimRolleri, p.SahipId, myId))
            .Select(p => new DashboardSummary(p.Id, p.Ad, p.Paylasim,
                DashboardCommon.DeserializeReports(p.RaporlarJson).Count, p.SahipAd, p.SahipId == myId,
                p.GuncellemeTarihi ?? p.OlusturmaTarihi,
                p.PaylasimRolleri == null ? null : p.PaylasimRolleri.Split(',', StringSplitOptions.RemoveEmptyEntries),
                p.Id == anaSayfaPanoId))
            .OrderByDescending(x => x.GuncellemeTarihi).ToList();
    }
}
