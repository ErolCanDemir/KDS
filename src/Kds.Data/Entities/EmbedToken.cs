namespace Kds.Data.Entities;

/// <summary>S42 — GÖMME (embed) anahtarı: dış bir siteye gömülen rapor/panonun "kim adına, nereden,
/// ne zamana kadar" çalışacağını tanımlar.
///
/// <para><b>Güvenlik kararı (kullanıcı onaylı, 26 Ağu 2026):</b> gömülü koşum SABİT bir servis
/// hesabıyla DEĞİL, token'ın taşıdığı KİMLİKLE yapılır ve satır güvenliği (RLS) aynen uygulanır.
/// Sabit hesap seçilseydi tek bir token sızıntısı tüm kiracının verisini açardı; bu modelde
/// sızan token yalnız o kimliğin göreceği satırları gösterir.</para>
///
/// <para><b>Neden DB'de duruyor:</b> token'ın kendisi imzalı/şifreli bir dizedir ve tek başına
/// doğrulanabilirdi — ama o zaman İPTAL edilemezdi. Sızıntı senaryosunda anahtarı anında kapatmak
/// gerektiği için doğrulama daima bu satıra bakar.</para></summary>
public class EmbedToken : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Yönetim listesinde tanımak için insan adı ("Bayi portalı — ciro kartı").</summary>
    public string Ad { get; set; } = string.Empty;

    /// <summary>Gömülen içerik: rapor VEYA pano (biri dolu). Yumuşak referans — hedef silinirse
    /// token doğrulaması "içerik bulunamadı" der, kayıt kendiliğinden çöpe dönmez.</summary>
    public Guid? RaporId { get; set; }
    public Guid? PanoId { get; set; }

    /// <summary>Koşumun yapılacağı kimliğin ANLIK GÖRÜNTÜSÜ (<c>CalistiranKullanici</c> JSON'u):
    /// roller + kullanıcı adı + RLS iddiaları. Abonelik/uyarı işlerindeki desenin aynısı — dış
    /// servise (Keycloak) bağımlı olmadan koşabilmek için token kurulurken dondurulur.</summary>
    public string KimlikJson { get; set; } = string.Empty;

    /// <summary>Gömmeye izin verilen alan adları (virgülle ayrık, ör. "portal.firma.com").
    /// Boşsa hiçbir yere gömülemez — güvenli varsayılan "kapalı"dır, "her yer" değil.</summary>
    public string IzinliAlanlar { get; set; } = string.Empty;

    public DateTimeOffset GecerlilikBitis { get; set; }

    /// <summary>Dolu ise anahtar İPTAL edilmiştir; süresi dolmamış olsa bile çalışmaz.</summary>
    public DateTimeOffset? IptalTarihi { get; set; }

    public Guid OlusturanId { get; set; }

    /// <summary>Son başarılı kullanım — "bu anahtar hâlâ kullanılıyor mu?" sorusunun cevabı
    /// (Kullanım ekranındaki ölü rapor mantığının gömme karşılığı).</summary>
    public DateTimeOffset? SonKullanim { get; set; }
}
