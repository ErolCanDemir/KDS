using Kds.Common.Cqrs;

namespace Kds.Contracts.DataMaps;

/// <summary>Bir tablonun kolonlarını getirir (tembel — tablo haritaya eklenince).</summary>
public sealed record GetTableColumnsRequest(Guid BaglantiId, string Sema, string Tablo)
    : IQuery<IReadOnlyList<SchemaColumnDto>>;
