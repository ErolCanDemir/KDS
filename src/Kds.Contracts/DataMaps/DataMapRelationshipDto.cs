namespace Kds.Contracts.DataMaps;

/// <summary>Haritadaki iki tablo arasında join ilişkisi (FK'dan gelmiş olabilir).</summary>
public sealed record DataMapRelationshipDto(
    Guid KaynakTabloId,
    string KaynakKolon,
    Guid HedefTabloId,
    string HedefKolon,
    bool FktenMi);
