namespace Kds.Contracts.Ingest;

/// <summary>İçe alım işi tam içeriği (editör için): seçilen tablolar + zamanlama.</summary>
public sealed record IngestJobDetail(
    Guid Id,
    string Ad,
    Guid BaglantiId,
    IReadOnlyList<IngestTableDto> Tablolar,
    string? Zamanlama,
    bool Aktif);
