using Kds.Execution;
using Xunit;

namespace Kds.Tests;

/// <summary>Y5 — dinamik (kayan) tarih ifadeleri: <c>$bugun-6ay</c>, <c>$ayBasi</c>, <c>$yilBasi-1yil</c>…
/// Sabit bir "bugün" verilerek çözüm deterministik sınanır.</summary>
public sealed class DinamikTarihTests
{
    // Çarşamba: hafta başı (Pazartesi) hesabının doğruluğu görünsün.
    private static readonly DateTime Bugun = new(2026, 8, 19);

    [Theory]
    [InlineData("$bugun", "2026-08-19")]
    [InlineData("$bugun-1gun", "2026-08-18")]
    [InlineData("$bugun+3gun", "2026-08-22")]
    [InlineData("$bugun-30gun", "2026-07-20")]
    [InlineData("$bugun-2hafta", "2026-08-05")]
    [InlineData("$bugun-6ay", "2026-02-19")]
    [InlineData("$bugun-2yil", "2024-08-19")]
    [InlineData("$haftaBasi", "2026-08-17")]   // Pazartesi
    [InlineData("$ayBasi", "2026-08-01")]
    [InlineData("$ayBasi-1ay", "2026-07-01")]
    [InlineData("$yilBasi", "2026-01-01")]
    [InlineData("$yilBasi-1yil", "2025-01-01")]
    public void ifadeler_cozulur(string ifade, string beklenen)
    {
        Assert.True(DinamikTarih.TryCoz(ifade, Bugun, out var sonuc));
        Assert.Equal(beklenen, sonuc.ToString("yyyy-MM-dd"));
    }

    [Theory]
    [InlineData("$100")]          // para gibi normal bir değer
    [InlineData("$bugun-6")]      // birimsiz
    [InlineData("$bugun-6hafta2")]
    [InlineData("$yarin")]        // tanımsız taban
    [InlineData("2026-08-19")]    // sabit tarih
    [InlineData("")]
    [InlineData(null)]
    public void taninmayan_deger_oldugu_gibi_kalir(string? ifade)
    {
        Assert.False(DinamikTarih.TryCoz(ifade, Bugun, out _));
        Assert.Equal(ifade, DinamikTarih.CozVarsa(ifade, Bugun));
    }

    [Fact]
    public void pazar_gunu_hafta_basi_bir_onceki_pazartesidir()
    {
        Assert.True(DinamikTarih.TryCoz("$haftaBasi", new DateTime(2026, 8, 23), out var s)); // Pazar
        Assert.Equal("2026-08-17", s.ToString("yyyy-MM-dd"));
    }
}
