using Kds.Business.Ingest;
using Kds.Contracts.Ingest;
using Xunit;

namespace Kds.Tests;

/// <summary>S12 Faz 2 cron zamanlama — saf vade hesabı (Cronos), UTC ile deterministik
/// (DST/yerel saatten bağımsız). Ayrıca kaydet doğrulaması cron ifadesini denetler.</summary>
public sealed class CronZamanlamaTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static DateTimeOffset An(string iso) => DateTimeOffset.Parse(iso, null,
        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);

    [Theory]
    [InlineData("0 3 * * *", true)]      // her gün 03:00
    [InlineData("0 * * * *", true)]      // saat başı
    [InlineData("0 0 3 * * *", true)]    // 6 alan (saniyeli)
    [InlineData("*/15 * * * *", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("saçma", false)]
    [InlineData("99 * * * *", false)]    // dakika aralık dışı
    public void gecerli_cron_ayirt_edilir(string ifade, bool beklenen) =>
        Assert.Equal(beklenen, CronZamanlama.Gecerli(ifade));

    [Fact]
    public void hic_calismamis_gunluk_is_gecmis_slot_varsa_vadeli()
    {
        // Dün oluşturulmuş, hiç çalışmamış, günlük 03:00 → şu an 04:00 → catch-up: vadeli.
        var vadeli = CronZamanlama.Zamani("0 3 * * *",
            sonSenkron: null, olusturma: An("2026-01-01T00:00:00Z"),
            simdi: An("2026-01-01T04:00:00Z"), Utc);
        Assert.True(vadeli);
    }

    [Fact]
    public void az_once_calisan_is_sonraki_slota_kadar_vadesiz()
    {
        // 03:00'te çalıştı; şimdi 04:00. Sonraki slot yarın 03:00 → vadesiz.
        var vadeli = CronZamanlama.Zamani("0 3 * * *",
            sonSenkron: An("2026-01-01T03:00:00Z"), olusturma: An("2026-01-01T00:00:00Z"),
            simdi: An("2026-01-01T04:00:00Z"), Utc);
        Assert.False(vadeli);
    }

    [Fact]
    public void saatlik_is_slot_gecince_vadeli_gecmeden_vadesiz()
    {
        // Saat başı; en son 03:00'te çalıştı.
        Assert.False(CronZamanlama.Zamani("0 * * * *",
            An("2026-01-01T03:00:00Z"), An("2026-01-01T00:00:00Z"), An("2026-01-01T03:30:00Z"), Utc));
        Assert.True(CronZamanlama.Zamani("0 * * * *",
            An("2026-01-01T03:00:00Z"), An("2026-01-01T00:00:00Z"), An("2026-01-01T04:00:00Z"), Utc));
    }

    [Fact]
    public void zamanlama_bos_ise_gecerli_ama_asla_vadeli_degil()
    {
        Assert.False(CronZamanlama.Gecerli(null));   // boş = geçersiz cron (ama kayıtta opsiyonel)
        Assert.False(CronZamanlama.Zamani(null,
            null, An("2026-01-01T00:00:00Z"), An("2030-01-01T00:00:00Z"), Utc));
    }

    [Theory]
    [InlineData(null, true)]              // zamanlama yok → yalnız elle, geçerli
    [InlineData("0 3 * * *", true)]
    [InlineData("bozuk cron", false)]
    public void kaydet_dogrulamasi_cron_denetler(string? zamanlama, bool gecerliOlmali)
    {
        var istek = new SaveIngestJobRequest(null, "İş", Guid.NewGuid(),
            [new IngestTableDto("dbo", "T", null)], zamanlama, true);
        var sonuc = new SaveIngestJobValidation().Validate(istek);
        Assert.Equal(gecerliOlmali, sonuc.IsValid);
    }
}
