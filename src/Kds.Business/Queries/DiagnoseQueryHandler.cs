using Kds.Contracts.Queries;
using Kds.Execution;
using MediatR;

namespace Kds.Business.Queries;

/// <summary>M33 "Sorun Ne?" (ham SQL yolu): bağlantı dizesini koşumla AYNI yönlendirmeyle çözer
/// (Canlı/İçe Alım + M22b depo hedefi) ve tanılayıcıya verir — sorgu çalıştırılmaz, tahmini plan
/// yorumlanır. Parametreler wire'dan JsonElement gelebilir; CLR'a çözülür (koşumla aynı).</summary>
public sealed class DiagnoseQueryHandler(
    QueryConnectionResolver cozucu, IQueryDiagnostician tanici, Settings.ISistemAyarlari ayarlar)
    : IRequestHandler<DiagnoseQueryRequest, QueryDiagnosis>
{
    public async Task<QueryDiagnosis> Handle(DiagnoseQueryRequest request, CancellationToken cancellationToken)
    {
        var connectionString = await cozucu.ResolveAsync(request.BaglantiId, cancellationToken);
        var parametreler = (request.Parametreler ?? new Dictionary<string, object?>())
            .ToDictionary(p => p.Key, p => JsonDegerCozucu.Coz(p.Value));
        // S16/A3: plan zaman aşımı ve bulgu eşikleri Sistem Ayarları'ndan (koddaki sabitler değil).
        return await tanici.DiagnoseAsync(
            new DiagnosisRequest(connectionString, request.Sql, parametreler,
                ayarlar.Sayi(Settings.AyarTanimlari.TaniPlanZamanAsimiSn),
                ayarlar.Sayi(Settings.AyarTanimlari.TaniTaramaEsigi),
                ayarlar.Sayi(Settings.AyarTanimlari.TaniSiralamaEsigi)),
            cancellationToken);
    }
}
