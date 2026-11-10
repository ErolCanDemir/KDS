namespace Kds.Contracts.Ingest;

/// <summary>İçe alım işi liste kartı (özet + son senkron durumu).
/// <see cref="ArtimliTabloSayisi"/> (S44): tablolardan kaçında artımlı kolon seçili.</summary>
public sealed record IngestJobSummary(
    Guid Id,
    string Ad,
    Guid BaglantiId,
    string? BaglantiAd,
    int TabloSayisi,
    string? Zamanlama,
    bool Aktif,
    DateTimeOffset? SonSenkronTarihi,
    int? SonSenkronSaniye,
    long? SonSatirSayisi,
    string? SonDurum,
    string? SonHata,
    int ArtimliTabloSayisi = 0);
