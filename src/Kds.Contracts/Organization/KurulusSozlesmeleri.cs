using Kds.Common.Cqrs;

namespace Kds.Contracts.Organization;

/// <summary>S35 — Kuruluş Ayarları (Yönetim → Kuruluş Ayarları). Kiracının kendi kimliği ve
/// markası; yalnız Admin görür ve düzenler.
///
/// <para><b>Kapsam sınırı (bilinçli):</b> sorgu limitleri burada YOK — onlar Sistem Ayarları'nda
/// tanımlıdır ve zaten kiracı bazında ezilebilir. Ekran o değerleri yalnız ÖZET olarak gösterir;
/// aynı eşiği iki yerde düzenletmek, birinin diğerini sessizce geçersiz kılmasıyla biter.</para></summary>
public sealed record GetOrganizationRequest : IQuery<OrganizationResult>;

/// <param name="KiraciId">Keycloak token'ındaki <c>tenant_id</c> ile birebir aynı olmalıdır —
/// ekran bunu kopyalanabilir gösterir, kimlik sağlayıcı yapılandırması buna dayanır.</param>
/// <param name="Limitler">Sistem Ayarları'ndan OKUNAN yürürlükteki değerler (salt görüntü).</param>
public sealed record OrganizationResult(
    Guid KiraciId,
    string Ad,
    string Durum,
    string? Plan,
    string? MarkaRengi,
    string? LogoDataUri,
    DateTimeOffset OlusturmaTarihi,
    IReadOnlyList<LimitOzeti> Limitler);

/// <param name="Kaynak">kiraci | kurulum | varsayilan — değerin nereden geldiği (Sistem
/// Ayarları'ndaki kaynak rozetiyle aynı dil).</param>
public sealed record LimitOzeti(string Anahtar, string Etiket, string Deger, string Kaynak);

/// <summary>Kuruluş kimliğini ve markasını günceller. Boş bırakılan marka alanları temizlenmiş
/// sayılır (null) — "logoyu kaldır" ayrı bir komut gerektirmesin.</summary>
public sealed record UpdateOrganizationRequest(
    string Ad,
    string? Plan,
    string? MarkaRengi,
    string? LogoDataUri) : ICommand<OrganizationResult>;

/// <summary>Markayı uygulama kabuğuna taşıyan DAR uç. Ayrı olmasının sebebi yetki: kuruluş
/// ayarlarını yalnız Admin görür, ama logo ve vurgu rengini <b>her kullanıcı</b> görmelidir —
/// aksi hâlde marka yalnız yöneticinin ekranında görünürdü. Bu yüzden burada plan, limit,
/// kiracı kimliği gibi yönetim bilgisi YOKTUR; yalnız çizim için gerekli üç alan döner.</summary>
public sealed record GetBrandingRequest : IQuery<BrandingResult>;

public sealed record BrandingResult(string Ad, string? MarkaRengi, string? LogoDataUri);
