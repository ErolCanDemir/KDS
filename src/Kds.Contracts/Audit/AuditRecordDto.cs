namespace Kds.Contracts.Audit;

/// <summary>Denetim ekranı satırı: bir sorgu icrasının kim/ne/ne zaman/sonuç özeti. Parola/iş verisi
/// tutulmaz — yalnız SQL metni + parametre değerleri + ölçümler (00-analiz §7).</summary>
public sealed record AuditRecordDto(
    Guid Id,
    DateTimeOffset Zaman,
    string? KullaniciAd,
    string? BaglantiAd,
    string Sql,
    string? Parametreler,
    bool Basarili,
    int? SatirSayisi,
    int SureMs,
    string? Hata,
    // M26: kayıtlı rapor koşumuysa raporun adı (serbest tasarımcı/SQL koşumunda null).
    string? RaporAd = null);
