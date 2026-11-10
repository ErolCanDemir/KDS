namespace Kds.Contracts.Subscriptions;

/// <summary>Rapor aboneliği tam içeriği (editör için).</summary>
public sealed record SubscriptionDetail(
    Guid Id,
    string Ad,
    Guid RaporId,
    string Bicim,
    IReadOnlyList<string> Alicilar,
    string? Zamanlama,
    bool Aktif,
    IReadOnlyDictionary<string, object?>? ParametreDegerleri = null);
