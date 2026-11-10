using System.Globalization;
using System.Text.Json;

namespace Kds.Execution;

/// <summary>S45 — ajan protokolündeki DEĞER KODLAMASI, tek yerde: ajan yazar (<see cref="Yaz"/>),
/// sunucu çözer (<see cref="Coz"/>), DataTable kolon tipi de aynı haritadan gelir
/// (<see cref="ClrTipi"/>). İki taraf ayrı kodlansaydı (ör. biri decimal'i sayı, diğeri metin
/// beklese) değerler sessizce yuvarlanır ya da koşu düşerdi.
///
/// Kurallar: binary/rowversion=base64 · decimal ailesi=invariant METİN (JSON double'ına sığmaz;
/// hassasiyet kaybı sessiz yanlıştır) · tarih=ISO round-trip ("O") · Guid=metin · sayılar=JSON
/// sayısı · bit=JSON bool. Dar-tipli parse — IO adaptörü sınırı (AGENTS §4.6).</summary>
public static class AjanVeri
{
    /// <summary>Kolonun DataTable/CLR tipi (SqlBulkCopy hedef tipe buna göre çevirir).</summary>
    public static Type ClrTipi(string tip) => tip.ToLowerInvariant() switch
    {
        "bigint" => typeof(long),
        "int" => typeof(int),
        "smallint" => typeof(short),
        "tinyint" => typeof(byte),
        "bit" => typeof(bool),
        "decimal" or "numeric" or "money" or "smallmoney" => typeof(decimal),
        "float" => typeof(double),
        "real" => typeof(float),
        "date" or "datetime" or "datetime2" or "smalldatetime" => typeof(DateTime),
        "datetimeoffset" => typeof(DateTimeOffset),
        "time" => typeof(TimeSpan),
        "uniqueidentifier" => typeof(Guid),
        "binary" or "varbinary" or "timestamp" or "rowversion" or "image" => typeof(byte[]),
        _ => typeof(string),
    };

    /// <summary>Ajan tarafı: okunan CLR değerini JSON-taşınabilir hâle getirir.</summary>
    public static object? Yaz(object? deger) => deger switch
    {
        null or DBNull => null,
        byte[] b => Convert.ToBase64String(b),
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        DateTime t => t.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset t => t.ToString("O", CultureInfo.InvariantCulture),
        TimeSpan t => t.ToString("c", CultureInfo.InvariantCulture),
        Guid g => g.ToString(),
        _ => deger,
    };

    /// <summary>Sunucu tarafı: JSON değerini kolonun tipine göre CLR'a çözer. Çözülemeyen değer
    /// FormatException türevi fırlatır — parti reddedilir, koşu HATA olarak görünür (yanlış tipe
    /// sessizce dökmek yerine).</summary>
    public static object? Coz(string tip, JsonElement e)
    {
        if (e.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var t = tip.ToLowerInvariant();
        return t switch
        {
            "bigint" => e.ValueKind == JsonValueKind.String ? long.Parse(e.GetString()!, CultureInfo.InvariantCulture) : e.GetInt64(),
            "int" => e.GetInt32(),
            "smallint" => e.GetInt16(),
            "tinyint" => e.GetByte(),
            "bit" => e.ValueKind == JsonValueKind.Number ? e.GetInt32() != 0 : e.GetBoolean(),
            "decimal" or "numeric" or "money" or "smallmoney" =>
                e.ValueKind == JsonValueKind.String
                    ? decimal.Parse(e.GetString()!, CultureInfo.InvariantCulture)
                    : e.GetDecimal(),
            "float" => e.GetDouble(),
            "real" => e.GetSingle(),
            "date" or "datetime" or "datetime2" or "smalldatetime" =>
                DateTime.Parse(e.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "datetimeoffset" => DateTimeOffset.Parse(e.GetString()!, CultureInfo.InvariantCulture),
            "time" => TimeSpan.Parse(e.GetString()!, CultureInfo.InvariantCulture),
            "uniqueidentifier" => e.GetGuid(),
            "binary" or "varbinary" or "timestamp" or "rowversion" or "image" =>
                Convert.FromBase64String(e.GetString()!),
            _ => e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText(),
        };
    }
}
