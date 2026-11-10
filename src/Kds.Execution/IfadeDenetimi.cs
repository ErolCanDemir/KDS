using System.Text.RegularExpressions;

namespace Kds.Execution;

/// <summary>Y1 — hesaplanan kolon formüllerinin denetimi. Formül, SELECT'e giren serbest bir
/// ifadedir (<c>{Tablo.Kolon} - {Tablo.Kolon}</c>, <c>SUM({T.K}) * 100.0 / SUM({T.K2})</c>).
/// Bekçi (ScriptDom) zaten SON SQL'i denetler; burada amaç kullanıcıyı <b>erken ve anlaşılır</b>
/// uyarmak ve ifadeyi ölçülü bir yüzeye kısıtlamak:
/// <list type="bullet">
/// <item>ifade sınırı dışına çıkma denemeleri (<c>;</c>, yorum, alt sorgu, DDL/DML) reddedilir;</item>
/// <item>tanımlı fonksiyon/tip adları dışındaki kelimeler reddedilir (yazım hatası da erken yakalanır);</item>
/// <item>parantez dengesi kontrol edilir.</item>
/// </list>
/// Tek-tırnaklı metin sabitleri serbesttir (içindeki kelimeler denetlenmez) — parametre değildir,
/// SQL'e literal olarak girer ve son SQL yine bekçiden geçer.</summary>
public static class IfadeDenetimi
{
    /// <summary>Formülde kullanılabilen fonksiyonlar (ve CAST için tip adları). Küme BİLEREK dar:
    /// yeni ihtiyaç çıktıkça büyütülür.</summary>
    public static readonly IReadOnlySet<string> IzinliAdlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // toplamalar
        "SUM", "AVG", "MIN", "MAX", "COUNT", "COUNT_BIG", "DISTINCT",
        // sayı / boşluk
        "ISNULL", "COALESCE", "NULLIF", "ABS", "ROUND", "FLOOR", "CEILING", "POWER", "SQRT", "SIGN",
        // metin
        "LEN", "LEFT", "RIGHT", "UPPER", "LOWER", "LTRIM", "RTRIM", "TRIM", "CONCAT", "REPLACE", "SUBSTRING",
        // tarih
        "YEAR", "MONTH", "DAY", "DATEDIFF", "DATEADD", "DATEPART", "EOMONTH", "GETDATE", "CAST", "CONVERT",
        // DATEADD/DATEDIFF parça adları
        "DAYOFYEAR", "WEEK", "WEEKDAY", "HOUR", "MINUTE", "SECOND", "QUARTER", "YY", "MM", "DD", "WK", "QQ",
        // CAST … AS <tip>
        "AS", "DECIMAL", "NUMERIC", "INT", "BIGINT", "SMALLINT", "FLOAT", "REAL", "MONEY",
        "VARCHAR", "NVARCHAR", "CHAR", "NCHAR", "DATE", "DATETIME", "DATETIME2", "BIT",
        // mantık (CASE zaten ayrı bir özellik ama ifade içinde de anlamlı)
        "CASE", "WHEN", "THEN", "ELSE", "END", "AND", "OR", "NOT", "NULL", "IIF", "CHOOSE",
    };

    /// <summary>Toplama (aggregate) fonksiyonları — ifade bunlardan birini içeriyorsa SATIR değil
    /// GRUP düzeyindedir: GROUP BY'a EKLENMEZ (bileşik/CASE kolonlarının tersine).</summary>
    public static readonly IReadOnlySet<string> ToplamaAdlari = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SUM", "AVG", "MIN", "MAX", "COUNT", "COUNT_BIG",
    };

    /// <summary>İfade bir toplama fonksiyonu çağırıyor mu (<c>SUM(</c>, <c>COUNT(</c> …)?
    /// Grup düzeyi/satır düzeyi ayrımı buna bakar.</summary>
    public static bool ToplamaIceriyorMu(string? ifade) =>
        !string.IsNullOrWhiteSpace(ifade)
        && ToplamaAdlari.Any(t => Regex.IsMatch(ifade, $@"\b{t}\s*\(", RegexOptions.IgnoreCase));

    private static readonly string[] YasakParcalar = [";", "--", "/*", "*/"];
    private static readonly IReadOnlySet<string> YasakAdlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "DROP", "ALTER", "CREATE", "TRUNCATE",
        "EXEC", "EXECUTE", "GRANT", "REVOKE", "UNION", "INTO", "FROM", "WHERE", "JOIN", "OPENROWSET",
        "OPENQUERY", "OPENDATASOURCE", "WAITFOR", "SHUTDOWN",
    };

    private static readonly Regex Kelime = new(@"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);

    /// <summary>İfadeyi denetler; sorun varsa açıklayıcı mesajla <see cref="QueryModelException"/>
    /// fırlatır. <paramref name="metin"/> yer tutucuları ÇÖZÜLMÜŞ hâlde de verilebilir.
    /// Dönen değer: ifade bir TOPLAMA içeriyor mu (GROUP BY kararı için).</summary>
    public static bool Dogrula(string? metin, string nerede = "Hesaplanan kolon")
    {
        if (string.IsNullOrWhiteSpace(metin))
            throw new QueryModelException($"{nerede}: formül boş olamaz.");

        foreach (var y in YasakParcalar)
            if (metin.Contains(y, StringComparison.Ordinal))
                throw new QueryModelException($"{nerede}: '{y}' kullanılamaz — burada yalnız bir HESAP yazılır.");

        // Metin sabitlerini ('…') denetim dışı bırak: içindeki kelimeler veri, kod değil.
        var kod = Regex.Replace(metin, @"'([^']|'')*'", " ");

        var derinlik = 0;
        foreach (var c in kod)
        {
            if (c == '(') derinlik++;
            else if (c == ')' && --derinlik < 0)
                throw new QueryModelException($"{nerede}: parantezler eşleşmiyor.");
        }
        if (derinlik != 0)
            throw new QueryModelException($"{nerede}: parantezler eşleşmiyor.");

        var toplamaVar = false;
        foreach (Match m in Kelime.Matches(kod))
        {
            var ad = m.Value;
            if (YasakAdlar.Contains(ad))
                throw new QueryModelException(
                    $"{nerede}: '{ad}' burada kullanılamaz — formül yalnız hesap içerir (sorgu yazılmaz).");
            if (!IzinliAdlar.Contains(ad))
                throw new QueryModelException(
                    $"{nerede}: '{ad}' tanınmıyor. Kolonları {{Tablo.Kolon}} biçiminde yazın; " +
                    "kullanılabilir fonksiyonlar: " + string.Join(", ", IzinliAdlar.Take(12)) + " …");
            if (ToplamaAdlari.Contains(ad)) toplamaVar = true;
        }

        return toplamaVar;
    }
}
