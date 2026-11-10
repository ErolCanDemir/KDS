using Kds.Data;

namespace Kds.Api.Identity;

/// <summary>Tenant bağlamını geçerli HTTP isteğinin token'ından (<c>tenant_id</c> claim)
/// okur. Kds.Data yalnız <see cref="ITenantContext"/> arayüzünü bilir; ASP.NET bağımlılığı
/// burada kalır. Arka plan işi (zamanlayıcı) <see cref="AmbientTenant"/> ile açıkça tenant
/// bağladıysa o öncelikli — HTTP dışı akışta (HttpContext yok) tenant böyle taşınır.</summary>
public sealed class HttpTenantContext(IHttpContextAccessor erisim) : ITenantContext
{
    public Guid? CurrentTenantId =>
        AmbientTenant.Current
        ?? (Guid.TryParse(erisim.HttpContext?.User.FindFirst("tenant_id")?.Value, out var kimlik)
            ? kimlik
            : null);
}
