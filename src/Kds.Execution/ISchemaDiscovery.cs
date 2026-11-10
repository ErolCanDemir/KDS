namespace Kds.Execution;

/// <summary>Hedef DB şemasını <c>sys.*</c> kataloğundan okur; Veri Haritası tasarımı (S6)
/// buradan beslenir. Büyük şema (600+ tablo) için üç dar metot: tablo ARAMA (sunucu-taraflı,
/// ilk N), tek-tablo KOLON (tembel), ve FK İLİŞKİ önerisi. Kds.Data'a referans vermez.</summary>
public interface ISchemaDiscovery
{
    /// <summary>Tabloları sunucu-taraflı arar (ad LIKE); en çok <paramref name="enCok"/> döner.
    /// Tüm tablolar tarayıcıya yüklenmez.</summary>
    Task<IReadOnlyList<SchemaTable>> SearchTablesAsync(
        string baglantiDizesi, string? arama, string? sema, int enCok, CancellationToken cancellationToken);

    /// <summary>Tek bir tablonun kolonlarını getirir (tembel — tablo eklenince çağrılır).</summary>
    Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(
        string baglantiDizesi, string sema, string tablo, CancellationToken cancellationToken);

    /// <summary>Bir tabloyu ilgilendiren foreign key ilişkilerini getirir (ilişki önerisi için).</summary>
    Task<IReadOnlyList<ForeignKey>> GetRelationshipsAsync(
        string baglantiDizesi, string sema, string tablo, CancellationToken cancellationToken);
}

public sealed record SchemaTable(string Sema, string Ad, int KolonSayisi);

public sealed record ColumnInfo(string Ad, string VeriTipi, bool NullOlabilir, bool BirincilAnahtarMi);

public sealed record ForeignKey(
    string KaynakSema, string KaynakTablo, string KaynakKolon,
    string HedefSema, string HedefTablo, string HedefKolon);
