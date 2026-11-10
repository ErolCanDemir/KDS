using Kds.Execution;
using Kds.Contracts.DataMaps;
using MediatR;

namespace Kds.Business.DataMaps;

/// <summary>Tek tablonun kolonları (tembel — tablo haritaya eklenince).</summary>
public sealed class GetTableColumnsHandler(ConnectionStringResolver cozucu, ISchemaDiscovery kesif)
    : IRequestHandler<GetTableColumnsRequest, IReadOnlyList<SchemaColumnDto>>
{
    public async Task<IReadOnlyList<SchemaColumnDto>> Handle(GetTableColumnsRequest request, CancellationToken cancellationToken)
    {
        var connectionString = await cozucu.ResolveAsync(request.BaglantiId, cancellationToken);
        var kolonlar = await kesif.GetColumnsAsync(connectionString, request.Sema, request.Tablo, cancellationToken);
        return kolonlar.Select(k => new SchemaColumnDto(k.Ad, k.VeriTipi, k.NullOlabilir, k.BirincilAnahtarMi)).ToList();
    }
}
