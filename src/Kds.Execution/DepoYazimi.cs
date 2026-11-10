using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Kds.Execution;

/// <summary>Depo tablosunun NÖTR kolon tanımı — <c>DbColumn</c>'a (doğrudan içe alım) ya da ajan
/// DTO'suna (S45 push) bağlı değil; iki yol da buna eşlenir ve AYNI DDL/MERGE üreticilerinden
/// geçer. Kopyalansaydı iki yazım yolu zamanla ayrışırdı.</summary>
public sealed record DepoKolon(
    string Ad, string Tip, int? Boyut, byte? Hassasiyet, byte? Olcek, bool NullOlabilir);

/// <summary>S44/S45 ortak depo yazım üreticileri: CREATE/DROP DDL, MERGE, indeks seçimi, ad kaçışı.
/// Saf metin üretimi — bağlantı açmaz.</summary>
public static class DepoYazimi
{
    /// <summary>Tablo başına kurulacak en fazla indeks sayısı.</summary>
    public const int IndeksSiniri = 12;

    public static string Kacir(string ad) => ad.Replace("]", "]]");

    /// <summary>Şema + DROP + CREATE (hedef ad staging için farklı verilebilir).</summary>
    public static string TabloDdl(string sema, string hedefTablo, IReadOnlyList<DepoKolon> kolonlar)
    {
        var sb = new StringBuilder();
        sb.Append($"IF SCHEMA_ID(N'{sema.Replace("'", "''")}') IS NULL EXEC('CREATE SCHEMA [{Kacir(sema)}]'); ");
        sb.Append($"IF OBJECT_ID(N'[{Kacir(sema)}].[{Kacir(hedefTablo)}]') IS NOT NULL ");
        sb.Append($"DROP TABLE [{Kacir(sema)}].[{Kacir(hedefTablo)}]; ");
        sb.Append($"CREATE TABLE [{Kacir(sema)}].[{Kacir(hedefTablo)}] (");
        sb.Append(string.Join(", ", kolonlar.Select(KolonTanimi)));
        sb.Append(");");
        return sb.ToString();
    }

    /// <summary>Tek kolonun CREATE TABLE tanımı (MSSQL tip + uzunluk/precision + NULL).
    /// <c>timestamp/rowversion</c> hedefe explicit yazılamaz (sunucu üretir) → değeri (8 bayt)
    /// KORUMAK için <c>binary(8)</c> olarak alınır; SqlBulkCopy binary'ye yazabilir. Computed
    /// kolonlar burada DÜZ kolon olur (değeri kaynağın SELECT'inden gelir; hesaplanmışlığı
    /// taşınmaz) — yani her ikisi de "atlanmadan" değeriyle çekilir.</summary>
    public static string KolonTanimi(DepoKolon k)
    {
        var tip = k.Tip.ToLowerInvariant();
        if (tip is "timestamp" or "rowversion")
        {
            return $"[{Kacir(k.Ad)}] binary(8) NULL";
        }

        var boyut = tip switch
        {
            "varchar" or "char" or "varbinary" or "binary" or "nvarchar" or "nchar" =>
                k.Boyut is int s && s is > 0 and < int.MaxValue ? $"({s})" : "(max)",
            "decimal" or "numeric" => $"({k.Hassasiyet ?? 18},{k.Olcek ?? 0})",
            "datetime2" or "time" or "datetimeoffset" => k.Olcek is byte sc ? $"({sc})" : "",
            _ => "",
        };
        var bos = k.NullOlabilir ? "NULL" : "NOT NULL";
        return $"[{Kacir(k.Ad)}] {tip}{boyut} {bos}";
    }

    /// <summary>Staging → hedef MERGE cümlesi (PK üzerinden; varsa güncelle, yoksa ekle). PK dışı
    /// kolon yoksa (tablo yalnız anahtardan oluşuyorsa) UPDATE dalı yazılmaz.</summary>
    public static string MergeUret(string sema, string tablo, string stagingAd,
        IReadOnlyList<string> kolonAdlari, IReadOnlyList<string> pk)
    {
        var hedefTam = $"[{Kacir(sema)}].[{Kacir(tablo)}]";
        var stagingTam = $"[{Kacir(sema)}].[{Kacir(stagingAd)}]";
        var pkKume = pk.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var guncellenecek = kolonAdlari.Where(a => !pkKume.Contains(a)).ToList();

        var sb = new StringBuilder();
        sb.Append($"MERGE {hedefTam} AS hedef USING {stagingTam} AS delta ON ");
        sb.Append(string.Join(" AND ", pk.Select(p => $"hedef.[{Kacir(p)}] = delta.[{Kacir(p)}]")));
        if (guncellenecek.Count > 0)
        {
            sb.Append(" WHEN MATCHED THEN UPDATE SET ");
            sb.Append(string.Join(", ", guncellenecek.Select(a => $"hedef.[{Kacir(a)}] = delta.[{Kacir(a)}]")));
        }

        sb.Append(" WHEN NOT MATCHED THEN INSERT (");
        sb.Append(string.Join(", ", kolonAdlari.Select(a => $"[{Kacir(a)}]")));
        sb.Append(") VALUES (");
        sb.Append(string.Join(", ", kolonAdlari.Select(a => $"delta.[{Kacir(a)}]")));
        sb.Append(");");
        return sb.ToString();
    }

