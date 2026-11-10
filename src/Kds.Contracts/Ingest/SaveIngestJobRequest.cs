using Kds.Common.Cqrs;

namespace Kds.Contracts.Ingest;

/// <summary>İçe alım işini kaydeder: <see cref="Id"/> boşsa newEntity, doluysa günceller. Yalnız
/// Admin+Geliştirici. <see cref="Tablolar"/> = çekilecek kaynak tablolar/kolonlar.</summary>
public sealed record SaveIngestJobRequest(
    Guid? Id,
    string Ad,
    Guid BaglantiId,
    IReadOnlyList<IngestTableDto> Tablolar,
    string? Zamanlama,
    bool Aktif) : ICommand<IngestJobSummary>;
