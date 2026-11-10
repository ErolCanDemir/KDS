using FluentValidation;
using Kds.Contracts.Metrics;
using Kds.Data;
using Kds.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Metrics;

/// <summary>Kataloğu taşınabilir biçimde verir (S30). Harita GUID değil ADIYLA yazılır: GUID
/// kiracıya özeldir, ad ise hedef ortamda (test → canlı) aynı kurulabilir — dosya bu sayede
/// ortamlar arası taşınır. Aktif/pasif ve sahip bilgisi bilerek dışarıda: onlar bu kurulumun
/// durumudur, tanımın parçası değil.</summary>
public sealed class ExportMetricsHandler(KdsDbContext context)
    : IRequestHandler<ExportMetricsRequest, IReadOnlyList<MetricExportItem>>
{
    public async Task<IReadOnlyList<MetricExportItem>> Handle(ExportMetricsRequest request, CancellationToken cancellationToken)
    {
        var sorgu = context.Metrikler.AsNoTracking();
        if (request.VeriHaritasiId is Guid haritaId)
        {
            sorgu = sorgu.Where(m => m.VeriHaritasiId == haritaId);
        }

        return await sorgu
            .OrderBy(m => m.Ad)
            .Select(m => new MetricExportItem(
                context.Haritalar.Where(h => h.Id == m.VeriHaritasiId).Select(h => h.Ad).FirstOrDefault() ?? "",
                m.Ad, m.Ifade, MetricCommon.BicimCoz(m.Bicim), m.Aciklama))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>İçe alım (S30): her öğe kendi başına değerlendirilir — biri düşerse diğerleri
/// etkilenmez, sonuç listesi öğe öğe ne olduğunu söyler. Kayıt normal <c>SaveMetric</c> yolundan
/// geçer: formül üreticiyle bugünkü haritada doğrulanır, güncellemeler sürüm geçmişine yazılır.</summary>
public sealed class ImportMetricsHandler(KdsDbContext context, ISender gonderici)
    : IRequestHandler<ImportMetricsRequest, IReadOnlyList<MetricImportItemResult>>
{
    public async Task<IReadOnlyList<MetricImportItemResult>> Handle(ImportMetricsRequest request, CancellationToken cancellationToken)
    {
        var sonuclar = new List<MetricImportItemResult>(request.Ogeler.Count);
        foreach (var oge in request.Ogeler)
        {
            sonuclar.Add(await OgeAlAsync(oge, request.CakismadaGuncelle, cancellationToken));
        }

        return sonuclar;
    }

    private async Task<MetricImportItemResult> OgeAlAsync(MetricExportItem oge, bool guncelle, CancellationToken ct)
    {
        var ad = oge.Ad?.Trim() ?? "";
        if (ad.Length == 0)
        {
            return new MetricImportItemResult("(adsız)", MetrikIceAlmaSonucu.Hata, "Metrik adı boş.");
        }

        var haritaId = await context.Haritalar.AsNoTracking()
            .Where(h => h.Ad == oge.HaritaAd)
            .Select(h => (Guid?)h.Id)
            .FirstOrDefaultAsync(ct);
        if (haritaId is null)
        {
            return new MetricImportItemResult(ad, MetrikIceAlmaSonucu.Hata,
                $"'{oge.HaritaAd}' adlı veri haritası bu kurulumda yok — önce haritayı kurun.");
        }

        var mevcutId = await context.Metrikler.AsNoTracking()
            .Where(m => m.Ad == ad)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(ct);
        if (mevcutId is not null && !guncelle)
        {
            return new MetricImportItemResult(ad, MetrikIceAlmaSonucu.Atlandi,
                "Aynı adla metrik zaten var (güncelleme kapalı).");
        }

        // Öğe bazlı hata toplama — toplu içe alımın sözleşmesi "biri düşerse diğerleri sürer"dir;
        // yakalananlar dar-tipli iş kuralı hatalarıdır (doğrulama / üretici / bulunamadı), sistem
        // hataları (DB kopması vb.) aynen dışarı fırlar ve koşumu keser.
        try
        {
            await gonderici.Send(new SaveMetricRequest(
                mevcutId, ad, haritaId.Value, oge.Ifade, oge.Bicim, oge.Aciklama), ct);
        }
        catch (Exception hata) when (hata is ValidationException or QueryModelException or KeyNotFoundException)
        {
            return new MetricImportItemResult(ad, MetrikIceAlmaSonucu.Hata, hata.Message);
        }

        return new MetricImportItemResult(ad,
            mevcutId is null ? MetrikIceAlmaSonucu.Eklendi : MetrikIceAlmaSonucu.Guncellendi, null);
    }
}
