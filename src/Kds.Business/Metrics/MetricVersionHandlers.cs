using Kds.Contracts.Metrics;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Metrics;

/// <summary>Metriğin sürüm geçmişi — en yenisi üstte. Satır = tanımın ESKİ bir hâli; güncel hâl
/// her zaman metriğin kendisidir (listede görünmez, ekran onu zaten gösteriyor).</summary>
public sealed class ListMetricVersionsHandler(KdsDbContext context)
    : IRequestHandler<ListMetricVersionsRequest, IReadOnlyList<MetricVersionSummary>>
{
    public async Task<IReadOnlyList<MetricVersionSummary>> Handle(
        ListMetricVersionsRequest request, CancellationToken cancellationToken)
    {
        _ = await context.Metrikler.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MetrikId, cancellationToken)
            ?? throw new KeyNotFoundException("Metrik bulunamadı.");

        var satirlar = await context.MetrikSurumleri.AsNoTracking()
            .Where(v => v.MetrikId == request.MetrikId)
            .OrderByDescending(v => v.SurumNo)
            .Select(v => new
            {
                Surum = v,
                DegistirenAd = context.Kullanicilar
                    .Where(k => k.Id == v.DegistirenId).Select(k => k.KullaniciAdi).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return satirlar
            .Select(x => new MetricVersionSummary(x.Surum.SurumNo, x.Surum.Ad, x.Surum.Ifade,
                MetricCommon.BicimCoz(x.Surum.Bicim), x.Surum.Aciklama, x.DegistirenAd,
                x.Surum.OlusturmaTarihi))
            .ToList();
    }
}

/// <summary>Seçili sürümü geri yükler — bilerek normal kaydetme YOLUNDAN geçer (ayrı bir "geri
/// yükleme yolu" açılmaz): formül bugünkü haritayla yeniden doğrulanır (eski tanımdaki kolon
/// silinmişse dönüş anlaşılır hatayla reddedilir) ve dönüşün kendisi de sürüm üretir — geçmiş
/// hiç kaybolmaz, "dönüşü geri almak" da mümkün olur.</summary>
public sealed class RestoreMetricVersionHandler(KdsDbContext context, ISender gonderici)
    : IRequestHandler<RestoreMetricVersionRequest, MetricSummary>
{
    public async Task<MetricSummary> Handle(RestoreMetricVersionRequest request, CancellationToken cancellationToken)
    {
        var metrik = await context.Metrikler.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MetrikId, cancellationToken)
            ?? throw new KeyNotFoundException("Metrik bulunamadı.");
        var surum = await context.MetrikSurumleri.AsNoTracking()
            .FirstOrDefaultAsync(v => v.MetrikId == request.MetrikId && v.SurumNo == request.SurumNo, cancellationToken)
            ?? throw new KeyNotFoundException("Sürüm bulunamadı.");

        return await gonderici.Send(new SaveMetricRequest(
            metrik.Id, surum.Ad, metrik.VeriHaritasiId, surum.Ifade,
            MetricCommon.BicimCoz(surum.Bicim), surum.Aciklama, metrik.Aktif), cancellationToken);
    }
}
