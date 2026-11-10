namespace Kds.Contracts.Ingest;

/// <summary>İçe alım işinde seçilen bir kaynak tablo. <see cref="Kolonlar"/> null → tablonun TÜM
/// kolonları çekilir (bulk); dolu → yalnız seçilen kolonlar (curated). Depo tablosu bu seçime göre
/// otomatik yaratılır.
/// <see cref="ArtimliKolon"/> (S44): doluysa tablo ARTIMLI yüklenir — her koşuda yalnız bu kolonu
/// son kaydedilen değerden büyük/eşit satırlar çekilip kaynak PK'sı üzerinden MERGE edilir.
/// Uygun kolonlar: rowversion (güncellemeleri de yakalar), artan Id, kayıt/değişiklik tarihi.
/// null → tam yükleme (DROP+CREATE, bugünkü davranış).</summary>
public sealed record IngestTableDto(string Sema, string Tablo, IReadOnlyList<string>? Kolonlar,
    string? ArtimliKolon = null);
