using Kds.Common.Cqrs;

namespace Kds.Contracts.Audit;

/// <summary>Tenant'ın denetim kayıtlarını filtreli + sayfalı listeler (en yeni önce). Yalnız Admin.
/// <see cref="Basarili"/> null → hepsi; <see cref="Arama"/> SQL metninde geçen; tarih aralığı OlusturmaTarihi'ne göre.</summary>
public sealed record ListAuditRequest(
    int Sayfa = 0,
    int SayfaBoyut = 50,
    bool? Basarili = null,
    string? Arama = null,
    DateTimeOffset? Baslangic = null,
    DateTimeOffset? Bitis = null) : IQuery<AuditPage>;
