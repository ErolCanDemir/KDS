namespace Kds.Contracts.DataMaps;

/// <summary>Şema keşfi — FK'dan çıkarılan ilişki önerisi.</summary>
public sealed record SchemaRelationshipDto(
    string KaynakSema, string KaynakTablo, string KaynakKolon,
    string HedefSema, string HedefTablo, string HedefKolon);
