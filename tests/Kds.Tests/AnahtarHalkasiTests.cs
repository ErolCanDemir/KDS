using Kds.Api.Kurulum;
using Xunit;

namespace Kds.Tests;

/// <summary>Uygulama geneli Data Protection halkasının KALICI dizine taşınması. Kayıtlı bağlantı
/// parolaları bu halkayla şifrelidir: halka container'la birlikte uçarsa parolalar çözülemez —
/// bu yüzden veri dizinine alınır ve eski konumdaki anahtarlar bir kereye mahsus kopyalanır.</summary>
[Collection(VeriDiziniKoleksiyonu.Ad)]   // S40: ortam değişkeni yarışı — gerekçe koleksiyon tanımında
public sealed class AnahtarHalkasiTests : IDisposable
{
    private readonly string kok = Path.Combine(Path.GetTempPath(), "kds-anahtar-" + Guid.NewGuid().ToString("N"));

    private string Dizin(string ad)
    {
        var yol = Path.Combine(kok, ad);
        Directory.CreateDirectory(yol);
        return yol;
    }

    [Fact]
    public void eski_konumdaki_anahtarlar_yeni_dizine_kopyalanir()
    {
        var eski = Dizin("eski");
        var yeni = Path.Combine(kok, "yeni");   // henüz yok — taşıma yaratmalı
        File.WriteAllText(Path.Combine(eski, "key-1.xml"), "<key/>");
        File.WriteAllText(Path.Combine(eski, "key-2.xml"), "<key/>");

        var sayi = AnahtarHalkasi.EskiHalkayiTasi(yeni, eski);

        Assert.Equal(2, sayi);
        Assert.Equal(2, Directory.GetFiles(yeni, "*.xml").Length);
        // Kopyalanır, TAŞINMAZ: geri dönüş mümkün kalsın.
        Assert.Equal(2, Directory.GetFiles(eski, "*.xml").Length);
    }

    [Fact]
    public void yeni_dizinde_anahtar_varsa_dokunulmaz()
    {
        // Her açılışta çalışır: ikinci kez çalışması mevcut halkayı EZMEMELİ.
        var eski = Dizin("eski");
        var yeni = Dizin("yeni");
        File.WriteAllText(Path.Combine(eski, "key-1.xml"), "<eski/>");
        File.WriteAllText(Path.Combine(yeni, "key-1.xml"), "<yeni/>");

        var sayi = AnahtarHalkasi.EskiHalkayiTasi(yeni, eski);

        Assert.Equal(0, sayi);
        Assert.Equal("<yeni/>", File.ReadAllText(Path.Combine(yeni, "key-1.xml")));
    }

    [Fact]
    public void eski_konum_yoksa_sessizce_gecilir()
    {
        var yeni = Path.Combine(kok, "yeni2");

        var sayi = AnahtarHalkasi.EskiHalkayiTasi(yeni, Path.Combine(kok, "olmayan"));

        Assert.Equal(0, sayi);
        Assert.True(Directory.Exists(yeni)); // dizin yine de hazırlanır
    }

    [Fact]
    public void uygulama_anahtar_dizini_veri_dizininin_altindadir_ve_kurulum_halkasindan_ayridir()
    {
        var veri = Dizin("veri");
        var eskiOrtam = Environment.GetEnvironmentVariable(VeriDizini.OrtamDegiskeni);
        try
        {
            Environment.SetEnvironmentVariable(VeriDizini.OrtamDegiskeni, veri);

            var uygulama = VeriDizini.UygulamaAnahtarDizini();
            var kurulum = VeriDizini.AnahtarDizini();

            Assert.StartsWith(veri, uygulama, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(uygulama));
            Assert.NotEqual(kurulum, uygulama); // iki halka bilerek ayrı
        }
        finally { Environment.SetEnvironmentVariable(VeriDizini.OrtamDegiskeni, eskiOrtam); }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(kok)) Directory.Delete(kok, recursive: true); } catch { /* geçici dizin */ }
    }
}
