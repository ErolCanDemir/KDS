using Kds.Common.Cqrs;

namespace Kds.Contracts.Identity;

/// <summary>S27 Roles &amp; Yetkiler — rol yönetimi sözleşmeleri. Roller Keycloak realm rolleridir
/// (kimlik TEK doğruluk kaynağı): burada oluşturulan rol girişte token'a yazılır; RLS kuralları
/// (<c>RowRule.Roller</c>) ve rapor/klasör görünürlüğü aynı ada bakar.</summary>
public sealed record RolOzeti(
    string Ad,
    string? Aciklama,
    /// <summary>İlk 500 üye sayılır (ekran rozeti için yeterli; tam sayım pahalı).</summary>
    int UyeSayisi,
    /// <summary>admin/gelistirici/musteri — ürünle gelen roller: silinemez.</summary>
    bool Sistem);

public sealed record RolUyesi(Guid KeycloakId, string KullaniciAdi, string? EPosta, string? AdSoyad);

public sealed record ListRolesRequest : IQuery<IReadOnlyList<RolOzeti>>;

public sealed record CreateRoleRequest(string Ad, string? Aciklama) : ICommand<RolOzeti>;

/// <summary>Sistem rolleri ve üyesi olan roller silinemez (üyeler önce çıkarılır — sessizce
/// yetki düşürmek yerine bilinçli işlem istenir).</summary>
public sealed record DeleteRoleRequest(string Ad) : ICommand<bool>;

public sealed record ListRoleMembersRequest(string Ad) : IQuery<IReadOnlyList<RolUyesi>>;

public sealed record AddRoleMemberRequest(string RolAdi, Guid KeycloakId) : ICommand<bool>;

public sealed record RemoveRoleMemberRequest(string RolAdi, Guid KeycloakId) : ICommand<bool>;

/// <summary>Üye eklerken aday arama — yalnız İSTEĞİ YAPANIN kiracısındaki kullanıcılar döner.</summary>
public sealed record SearchUsersRequest(string? Arama) : IQuery<IReadOnlyList<RolUyesi>>;

/// <summary>Rol servisi sağlık denetimi — ekran açılışta "yapılandırılmadı/ulaşılamadı" kartını
/// bununla doldurur (Soru Sor'daki dayanıklılık deseniyle aynı: özellik yoksa ürün çökmez).</summary>
public sealed record RolYonetimDurumuRequest : IQuery<RolYonetimDurumu>;

public sealed record RolYonetimDurumu(bool Hazir, string? Sebep);
