namespace Kds.Business.Connections;

/// <summary>Bir bağlantı denemesinin sonucu (paylaşılan yanıt: Test/TestById döner). Süre ms;
/// hata varsa kısa mesaj.</summary>
public sealed record ConnectionTestResult(bool Basarili, int? Ms, string? Mesaj);
