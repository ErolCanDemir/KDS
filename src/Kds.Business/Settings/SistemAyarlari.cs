using Kds.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kds.Business.Settings;

/// <summary>S16/A3 — ayar okuma yüzeyi. Değer sırası: <b>kiracıya özel → kurulum geneli →
/// koddaki varsayılan</b>. Her sorgu isteğinde DB'ye gitmemek için kısa süreli bellek
/// önbelleği kullanılır; yazım önbelleği düşürür (<see cref="Bosalt"/>).</summary>
public interface ISistemAyarlari
{
    string Metin(string anahtar);
    int Sayi(string anahtar);
    bool EvetHayir(string anahtar);
}

/// <summary>Ayarların DB'den okunan hâli (önbellekli). Sırlar burada ÇÖZÜLMÜŞ döner — çağıran
/// (ör. SMTP göndericisi) düz değere ihtiyaç duyar; API katmanı sırları dışarı vermez.</summary>
public sealed class SistemAyarlari(
    KdsDbContext context, IMemoryCache onbellek, ITenantContext tenant,
    Connections.IPasswordProtector? koruma = null)
    : ISistemAyarlari
{
    private const string OnbellekAnahtari = "kds.sistem.ayarlari";
    private static readonly TimeSpan Sure = TimeSpan.FromSeconds(30);

    public string Metin(string anahtar)
    {
        var tanim = AyarTanimlari.Bul(anahtar);
        var sozluk = Yukle();
        var kiraciId = tenant.CurrentTenantId;
        string? ham = null;
        if (kiraciId is Guid k && sozluk.TryGetValue((k, anahtar), out var kiraciDeger)) ham = kiraciDeger;
        else if (sozluk.TryGetValue((null, anahtar), out var genel)) ham = genel;
        if (ham is null) return tanim?.Varsayilan ?? string.Empty;

        // Sır tipindeki ayarlar diskte ŞİFRELİ durur; tüketiciye düz değer gerekir.
        return tanim?.Tip == AyarTipi.Sir ? Coz(ham) : ham;
    }

    private string Coz(string sifreli)
    {
        if (string.IsNullOrEmpty(sifreli) || koruma is null) return sifreli;
        try { return koruma.Unprotect(sifreli); }
        catch (Exception) { return string.Empty; } // anahtar değişmiş/bozuk: sırrı sızdırmadan boş dön
    }

    public int Sayi(string anahtar) =>
        int.TryParse(Metin(anahtar), out var d) ? d
            : int.TryParse(AyarTanimlari.Bul(anahtar)?.Varsayilan, out var v) ? v : 0;

    public bool EvetHayir(string anahtar) =>
        bool.TryParse(Metin(anahtar), out var d) ? d
            : bool.TryParse(AyarTanimlari.Bul(anahtar)?.Varsayilan, out var v) && v;

    private Dictionary<(Guid? Kiraci, string Anahtar), string> Yukle() =>
        onbellek.GetOrCreate(OnbellekAnahtari, giris =>
        {
            giris.AbsoluteExpirationRelativeToNow = Sure;
            return context.SistemAyarlari.AsNoTracking().ToList()
                .ToDictionary(a => (a.TenantId, a.Anahtar), a => a.Deger);
        })!;

    /// <summary>Ayar yazıldığında çağrılır — sonraki okuma DB'den tazelenir.</summary>
    public static void Bosalt(IMemoryCache onbellek) => onbellek.Remove(OnbellekAnahtari);
}
