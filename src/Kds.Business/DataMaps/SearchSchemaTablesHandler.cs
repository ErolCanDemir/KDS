using Kds.Execution;
using Kds.Contracts.DataMaps;
using MediatR;

namespace Kds.Business.DataMaps;

/// <summary>"Tablo Ekle" araması — bağlantının şemasında sunucu-taraflı tablo arama.</summary>
public sealed class SearchSchemaTablesHandler(ConnectionStringResolver cozucu, ISchemaDiscovery kesif)
    : IRequestHandler<SearchSchemaTablesRequest, IReadOnlyList<SchemaTableDto>>
{
    public async Task<IReadOnlyList<SchemaTableDto>> Handle(SearchSchemaTablesRequest request, CancellationToken cancellationToken)
    {
        var connectionString = await cozucu.ResolveAsync(request.BaglantiId, cancellationToken);
        var tablolar = await kesif.SearchTablesAsync(connectionString, request.Arama, request.Sema, request.EnCok, cancellationToken);
        return tablolar.Select(t => new SchemaTableDto(t.Sema, t.Ad, t.KolonSayisi)).ToList();
    }
}
