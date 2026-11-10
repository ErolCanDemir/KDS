namespace Kds.Execution;

/// <summary>Kaynak DB'den seçili tabloları tenant deposuna kopyalar (S12/Mod B): depo DB'yi yoksa
/// yaratır, her tabloyu TAM (DROP+CREATE+SqlBulkCopy) ya da ARTIMLI (S44: su seviyesi + staging +
/// MERGE) yükler. Saf ADO.NET — Kds.Data'a referans yok.</summary>
public interface IIngestRunner
{
    Task<IngestOutcome> RunAsync(string kaynakDizesi, string depoSunucuDizesi, string depoDbAdi,
        IReadOnlyList<IngestTableSpec> tablolar, IReadOnlyDictionary<string, string> suSeviyeleri,
        CancellationToken cancellationToken);

    /// <summary>M22b — hedefi HAZIR (var olan) bir veritabanı: DB yaratılmaz;
    /// <paramref name="depoDizesi"/> kullanıcının seçtiği hedef bağlantının TAM dizesidir.</summary>
    Task<IngestOutcome> RunHedefeAsync(string kaynakDizesi, string depoDizesi,
        IReadOnlyList<IngestTableSpec> tablolar, IReadOnlyDictionary<string, string> suSeviyeleri,
        CancellationToken cancellationToken);
}

/// <summary>Çekilecek tek kaynak tablo. <see cref="Kolonlar"/> null → tüm kolonlar.
/// <see cref="ArtimliKolon"/> (S44) doluysa tablo artımlı yüklenir; koşucu gerekince
/// (su seviyesi yok / hedef tablo yok / şema kayması / PK yok) TAM yüklemeye düşer.</summary>
public sealed record IngestTableSpec(string Sema, string Tablo, IReadOnlyList<string>? Kolonlar,
    string? ArtimliKolon = null);

/// <summary>Koşu sonucu. <see cref="YeniSuSeviyeleri"/>: artımlı tabloların bu koşu SONUNDAKİ su
/// seviyeleri ("sema.tablo" → değer) — çağıran saklar, sonraki koşuya geri verir. Tam yüklenen
/// artımlı tablo da yeni seviyesini alır (ilk koşu → sonraki koşu artımlı olabilsin).</summary>
public sealed record IngestOutcome(int TabloSayisi, long ToplamSatir,
    int ArtimliTabloSayisi = 0,
    IReadOnlyDictionary<string, string>? YeniSuSeviyeleri = null);
