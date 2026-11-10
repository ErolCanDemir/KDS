namespace Kds.Execution;

public sealed record QueryResult(
    IReadOnlyList<QueryColumn> Kolonlar,
    IReadOnlyList<object?[]> Satirlar,
    bool SatirSiniriUygulandi,
    TimeSpan Sure);

public sealed record QueryColumn(string Ad, string VeriTipi);
