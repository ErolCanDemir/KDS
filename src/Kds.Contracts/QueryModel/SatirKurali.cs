namespace Kds.Contracts.QueryModel;

/// <summary>Satır-düzeyi güvenlik (RLS) kuralı — "bu kullanıcı bu tablonun YALNIZ şu satırlarını
/// görsün". Kiracı yalıtımı zaten var; bu, kiracının İÇİNDE kullanıcı/rol bazlı daraltmadır
/// (ör. "bayi kullanıcısı yalnız kendi servisinin taleplerini görür").
/// <para><b>Nerede uygulanır:</b> sorgu ÜRETİMİNDE — kural, modelden üretilen SQL'in WHERE'ine
/// eklenir ve kullanıcı tarafından kaldırılamaz. Kural yalnız, sorguda O TABLO kullanılıyorsa
/// devreye girer (tablo sorguda yoksa o tablodan satır da dönmez).</para>
/// <para><b>Fail-closed:</b> kural uygulanacakken değeri çözülemiyorsa (ör. token'da beklenen
/// iddia yok) sorgu ÇALIŞTIRILMAZ. Sessizce filtresiz koşmak, veri sızdırmanın en sinsi
/// biçimidir.</para></summary>
public sealed record RowFilter(
    Guid TabloId,
    string Kolon,
    KarsilastirmaOp Op,
    IReadOnlyList<object?> Degerler,
    /// <summary>Hata/denetim mesajlarında görünen kural adı (hangi kuralın engellediği belli olsun).</summary>
    string? KuralAdi = null);

/// <summary>Kuralın karşılaştırma DEĞERİ nereden gelir?
/// <list type="bullet">
/// <item><see cref="SabitDeger"/> — kuralda yazan sabit ("Ege").</item>
/// <item><see cref="TokenIddiasi"/> — kimlik sağlayıcıdan gelen iddia (claim); değer alanına
/// iddia ADI yazılır (ör. <c>servis_kodu</c>). Kullanıcı başına ayrı bir tablo tutmak yerine
/// SSO'nun kendi kullanıcı niteliklerini kullanmak, tek doğruluk kaynağı sağlar.</item>
/// <item><see cref="KullaniciAdi"/> / <see cref="EPosta"/> — "yalnız kendi kayıtları" deseni.</item>
/// </list></summary>
public enum KuralDegerKaynagi { SabitDeger = 0, TokenIddiasi, KullaniciAdi, EPosta }
