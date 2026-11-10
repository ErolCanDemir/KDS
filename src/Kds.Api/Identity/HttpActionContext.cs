using Kds.Contracts.Logging;

namespace Kds.Api.Identity;

/// <summary>İstek-başına aksiyon bağlamı (olay günlüğü korelasyonu): <see cref="ActionId"/> ilk
/// erişimde üretilip <c>HttpContext.Items</c>'ta önbelleklenir → istek boyunca sabittir; aynı istekte
/// üretilen DataChange/Exception olayları bu Id'yi taşır. Kullanıcı/tenant/ip token claim'lerinden.</summary>
public sealed class HttpActionContext(IHttpContextAccessor erisim) : IActionContext
{
    private const string Anahtar = "kds-action-id";

    public Guid ActionId
    {
        get
        {
            var ctx = erisim.HttpContext;
            if (ctx is null) return Guid.Empty;
            if (ctx.Items.TryGetValue(Anahtar, out var v) && v is Guid mevcut) return mevcut;
            var yeni = Guid.NewGuid();
            ctx.Items[Anahtar] = yeni;
            return yeni;
        }
    }

    public Guid? TenantId =>
        Guid.TryParse(erisim.HttpContext?.User.FindFirst("tenant_id")?.Value, out var t) ? t : null;

    public Guid? UserId =>
        Guid.TryParse(erisim.HttpContext?.User.FindFirst("sub")?.Value, out var u) ? u : null;

    public string? UserName => erisim.HttpContext?.User.Identity?.Name;

    public string? Ip => erisim.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
