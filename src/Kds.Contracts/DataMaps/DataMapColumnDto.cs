namespace Kds.Contracts.DataMaps;

/// <summary>Haritadaki bir tablonun kolonu (dostça ad + rol + veri tipi).</summary>
public sealed record DataMapColumnDto(
    string HamAd,
    string? DostcaAd,
    ColumnRole Rol,
    string? VeriTipi);
