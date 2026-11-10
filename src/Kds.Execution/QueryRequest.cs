namespace Kds.Execution;

/// <summary>Bir icra isteği. SQL daima parametreli gelir (00-analiz §5); satır
/// sınırı ve zaman aşımı zorunludur — sınırsız sorgu yoktur (00-analiz §7).</summary>
public sealed record QueryRequest(
    string BaglantiDizesi,
    string Sql,
    IReadOnlyDictionary<string, object?> Parametreler,
    int SatirSiniri,
    TimeSpan ZamanAsimi);
