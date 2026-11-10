using System.Diagnostics;
using Kds.Business.DataMaps;
using Kds.Business.Identity;
using Kds.Contracts.Metrics;
using Kds.Contracts.Queries;
using Kds.Contracts.QueryModel;
using Kds.Data;
using Kds.Data.Entities;
using Kds.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Metrics;

/// <summary>Metrik handler'larının ortak parçaları.</summary>
internal static class MetricCommon
{
    public static MetrikBicimi BicimCoz(string s) =>
        Enum.TryParse<MetrikBicimi>(s, true, out var b) ? b : MetrikBicimi.Sayi;

    /// <summary>Metriği kullanan raporların adları. Model, referansı <c>metrikId</c> GUID'iyle
    /// taşıdığı için kullanım JSON'da kimlik aramasıyla bulunur — küçük katalog + kiracı başına
    /// makul rapor sayısında yeterli; ayrı bir bağ tablosu v1 için gereksiz karmaşıklık olurdu.</summary>
    public static async Task<List<string>> KullananRaporlarAsync(KdsDbContext context, Guid metrikId, CancellationToken ct)
    {
        var aranan = metrikId.ToString("D");
        return await context.Raporlar.AsNoTracking()
            .Where(r => r.ModelJson != null && EF.Functions.Like(r.ModelJson, $"%{aranan}%"))
            .Select(r => r.Ad)
            .OrderBy(a => a)
            .ToListAsync(ct);
    }

    public static async Task<MetricSummary> OzetAsync(KdsDbContext context, Metric m, CancellationToken ct)
    {
        var haritaAd = await context.Haritalar.AsNoTracking()
            .Where(h => h.Id == m.VeriHaritasiId).Select(h => h.Ad).FirstOrDefaultAsync(ct);
        var sahipAd = await context.Kullanicilar.AsNoTracking()
            .Where(k => k.Id == m.SahipId).Select(k => k.KullaniciAdi).FirstOrDefaultAsync(ct);
        var kullanan = await KullananRaporlarAsync(context, m.Id, ct);
        return new MetricSummary(m.Id, m.Ad, m.VeriHaritasiId, haritaAd, m.Ifade,
            BicimCoz(m.Bicim), m.Aciklama, sahipAd, m.Aktif, kullanan.Count);
    }
}

public sealed class ListMetricsHandler(KdsDbContext context)
    : IRequestHandler<ListMetricsRequest, IReadOnlyList<MetricSummary>>
{
    public async Task<IReadOnlyList<MetricSummary>> Handle(ListMetricsRequest request, CancellationToken cancellationToken)
    {
        var metrikler = await context.Metrikler.AsNoTracking().OrderBy(m => m.Ad).ToListAsync(cancellationToken);
        // Katalog küçüktür (kiracı başına onlarca); yardımcı sorgular liste boyunda kabul edilebilir.
        var sonuc = new List<MetricSummary>(metrikler.Count);
        foreach (var m in metrikler)
        {
            sonuc.Add(await MetricCommon.OzetAsync(context, m, cancellationToken));
        }

        return sonuc;
    }
}

public sealed class GetMetricHandler(KdsDbContext context)
    : IRequestHandler<GetMetricRequest, MetricSummary>
{
    public async Task<MetricSummary> Handle(GetMetricRequest request, CancellationToken cancellationToken)
    {
        var m = await context.Metrikler.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Metrik bulunamadı.");
        return await MetricCommon.OzetAsync(context, m, cancellationToken);
    }
}

