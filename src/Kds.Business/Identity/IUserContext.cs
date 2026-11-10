namespace Kds.Business.Identity;

/// <summary>Mevcut isteğin kullanıcı bağlamı (rapor sahibi, favori, görünürlük kararları için).
/// Gerçekleştirme HTTP token'ından okur (Api katmanı); Business, ASP.NET bağımlılığı taşımaz.
/// Local <c>User.Id</c>'ye ihtiyaç duyan handler, <see cref="KeycloakId"/> ile tabloya bakar
/// (JIT provisioning middleware kaydı garanti eder).</summary>
public interface IUserContext
{
    /// <summary>Token'daki <c>sub</c> — Keycloak kullanıcı UUID'si; anonim istekte null.</summary>
    Guid? KeycloakId { get; }

    /// <summary>admin rolü var mı (Özel raporları da görür/yönetir).</summary>
    bool IsAdmin { get; }

    /// <summary>admin ya da gelistirici — tasarım/kaydetme yetkisi.</summary>
    bool CanDesign { get; }

    // RLS üyeleri: varsayılan uygulamalarla gelirler ki kimlikle ilgilenmeyen sahteler/testler
    // (ve ileride eklenecek bağlamlar) bunları yazmak zorunda kalmasın. GÜVENLİ varsayılan:
    // rol yok + iddia yok → kural eşleşmesi olmaz; RLS'e tabi bir bağlamda gerçek uygulama
    // (HttpUserContext) bunları doldurur.

    /// <summary>Token'daki roller. RLS kuralı "hangi role uygulanır" diye buna bakar.</summary>
    IReadOnlyCollection<string> Roller => [];

    /// <summary>Kullanıcı adı (<c>preferred_username</c>) — "yalnız kendi kayıtları" kuralı için.</summary>
    string? KullaniciAdi => null;

    /// <summary>E-posta iddiası — "yalnız kendi kayıtları" kuralının diğer yaygın biçimi.</summary>
    string? EPosta => null;

    /// <summary>Token'daki DÜZ iddialar (ad → değer; çok değerli iddia virgülle birleşir). RLS
    /// kuralı "servis_kodu" gibi bir niteliğin değerini buradan okur — böylece kullanıcı
    /// nitelikleri için ayrı tablo tutmak yerine kimlik sağlayıcı tek doğruluk kaynağı olur.</summary>
    IReadOnlyDictionary<string, string> Iddialar => new Dictionary<string, string>();
}
