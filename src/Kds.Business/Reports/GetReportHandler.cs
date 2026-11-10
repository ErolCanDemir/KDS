using System.Text.Json;
using Kds.Business.Identity;
using Kds.Contracts.Reports;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Reports;

/// <summary>Raporu model dahil getirir; görünmez/yok → null (404).</summary>
public sealed class GetReportHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<GetReportRequest, ReportDetail?>
{
    public async Task<ReportDetail?> Handle(GetReportRequest request, CancellationToken cancellationToken)
    {
        var myId = await ReportCommon.MyIdAsync(context, kb, cancellationToken);
        var r = await context.Raporlar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        // S27: görünürlük tek kuraldan (liste ile aynı) — Roller paylaşımı + klasör rol erişimi dahil.
        if (r is null || !await RaporGorunurluk.GorebilirAsync(context, kb, r, myId, cancellationToken))
            return null;

        // SQL raporunda model yok (HamSql+BaglantiId taşır); model raporunda JSON çözülür.
        var model = r.ModelJson is null ? null
            : JsonSerializer.Deserialize<Contracts.QueryModel.QueryModel>(r.ModelJson, ReportCommon.JsonAyar)
              ?? throw new InvalidOperationException("Report modeli çözülemedi.");
        var sahipAd = await context.Kullanicilar.Where(u => u.Id == r.SahipId)
            .Select(u => u.AdSoyad ?? u.KullaniciAdi).FirstOrDefaultAsync(cancellationToken);

        return new ReportDetail(r.Id, r.Ad, r.KlasorId, r.HaritaId, model, r.HamSql, r.BaglantiId,
            r.Gorsellestirme, r.Paylasim, sahipAd, r.SahipId == myId,
            r.PaylasimRolleri?.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }
}