    /// <summary>Depo tablosu için indeks DDL'leri — raporlamanın gerçekte süzdüğü/kırdığı iki kolon
    /// ailesi: TARİH kolonları (zaman kırılımı ve tarih filtresi her raporda var) ve ANAHTAR
    /// kolonları (<c>Id</c> ile biten sayısal/GUID kolonlar — kırılım ve birleştirme oradan olur).
    /// Tek kolonluk indeks yeter: <c>DATEADD(month, DATEDIFF(...))</c> gibi ifadeler indeksi
    /// arama için kullanamaz ama TARAMAYI tek kolona indirir — asıl kazanç budur.
    /// Tablo başına <see cref="IndeksSiniri"/> ile sınırlıdır; geniş tablolarda yükleme süresini ve
    /// depo boyutunu indeks enflasyonuna kaptırmamak için.</summary>
    public static IEnumerable<string> IndeksDdlUret(string sema, string tablo,
        IReadOnlyList<DepoKolon> kolonlar)
    {
        var sayac = 0;
        foreach (var k in kolonlar)
        {
            if (sayac >= IndeksSiniri)
            {
                yield break;
            }

            var tip = k.Tip.ToLowerInvariant();
            var tarih = tip is "date" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset";
            var anahtar = (k.Ad.Equals("Id", StringComparison.OrdinalIgnoreCase)
                           || k.Ad.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                          && tip is "int" or "bigint" or "smallint" or "tinyint" or "uniqueidentifier";
            if (!tarih && !anahtar)
            {
                continue;
            }

            sayac++;
            yield return $"CREATE INDEX [IX_{Kacir(tablo)}_{Kacir(k.Ad)}] " +
                         $"ON [{Kacir(sema)}].[{Kacir(tablo)}] ([{Kacir(k.Ad)}]);";
        }
    }
}

/// <summary>S44/S45 ortak su-seviyesi (watermark) dönüşümleri: saklanan metin ↔ SQL parametresi /
/// MAX sonucu. İçe alım koşucusu ve ajan AYNI biçimi kullanır — biri hex yazıp diğeri onu sayı
/// diye okusaydı satırlar sessizce atlanırdı.</summary>
public static class SuSeviyesi
{
    /// <summary>Değeri SQL parametresine çevirir (kolonun kaynak tipine göre). Tanınmayan tipte
    /// metin karşılaştırması yapılır. Çözülemeyen değer FormatException fırlatır — çağıran TAM
    /// yüklemeye düşer.</summary>
    public static SqlParameter Parametre(string ad, string tip, string deger) => tip switch
    {
        "timestamp" or "rowversion" or "binary" or "varbinary" =>
            new SqlParameter(ad, SqlDbType.Binary, 8) { Value = HexCoz(deger) },
        "int" or "bigint" or "smallint" or "tinyint" =>
            new SqlParameter(ad, SqlDbType.BigInt) { Value = long.Parse(deger, CultureInfo.InvariantCulture) },
        "decimal" or "numeric" or "money" or "smallmoney" =>
            new SqlParameter(ad, SqlDbType.Decimal) { Value = decimal.Parse(deger, CultureInfo.InvariantCulture) },
        "date" or "datetime" or "datetime2" or "smalldatetime" =>
            new SqlParameter(ad, SqlDbType.DateTime2)
            { Value = DateTime.Parse(deger, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) },
        "datetimeoffset" =>
            new SqlParameter(ad, SqlDbType.DateTimeOffset)
            { Value = DateTimeOffset.Parse(deger, CultureInfo.InvariantCulture) },
        _ => new SqlParameter(ad, deger),
    };

    /// <summary>MAX sonucu → saklanabilir metin. Kolon tipine göre kayıpsız (round-trip) biçim.</summary>
    public static string? Bicimle(object? deger) => deger switch
    {
        null or DBNull => null,
        byte[] b => "0x" + Convert.ToHexString(b),
        DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => deger.ToString(),
    };

    private static byte[] HexCoz(string deger)
    {
        var ham = deger.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? deger[2..] : deger;
        try { return Convert.FromHexString(ham); }
        catch (FormatException) { throw new FormatException($"'{deger}' onaltılık değere çözülemedi."); }
    }
}
