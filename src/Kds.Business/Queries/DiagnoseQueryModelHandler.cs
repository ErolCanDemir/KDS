using Kds.Business.DataMaps;
using Kds.Contracts.Queries;
using Kds.Contracts.QueryModel;
using Kds.Execution;
using MediatR;

namespace Kds.Business.Queries;

/// <summary>M33 "Sorun Ne?" (tasarımcı yolu): modeli koşumla aynı üreticiyle SQL'e çevirir,
/// haritanın bağlantısında <see cref="DiagnoseQueryRequest"/> ile tanılatır (tek kapı).</summary>
public sealed class DiagnoseQueryModelHandler(IDataMapReader haritalar, ISender gonderici, IQueryGenerator uretici)
    : IRequestHandler<DiagnoseQueryModelRequest, QueryDiagnosis>
{
    public async Task<QueryDiagnosis> Handle(DiagnoseQueryModelRequest request, CancellationToken cancellationToken)
    {
        var dataMap = await haritalar.GetDetailAsync(request.Model.HaritaId, cancellationToken)
            ?? throw new KeyNotFoundException("Harita bulunamadı.");

        var uretilen = uretici.Generate(request.Model, dataMap, request.ParametreDegerleri);
        var parametreler = uretilen.Parametreler.ToDictionary(p => p.Key, p => JsonDegerCozucu.Coz(p.Value));

        return await gonderici.Send(
            new DiagnoseQueryRequest(dataMap.BaglantiId, uretilen.Sql, parametreler), cancellationToken);
    }
}
