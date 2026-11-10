using System.Globalization;
using System.Text.RegularExpressions;

namespace Kds.Execution;

/// <summary>Y5 — dinamik (kayan) tarih ifadeleri. Rapor kaydedildiğinde sabit tarih yazılırsa
/// bir hafta sonra yanlış pencereyi gösterir; bu ifadeler <b>her çalıştırmada</b> o günün
/// tarihine göre çözülür.
/// <para>Dilbilgisi: <c>$taban[±sayıbirim]</c> — taban: <c>bugun</c> · <c>haftaBasi</c> ·
/// <c>ayBasi</c> · <c>yilBasi</c>; birim: <c>gun</c> · <c>hafta</c> · <c>ay</c> · <c>yil</c>.
/// Örnek: <c>$bugun</c>, <c>$bugun-6ay</c>, <c>$ayBasi</c>, <c>$yilBasi-1yil</c>, <c>$bugun-30gun</c>.</para>
/// <para>Tanınmayan metin OLDUĞU GİBİ bırakılır — "$100" gibi normal değerler bozulmaz.</para>
/// Hafta başı PAZARTESİ kabul edilir (TR iş takvimi).</summary>
public static class DinamikTarih
{
    private static readonly Regex Kalip = new(
        @"^\$(?<taban>bugun|haftaBasi|ayBasi|yilBasi)(?:(?<isaret>[+-])(?<sayi>\d+)(?<birim>gun|hafta|ay|yil))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>İfadeyi çözer. Tanınmazsa false döner (çağıran değeri olduğu gibi bırakır).</summary>
    public static bool TryCoz(string? ifade, DateTime bugun, out DateTime sonuc)
    {
        sonuc = default;
        if (string.IsNullOrEmpty(ifade) || ifade[0] != '$') return false;
        var e = Kalip.Match(ifade);
        if (!e.Success) return false;

        var gun = bugun.Date;
        var taban = e.Groups["taban"].Value.ToLowerInvariant() switch
        {
            "bugun" => gun,
            // Pazartesi = haftanın ilk günü (Sunday=0 olduğundan pazar 6 gün geriye gider).
            "haftabasi" => gun.AddDays(-(((int)gun.DayOfWeek + 6) % 7)),
            "aybasi" => new DateTime(gun.Year, gun.Month, 1),
            "yilbasi" => new DateTime(gun.Year, 1, 1),
            _ => gun,
        };

        if (!e.Groups["sayi"].Success)
        {
            sonuc = taban;
            return true;
        }

        var miktar = int.Parse(e.Groups["sayi"].Value, CultureInfo.InvariantCulture);
        if (e.Groups["isaret"].Value == "-") miktar = -miktar;
        sonuc = e.Groups["birim"].Value.ToLowerInvariant() switch
        {
            "gun" => taban.AddDays(miktar),
            "hafta" => taban.AddDays(miktar * 7),
            "ay" => taban.AddMonths(miktar),
            "yil" => taban.AddYears(miktar),
            _ => taban,
        };
        return true;
    }

    /// <summary>Değer dinamik tarih ifadesiyse <c>yyyy-MM-dd</c> metnine çevirir; değilse aynen
    /// döndürür (kolon tipine çevirme adımı ConvertType'ta yapılır).</summary>
    public static object? CozVarsa(object? deger, DateTime? bugun = null) =>
        deger is string s && TryCoz(s, bugun ?? DateTime.Today, out var t)
            ? t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : deger;
}
