namespace Kds.Contracts.Audit;

/// <summary>Denetim listesi sayfası: toplam (filtreye göre) + o sayfadaki kayıtlar.</summary>
public sealed record AuditPage(int Toplam, IReadOnlyList<AuditRecordDto> Kayitlar);
