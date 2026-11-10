using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Kds.Api.Kurulum;

/// <summary>S16/A1 — kurulum yapılandırmasını KAYNAK SIRASINA göre okur, sihirbaz (A2) yazınca
/// şifreli dosyaya kaydeder:
/// <list type="number">
/// <item>Ortam değişkenleri (<c>KDS_METADATA_BAGLANTI</c>…) — container/bulut: dosya kullanılmaz.</item>
/// <item>Şifreli <c>kurulum.json</c> — klasik sunucu kurulumu (sihirbazın yazdığı).</item>
/// <item><c>appsettings</c> <c>ConnectionStrings:*</c> — YALNIZ geriye uyumluluk (mevcut dev/Compose
/// kurulumları kırılmasın); kullanıldığında uyarı loglanır.</item>
/// <item>Hiçbiri → kurulum modu (boş ayar döner).</item>
/// </list>
/// Dosya Data Protection ile şifrelenir; anahtar halkası <see cref="VeriDizini.AnahtarDizini"/>
/// altında ve uygulama geneli halkadan AYRIDIR (bağlantı parolalarının halkasına dokunulmaz).
/// try/catch burada meşru — IO adaptörü sınırı (AGENTS §4.6).</summary>
public sealed class KurulumDeposu
{
    private const string Amac = "KDS.Kurulum.v1";
    private static readonly JsonSerializerOptions JsonSecenek = new() { WriteIndented = true };

    private readonly IDataProtector koruyucu;

    public KurulumDeposu()
    {
        // Uygulama DI'ından BAĞIMSIZ halka: kurulum dosyası, DB'den önce (ve DI kurulmadan) okunur.
        koruyucu = DataProtectionProvider
            .Create(new DirectoryInfo(VeriDizini.AnahtarDizini()), b => b.SetApplicationName("LST-KDS-Kurulum"))
            .CreateProtector(Amac);
    }

    /// <summary>Kaynak sırasını uygular. <paramref name="config"/> yalnız 3. sırada (geçiş) kullanılır.</summary>
    public (KurulumAyarlari Ayarlar, string Kaynak) Oku(IConfiguration config)
    {
        var ortam = OrtamdanOku();
        if (ortam is not null) return (ortam, "ortam değişkenleri");

        var dosya = DosyadanOku();
        if (dosya is not null) return (dosya, "kurulum dosyası");

        var eski = ConfigdenOku(config);
        if (eski is not null) return (eski, "appsettings (eski yapılandırma)");

        return (new KurulumAyarlari(), "yok (kurulum modu)");
    }

    /// <summary>Sihirbazın (A2) çağırdığı yazım: şifreli dosyaya kaydeder.</summary>
    public void Yaz(KurulumAyarlari ayarlar)
    {
        ayarlar.Surum = KurulumAyarlari.GecerliSurum;
        ayarlar.KurulumTarihi ??= DateTimeOffset.Now;
        var json = JsonSerializer.Serialize(ayarlar, JsonSecenek);
        File.WriteAllText(VeriDizini.KurulumDosyasi(), koruyucu.Protect(json));
    }

    private static KurulumAyarlari? OrtamdanOku()
    {
        var metadata = Environment.GetEnvironmentVariable("KDS_METADATA_BAGLANTI");
        if (string.IsNullOrWhiteSpace(metadata)) return null;
        return new KurulumAyarlari
        {
            MetadataBaglantisi = metadata,
            DepoBaglantisi = Environment.GetEnvironmentVariable("KDS_DEPO_BAGLANTI") ?? string.Empty,
            LogBaglantisi = Environment.GetEnvironmentVariable("KDS_LOG_BAGLANTI") ?? string.Empty,
            KimlikYetkili = Environment.GetEnvironmentVariable("KDS_KIMLIK_YETKILI") ?? string.Empty,
            KimlikIstemci = Environment.GetEnvironmentVariable("KDS_KIMLIK_ISTEMCI") ?? string.Empty,
            KimlikMetadataAdresi = Environment.GetEnvironmentVariable("KDS_KIMLIK_METADATA"),
            KimlikHttpsZorunlu = Environment.GetEnvironmentVariable("KDS_KIMLIK_HTTPS") is not "false",
            KimlikYonetimIstemci = Environment.GetEnvironmentVariable("KDS_KIMLIK_YONETIM_ISTEMCI") ?? string.Empty,
            KimlikYonetimSir = Environment.GetEnvironmentVariable("KDS_KIMLIK_YONETIM_SIR") ?? string.Empty,
            OnyuzAdres = Environment.GetEnvironmentVariable("KDS_ONYUZ_ADRES") ?? string.Empty,
        };
    }

    private KurulumAyarlari? DosyadanOku()
    {
        var yol = VeriDizini.KurulumDosyasi();
        if (!File.Exists(yol)) return null;
        try
        {
            var json = koruyucu.Unprotect(File.ReadAllText(yol));
            return JsonSerializer.Deserialize<KurulumAyarlari>(json);
        }
        catch (Exception hata)
        {
            // Anahtar halkası kaybı/bozuk dosya: sessizce kurulum moduna düşmek yerine AÇIKÇA söyle
            // (yanlışlıkla yeni kuruluma başlanıp mevcut veriye bağlanılmaması için).
            throw new InvalidOperationException(
                $"Kurulum dosyası okunamadı ({yol}). Şifreleme anahtarları ({VeriDizini.AnahtarDizini()}) " +
                "silinmiş/değişmiş olabilir; yedekten geri alın ya da dosyayı silip kurulumu tekrarlayın.", hata);
        }
    }

    private static KurulumAyarlari? ConfigdenOku(IConfiguration config)
    {
        var metadata = config.GetConnectionString("KdsMetadata");
        if (string.IsNullOrWhiteSpace(metadata)) return null;
        var kimlik = config.GetSection("Kimlik");
        return new KurulumAyarlari
        {
            MetadataBaglantisi = metadata,
            DepoBaglantisi = config.GetConnectionString("KdsDepo") ?? string.Empty,
            LogBaglantisi = config.GetConnectionString("KdsLog") ?? string.Empty,
            KimlikYetkili = kimlik["Yetkili"] ?? string.Empty,
            KimlikIstemci = kimlik["Istemci"] ?? string.Empty,
            KimlikMetadataAdresi = kimlik["MetadataAdresi"],
            KimlikIssuerDeseni = kimlik["IssuerDeseni"],
            KimlikHttpsZorunlu = kimlik.GetValue("HttpsZorunlu", true),
            KimlikYonetimIstemci = kimlik["YonetimIstemci"] ?? string.Empty,
            KimlikYonetimSir = kimlik["YonetimSir"] ?? string.Empty,
            OnyuzAdres = config["Onyuz:Adres"] ?? string.Empty,
        };
    }
}
