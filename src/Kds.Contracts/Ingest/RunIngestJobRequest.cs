using Kds.Common.Cqrs;

namespace Kds.Contracts.Ingest;

/// <summary>İçe alım işini ŞİMDİ çalıştırır (elle tetik, Faz 1): kaynaktan seçili tabloları tenant
/// deposuna yükler. Sonuç: tablo/satır/süre. Durum işe yazılır.
/// <see cref="TamYukleme"/> (S44): true → artımlı su seviyeleri SIFIRLANIR ve her tablo baştan
/// yüklenir; artımlının taşımadığı silinmiş satırlar böyle temizlenir.</summary>
public sealed record RunIngestJobRequest(Guid Id, bool TamYukleme = false) : ICommand<IngestRunResult>;
