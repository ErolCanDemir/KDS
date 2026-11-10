namespace Kds.Contracts.Subscriptions;

/// <summary>Rapor aboneliği liste kartı (özet + son gönderim durumu).</summary>
public sealed record SubscriptionSummary(
    Guid Id,
    string Ad,
    Guid RaporId,
    string? RaporAd,
    string Bicim,
    int AliciSayisi,
    string? Zamanlama,
    bool Aktif,
    DateTimeOffset? SonGonderimTarihi,
    int? SonAliciSayisi,
    long? SonSatirSayisi,
    string? SonDurum,
    string? SonHata);