/// <summary>Metriği kaydeder. <b>Formül kaydetme anında üreticinin kendisiyle doğrulanır</b> —
/// metriği tek alan yapan deneme modeli haritada SQL'e çevrilir; çevrilemiyorsa üreticinin yol
/// gösteren hatası (mevcut kolonlar vb.) aynen kullanıcıya gider. "Geçerli görünüp çalışmayan
/// metrik" kaydedilemez (Uyarılar'daki K3 dersi: doğrulamasız kayıt, sessizce ölü kayıttır).</summary>
public sealed class SaveMetricHandler(
    KdsDbContext context, IUserContext kb, IDataMapReader haritalar, IQueryGenerator uretici)
    : IRequestHandler<SaveMetricRequest, MetricSummary>
{
    public async Task<MetricSummary> Handle(SaveMetricRequest request, CancellationToken cancellationToken)
    {
        var harita = await haritalar.GetDetailAsync(request.VeriHaritasiId, cancellationToken)
            ?? throw new KeyNotFoundException("Metriğin bağlanacağı harita bulunamadı.");

        // Formül doğrulama: metrik tek başına bir modelmiş gibi üret. Üretici geçersiz kolon/işlev
        // görürse QueryModelException fırlatır → API 400 + üreticinin açıklayıcı mesajı.
        var deneme = new QueryModel(request.VeriHaritasiId, Alanlar: [], Filtre: null, Siralama: [], SatirLimiti: null,
            HesaplananAlanlar: [new ComputedColumn(request.Ifade, request.Ad)]);
        uretici.Generate(deneme, harita);

        Metric metrik;
        var benimId = await MetricSahip.BenimIdAsync(context, kb, cancellationToken);
        if (request.Id is Guid mevcutId)
        {
            metrik = await context.Metrikler.FirstOrDefaultAsync(x => x.Id == mevcutId, cancellationToken)
                ?? throw new KeyNotFoundException("Metrik bulunamadı.");

            // S30 sürümleme: TANIM değişiyorsa önceki hâl geçmişe yazılır (satır = eski hâl).
            // Aktif bayrağı bilerek dışarıda — o durumdur, tanım değil; aç/kapa geçmişi şişirmesin.
            var tanimDegisti = metrik.Ad != request.Ad.Trim()
                || metrik.Ifade != request.Ifade.Trim()
                || metrik.Bicim != request.Bicim.ToString()
                || metrik.Aciklama != (string.IsNullOrWhiteSpace(request.Aciklama) ? null : request.Aciklama.Trim());
            if (tanimDegisti)
            {
                var sonSurum = await context.MetrikSurumleri
                    .Where(v => v.MetrikId == metrik.Id)
                    .MaxAsync(v => (int?)v.SurumNo, cancellationToken) ?? 0;
                context.MetrikSurumleri.Add(new MetricVersion
                {
                    MetrikId = metrik.Id,
                    SurumNo = sonSurum + 1,
                    Ad = metrik.Ad,
                    Ifade = metrik.Ifade,
                    Bicim = metrik.Bicim,
                    Aciklama = metrik.Aciklama,
                    DegistirenId = benimId ?? Guid.Empty,
                });
            }
        }
        else
        {
            metrik = new Metric { SahipId = benimId ?? Guid.Empty };
            context.Metrikler.Add(metrik);
        }

        metrik.Ad = request.Ad.Trim();
        metrik.VeriHaritasiId = request.VeriHaritasiId;
        metrik.Ifade = request.Ifade.Trim();
        metrik.Bicim = request.Bicim.ToString();
        metrik.Aciklama = string.IsNullOrWhiteSpace(request.Aciklama) ? null : request.Aciklama.Trim();
        metrik.Aktif = request.Aktif;
        await context.SaveChangesAsync(cancellationToken);

        return await MetricCommon.OzetAsync(context, metrik, cancellationToken);
    }
}

public sealed class DeleteMetricHandler(KdsDbContext context)
    : IRequestHandler<DeleteMetricRequest, bool>
{
    public async Task<bool> Handle(DeleteMetricRequest request, CancellationToken cancellationToken)
    {
        var metrik = await context.Metrikler.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (metrik is null)
        {
            return false;
        }

        // Silme ENGELLENMEZ (kullanım sayısı onay diyaloğunda gösterilir); metriği kullanan rapor
        // koşumda AÇIK hata verir — sessiz kırılma yok (MetrikCozucu).
        context.Metrikler.Remove(metrik);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class GetMetricUsageHandler(KdsDbContext context)
    : IRequestHandler<GetMetricUsageRequest, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(GetMetricUsageRequest request, CancellationToken cancellationToken) =>
        await MetricCommon.KullananRaporlarAsync(context, request.Id, cancellationToken);
}

/// <summary>Metriğin GÜNCEL değerini ölçer: metriği tek alan yapan kırılımsız model,
/// <see cref="RunQueryModelRequest"/> ile normal koşum hattından geçer — yani RLS, satır sınırı,
/// bekçi ve denetim aynen uygulanır (ayrı bir "ölçüm yolu" açılmaz).</summary>
public sealed class MeasureMetricHandler(KdsDbContext context, ISender gonderici)
    : IRequestHandler<MeasureMetricRequest, MetricMeasureResult>
{
    public async Task<MetricMeasureResult> Handle(MeasureMetricRequest request, CancellationToken cancellationToken)
    {
        var metrik = await context.Metrikler.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Metrik bulunamadı.");

        var kronometre = Stopwatch.StartNew();
        var model = new QueryModel(metrik.VeriHaritasiId, Alanlar: [], Filtre: null, Siralama: [], SatirLimiti: null,
            MetrikAlanlar: [new MetricField(metrik.Id)]);
        var sonuc = await gonderici.Send(new RunQueryModelRequest(model), cancellationToken);
        kronometre.Stop();

        var deger = Alerts.UyariDegerlendirme.Ozetle(sonuc, Contracts.Alerts.OzetTuru.IlkSatir, metrik.Ad);
        return new MetricMeasureResult(deger, MetricCommon.BicimCoz(metrik.Bicim), (int)kronometre.Elapsed.TotalMilliseconds);
    }
}

/// <summary>Sahip çözümü (rapor/uyarı tarafındaki desenle aynı).</summary>
internal static class MetricSahip
{
    public static Task<Guid?> BenimIdAsync(KdsDbContext context, IUserContext kb, CancellationToken ct) =>
        kb.KeycloakId is not Guid kid
            ? Task.FromResult<Guid?>(null)
            : context.Kullanicilar.Where(k => k.KeycloakId == kid).Select(k => (Guid?)k.Id).FirstOrDefaultAsync(ct);
}
