using Kds.Business.DataMaps;
using Kds.Execution;
using Kds.Contracts.QueryModel;
using MediatR;

namespace Kds.Business.Queries;

/// <summary>"SQL'i Göster": sorgu modelini existing tenant'ın Veri Haritası'na göre (tenant-filtreli
/// yüklenir) parametreli SQL'e çevirir. Çalıştırmaz — yalnız üretir. Harita yoksa/başka tenant'ınsa
/// bulunamaz (yetki + izolasyon global query filter'dan gelir).</summary>
public sealed class GenerateQuerySqlHandler(IDataMapReader haritalar, IQueryGenerator uretici)
    : IRequestHandler<GenerateQuerySqlRequest, GeneratedQuery>
{
    public async Task<GeneratedQuery> Handle(GenerateQuerySqlRequest request, CancellationToken cancellationToken)
    {
        var dataMap = await haritalar.GetDetailAsync(request.Model.HaritaId, cancellationToken)
            ?? throw new KeyNotFoundException("Harita bulunamadı.");
        return uretici.Generate(request.Model, dataMap, request.ParametreDegerleri);
    }
}
