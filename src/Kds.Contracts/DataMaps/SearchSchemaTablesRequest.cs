using Kds.Common.Cqrs;

namespace Kds.Contracts.DataMaps;

/// <summary>Bir bağlantının şemasında tablo arar (sunucu-taraflı, ilk N). "Tablo Ekle" araması.</summary>
public sealed record SearchSchemaTablesRequest(Guid BaglantiId, string? Arama, string? Sema, int EnCok = 30)
    : IQuery<IReadOnlyList<SchemaTableDto>>;
