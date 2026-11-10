using Kds.Execution;
using Kds.Contracts.DataMaps;
using MediatR;

namespace Kds.Business.DataMaps;

/// <summary>Bir tablonun FK ilişki önerileri.</summary>
public sealed class RelationshipSuggestionsHandler(ConnectionStringResolver cozucu, ISchemaDiscovery kesif)
    : IRequestHandler<RelationshipSuggestionsRequest, IReadOnlyList<SchemaRelationshipDto>>
{
    public async Task<IReadOnlyList<SchemaRelationshipDto>> Handle(RelationshipSuggestionsRequest request, CancellationToken cancellationToken)
    {
        var connectionString = await cozucu.ResolveAsync(request.BaglantiId, cancellationToken);
        var fk = await kesif.GetRelationshipsAsync(connectionString, request.Sema, request.Tablo, cancellationToken);
        return fk.Select(f => new SchemaRelationshipDto(f.KaynakSema, f.KaynakTablo, f.KaynakKolon, f.HedefSema, f.HedefTablo, f.HedefKolon)).ToList();
    }
}
