namespace Kds.Contracts.DataMaps;

/// <summary>Şema keşfi — bir tablonun kolonu.</summary>
public sealed record SchemaColumnDto(string Ad, string VeriTipi, bool NullOlabilir, bool BirincilAnahtarMi);
