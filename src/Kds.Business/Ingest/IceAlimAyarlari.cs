using Kds.Business.Settings;

namespace Kds.Business.Ingest;

/// <summary>İçe alım deposunun KURULUM düzeyi ayarı: KDS'in kendi depo SUNUCUSU bağlantı dizesi
/// (belirli DB'siz — master'a bağlanır; iş çalışınca kiracı başına depo DB'si yoksa yaratılır).
/// Kaynak: kurulum (ortam değişkeni / kurulum.json).
/// <para>S16/A3: Sistem Ayarları'nda <c>Depo.SunucuBaglantisi</c> doluysa O ÖNCELİKLİDİR —
/// çözüm tek yerde: <see cref="DepoCozucu"/>.</para></summary>
public sealed class IceAlimAyarlari
{
    public string DepoSunucuBaglantisi { get; set; } = string.Empty;

    /// <summary>Tenant'ın depo DB adı — TEK yerde tanımlı: hem içe alım yükleme (RunIngestJob) hem
    /// İçe Alım modlu rapor yönlendirmesi (RunQuery) aynı adı kullanır ki yazılan yerden okunsun.</summary>
    public static string DepoDbAdi(Guid tenantId) => "KdsDepo_" + tenantId.ToString("N");

    /// <summary>Depo DB adını AYARDAKİ desenden üretir (<c>{kiraci}</c> → kiracı kimliği).
    /// Desen boş/yer tutucusuzsa eski davranışa düşer.</summary>
    public static string DepoDbAdi(Guid tenantId, string? desen) =>
        string.IsNullOrWhiteSpace(desen) || !desen.Contains(AyarTanimlari.KiraciYerTutucu, StringComparison.Ordinal)
            ? DepoDbAdi(tenantId)
            : desen.Replace(AyarTanimlari.KiraciYerTutucu, tenantId.ToString("N"), StringComparison.Ordinal);
}

/// <summary>S16/A3 — depo sunucusu + DB adının TEK çözüm noktası. Öncelik: <b>Sistem Ayarları →
/// kurulum (env/kurulum.json)</b>. Böylece aynı ürün hem değişmez (container, env) hem de
/// ekrandan yönetilen (klasik kurulum) dağıtımda çalışır.</summary>
public sealed class DepoCozucu(IceAlimAyarlari kurulum, ISistemAyarlari ayarlar)
{
    public string SunucuDizesi()
    {
        var ayar = ayarlar.Metin(AyarTanimlari.DepoSunucuBaglantisi);
        return !string.IsNullOrWhiteSpace(ayar) ? ayar : kurulum.DepoSunucuBaglantisi;
    }

    public string DbAdi(Guid tenantId) =>
        IceAlimAyarlari.DepoDbAdi(tenantId, ayarlar.Metin(AyarTanimlari.DepoDbAdiDeseni));
}
