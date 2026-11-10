using Cronos;

namespace Kds.Business.Ingest;

/// <summary>İçe alım zamanlaması için saf cron yardımcısı (Cronos). 5 alan (dk saat gün ay haftagünü)
/// ya da 6 alan (saniyeli) kabul eder. Durum tutmaz — hem doğrulama (kaydet) hem zamanlayıcı (vade
/// kontrolü) bunu paylaşır; DST'yi verilen zaman diliminde doğru işler.</summary>
public static class CronZamanlama
{
    /// <summary>İfade ayrıştırılabiliyor mu? (Doğrulamada kullanılır.)</summary>
    public static bool Gecerli(string? ifade) => Ayristir(ifade) is not null;

    /// <summary><paramref name="taban"/>'dan (o an DAHİL değil) sonraki ilk çalışma anı; ifade
    /// geçersizse ya da hiç eşleşmiyorsa null.</summary>
    public static DateTimeOffset? SonrakiCalisma(string? ifade, DateTimeOffset taban, TimeZoneInfo zon) =>
        Ayristir(ifade)?.GetNextOccurrence(taban, zon, inclusive: false);

    /// <summary>İşin ŞİMDİ çalışması gerekiyor mu? Son senkron (yoksa işin oluşturma anı) tabanından
    /// sonraki planlı an <paramref name="simdi"/>'ye eşit/erkense vade gelmiştir. Böylece iş çalışıp
    /// SonSenkronTarihi güncellenince taban ileri kayar ve bir sonraki slota kadar tekrar tetiklenmez.</summary>
    public static bool Zamani(string? ifade, DateTimeOffset? sonSenkron, DateTimeOffset olusturma,
        DateTimeOffset simdi, TimeZoneInfo zon)
    {
        var sonraki = SonrakiCalisma(ifade, sonSenkron ?? olusturma, zon);
        return sonraki is { } an && an <= simdi;
    }

    private static CronExpression? Ayristir(string? ifade)
    {
        if (string.IsNullOrWhiteSpace(ifade))
        {
            return null;
        }

        // Alan sayısı 6 ise ilk alan saniyedir; değilse standart 5 alan.
        var bicim = ifade.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 6
            ? CronFormat.IncludeSeconds
            : CronFormat.Standard;
        try
        {
            return CronExpression.Parse(ifade, bicim);
        }
        catch (CronFormatException)
        {
            return null;
        }
    }
}
