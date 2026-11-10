namespace Kds.Data.Entities;

/// <summary>Uygulamanın kendi kullanıcı kaydı. Kimlik doğrulama Keycloak'tadır; bu tablo
/// kendi verimizde kullanıcıya referans (rapor sahibi, audit, ince yetki) verebilmek için
/// tutulur. <see cref="KeycloakId"/> = token'daki <c>sub</c>. İlk girişte otomatik oluşturulur
/// (JIT provisioning); parola/kimlik asla kopyalanmaz.</summary>
public class User : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>Keycloak kullanıcı UUID'si (token'daki <c>sub</c>). Benzersiz.</summary>
    public Guid KeycloakId { get; set; }

    public string KullaniciAdi { get; set; } = string.Empty;
    public string? EPosta { get; set; }
    public string? AdSoyad { get; set; }
    public DateTimeOffset? SonGirisTarihi { get; set; }

    /// <summary>S43 — kullanıcının ana sayfasında açılacak pano; null = seçim yok (ana sayfa
    /// karşılama görünümünü gösterir). Panoya FK YOK: pano silinirse ya da paylaşımı kapanırsa
    /// kayıt bozulmaz, okuma anında görünürlük denetlenip sessizce karşılamaya düşülür.
    /// Tek pano tutulduğu için ayrı tabloya (favoriler deseni) gerek yok — tekillik kolonun kendisi.</summary>
    public Guid? AnaSayfaPanoId { get; set; }
}
