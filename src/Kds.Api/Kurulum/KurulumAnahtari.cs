using System.Security.Cryptography;

namespace Kds.Api.Kurulum;

/// <summary>S16/A2 — kurulum modunun güvenlik kapısı. Yapılandırılmamış bir uygulama açık portta
/// durur; kurulum uçlarını herkese açık bırakmak "ilk gelen veritabanını belirler" demektir.
/// Bu yüzden uygulama açılışta tek kullanımlık bir ANAHTAR üretir, loga ve
/// <see cref="VeriDizini.KurulumAnahtariDosyasi"/>'na yazar: kurulumu yapabilen = sunucuya
/// erişebilen. Kurulum bitince anahtar dosyası silinir ve uçlar kapanır (410).
/// <para>Kaba kuvvete karşı: karşılaştırma sabit zamanlı, 5 başarısız denemeden sonra kurulum
/// kilitlenir (uygulamayı yeniden başlatmak gerekir).</para></summary>
public sealed class KurulumAnahtari
{
    private const int EnCokDeneme = 5;

    private readonly byte[]? anahtar;
    private int basarisiz;

    public KurulumAnahtari(KurulumDurumu durum, ILogger<KurulumAnahtari> gunluk)
    {
        if (durum.Yapilandirildi)
        {
            Temizle();
            return;
        }

        var metin = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        anahtar = System.Text.Encoding.UTF8.GetBytes(metin);
        var dosya = VeriDizini.KurulumAnahtariDosyasi();
        File.WriteAllText(dosya, metin);
        gunluk.LogWarning(
            "KURULUM ANAHTARI: {Anahtar} (ayrıca {Dosya}). Kurulum sihirbazı bu anahtarı ister; " +
            "kurulum tamamlanınca dosya silinir.", metin, dosya);
    }

    public bool Kilitli { get; private set; }

    /// <summary>Sabit zamanlı doğrulama + deneme sayacı.</summary>
    public bool Dogrula(string? gelen)
    {
        if (Kilitli || anahtar is null || string.IsNullOrWhiteSpace(gelen)) return false;
        var gelenBayt = System.Text.Encoding.UTF8.GetBytes(gelen);
        if (CryptographicOperations.FixedTimeEquals(anahtar, gelenBayt)) return true;

        if (Interlocked.Increment(ref basarisiz) >= EnCokDeneme) Kilitli = true;
        return false;
    }

    /// <summary>Kurulum tamamlandı: anahtar dosyasını sil (artık kullanılamaz).</summary>
    public static void Temizle()
    {
        var dosya = VeriDizini.KurulumAnahtariDosyasi();
        if (File.Exists(dosya)) File.Delete(dosya);
    }
}
