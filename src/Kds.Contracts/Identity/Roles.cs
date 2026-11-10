namespace Kds.Contracts.Identity;

/// <summary>Kaba roller Keycloak realm rolleridir (00-analiz §7); rapor/klasör-bazlı
/// ince yetki metadata DB'de tutulur. Adlar Keycloak'taki realm rol adlarıyla birebirdir.</summary>
public static class Roles
{
    /// <summary>Tenant yöneticisi (kullanıcı/rol, bağlantı, tüm ekranlar).</summary>
    public const string Admin = "admin";

    /// <summary>Veri haritası + sorgu/rapor + dashboard tasarlar.</summary>
    public const string Gelistirici = "gelistirici";

    /// <summary>Yalnız yetkili rapor/dashboard görüntüler.</summary>
    public const string Musteri = "musteri";
}
