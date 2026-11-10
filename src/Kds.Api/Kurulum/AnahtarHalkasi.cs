namespace Kds.Api.Kurulum;

/// <summary>Uygulama geneli Data Protection halkasının KALICI konuma taşınması.
/// <para>Sorun: halka varsayılanda çerçevenin seçtiği yerdedir (Windows'ta kullanıcının
/// <c>%LOCALAPPDATA%\ASP.NET\DataProtection-Keys</c>'i, Linux/container'da
/// <c>~/.aspnet/DataProtection-Keys</c>). Container yeniden yaratıldığında burası uçar ve
/// <b>kayıtlı bağlantı parolaları çözülemez</b> olur. Çözüm: halkayı veri dizinine
/// (<see cref="VeriDizini.UygulamaAnahtarDizini"/>) almak.</para>
/// <para>Geçiş kaybı olmasın diye: yeni dizin BOŞsa ve eski konumda anahtar varsa, anahtarlar
/// KOPYALANIR (taşınmaz — geri dönüş mümkün kalsın). Uygulama adı (discriminator) bilerek
/// DEĞİŞTİRİLMEZ; değiştirilseydi aynı anahtarlarla bile eski şifreli değerler çözülemezdi.</para></summary>
public static class AnahtarHalkasi
{
    /// <summary>Çerçevenin varsayılan halka dizini (yoksa null).</summary>
    public static string? EskiVarsayilanDizin()
    {
        var kok = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(kok)) return null;
        var yol = OperatingSystem.IsWindows()
            ? Path.Combine(kok, "ASP.NET", "DataProtection-Keys")
            : Path.Combine(kok, ".aspnet", "DataProtection-Keys");
        return Directory.Exists(yol) ? yol : null;
    }

    /// <summary>Eski konumdaki anahtarları yeni (kalıcı) dizine kopyalar. Yeni dizinde ZATEN
    /// anahtar varsa hiçbir şey yapmaz — böylece her açılışta yeniden çalışması zararsızdır.
    /// Dönen değer: kopyalanan dosya sayısı.</summary>
    public static int EskiHalkayiTasi(string yeniDizin, string? eskiDizin = null)
    {
        Directory.CreateDirectory(yeniDizin);
        if (Directory.EnumerateFiles(yeniDizin, "*.xml").Any()) return 0;

        var kaynak = eskiDizin ?? EskiVarsayilanDizin();
        if (kaynak is null || !Directory.Exists(kaynak)) return 0;

        var sayac = 0;
        foreach (var dosya in Directory.EnumerateFiles(kaynak, "*.xml"))
        {
            File.Copy(dosya, Path.Combine(yeniDizin, Path.GetFileName(dosya)), overwrite: false);
            sayac++;
        }
        return sayac;
    }
}
