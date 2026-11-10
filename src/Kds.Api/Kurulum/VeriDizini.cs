namespace Kds.Api.Kurulum;

/// <summary>S16/A1 — uygulamanın MAKİNEYE ait verisini (kurulum dosyası, şifreleme anahtarları)
/// tuttuğu dizin. Sıra: <c>KDS_VERI_DIZINI</c> ortam değişkeni → Windows'ta
/// <c>%ProgramData%\LST\KDS</c>, diğer platformlarda <c>/var/lib/kds</c>. Yol koda gömülü bir
/// BAĞLANTI değil, platform standardı bir konumdur; container'da env ile kalıcı birime taşınır.</summary>
public static class VeriDizini
{
    public const string OrtamDegiskeni = "KDS_VERI_DIZINI";

    /// <summary>Veri dizinini çözer ve yoksa yaratır.</summary>
    public static string Yol()
    {
        var ozel = Environment.GetEnvironmentVariable(OrtamDegiskeni);
        var yol = !string.IsNullOrWhiteSpace(ozel)
            ? ozel
            : OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LST", "KDS")
                : "/var/lib/kds";
        Directory.CreateDirectory(yol);
        return yol;
    }

    /// <summary>Kurulum dosyasının tam yolu (şifreli).</summary>
    public static string KurulumDosyasi() => Path.Combine(Yol(), "kurulum.json");

    /// <summary>Kurulum dosyasını şifreleyen anahtar halkasının dizini. UYGULAMA GENELİ halkadan
    /// (<see cref="UygulamaAnahtarDizini"/>) AYRIDIR: kurulum dosyası, uygulama daha ayağa
    /// kalkmadan okunur — iki halkayı ayırmak, birinin kaybının diğerini kilitlememesini sağlar.</summary>
    public static string AnahtarDizini()
    {
        var yol = Path.Combine(Yol(), "anahtarlar");
        Directory.CreateDirectory(yol);
        return yol;
    }

    /// <summary>Uygulama geneli Data Protection halkasının dizini — <b>bağlantı parolaları</b>
    /// bununla şifrelenir. Çerçevenin varsayılan konumu (Windows'ta kullanıcının
    /// <c>%LOCALAPPDATA%</c>'sı, container'da <c>~/.aspnet</c>) container yeniden yaratıldığında
    /// UÇAR ve kayıtlı parolalar çözülemez hâle gelirdi; halka bu yüzden KALICI veri dizinine
    /// alınır (compose'da <c>kds-veri</c> birimi).</summary>
    public static string UygulamaAnahtarDizini()
    {
        var yol = Path.Combine(Yol(), "anahtarlar-uygulama");
        Directory.CreateDirectory(yol);
        return yol;
    }

    /// <summary>Kurulum anahtarı dosyası (yalnız uygulama kurulmamışken vardır).</summary>
    public static string KurulumAnahtariDosyasi() => Path.Combine(Yol(), "kurulum-anahtari.txt");
}
