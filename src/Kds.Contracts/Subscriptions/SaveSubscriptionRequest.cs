using Kds.Common.Cqrs;

namespace Kds.Contracts.Subscriptions;

/// <summary>Rapor aboneliğini kaydeder: <see cref="Id"/> boşsa yeni, doluysa günceller. Yalnız
/// Admin+Geliştirici. <see cref="Bicim"/> = "Pdf" | "Xlsx" | "Csv"; <see cref="Alicilar"/> = e-posta
/// adresleri; <see cref="Zamanlama"/> boş → yalnız elle gönderilir.</summary>
public sealed record SaveSubscriptionRequest(
    Guid? Id,
    string Ad,
    Guid RaporId,
    string Bicim,
    IReadOnlyList<string> Alicilar,
    string? Zamanlama,
    bool Aktif,
    // M24/M25: parametreli raporun çalıştırma değerleri (anahtar→değer; TarihAralik ucu "$bugun"
    // olabilir → gönderim günü). Null = değersiz (parametresiz rapor).
    IReadOnlyDictionary<string, object?>? ParametreDegerleri = null) : ICommand<SubscriptionSummary>;
