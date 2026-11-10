using Kds.Common.Cqrs;

namespace Kds.Contracts.DataMaps;

/// <summary>Bir tabloyu ilgilendiren FK ilişki önerilerini getirir.</summary>
public sealed record RelationshipSuggestionsRequest(Guid BaglantiId, string Sema, string Tablo)
    : IQuery<IReadOnlyList<SchemaRelationshipDto>>;
