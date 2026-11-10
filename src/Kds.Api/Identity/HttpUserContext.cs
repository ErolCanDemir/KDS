using Kds.Business.Identity;
using Kds.Contracts.Identity;

namespace Kds.Api.Identity;

/// <summary>Mevcut isteğin kullanıcı bağlamı — token claim'lerinden okur
/// (<c>MapInboundClaims=false</c>: <c>sub</c> adıyla durur; roller KeycloakRoleMapping ile
/// <see cref="System.Security.Claims.ClaimTypes.Role"/>'e çevrilmiştir).</summary>
public sealed class HttpUserContext(IHttpContextAccessor erisim) : IUserContext
{
    public Guid? KeycloakId =>
        Guid.TryParse(erisim.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;

    public bool IsAdmin => erisim.HttpContext?.User.IsInRole(Roles.Admin) == true;

    public bool CanDesign =>
        IsAdmin || erisim.HttpContext?.User.IsInRole(Roles.Gelistirici) == true;

    public IReadOnlyCollection<string> Roller =>
        erisim.HttpContext?.User.FindAll(System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];

    public string? KullaniciAdi => erisim.HttpContext?.User.FindFirst("preferred_username")?.Value;

    public string? EPosta => erisim.HttpContext?.User.FindFirst("email")?.Value;

    /// <summary>Token'daki düz iddialar. Aynı ad birden çok kez gelirse (Keycloak çok değerli
    /// nitelikleri böyle yollar) değerler VİRGÜLLE birleşir — RLS çözücüsü bunu çoklu değere açar.
    /// Rol iddiaları dışarıda bırakılır: onlar <see cref="Roller"/>'da yaşar.</summary>
    public IReadOnlyDictionary<string, string> Iddialar =>
        erisim.HttpContext?.User.Claims
            .Where(c => c.Type != System.Security.Claims.ClaimTypes.Role)
            .GroupBy(c => c.Type, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(c => c.Value)), StringComparer.OrdinalIgnoreCase)
        ?? new Dictionary<string, string>();
}
