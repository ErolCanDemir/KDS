using Kds.Contracts.Users;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace Kds.Api.Identity;

/// <summary>JIT provisioning: kimliği doğrulanmış her API isteğinde, geçerli Keycloak
/// kullanıcısının local User kaydını sağlar (yoksa oluşturur). Aynı kullanıcı için
/// tekrar tekrar DB'ye gitmemek üzere kısa süreli önbellekle korunur.</summary>
public sealed class UserProvisioningMiddleware(RequestDelegate sonraki, IMemoryCache onbellek)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var kullanici = context.User;
        if (kullanici.Identity?.IsAuthenticated == true &&
            context.Request.Path.StartsWithSegments("/api") &&
            Guid.TryParse(kullanici.FindFirst("sub")?.Value, out var keycloakId) &&
            Guid.TryParse(kullanici.FindFirst("tenant_id")?.Value, out var tenantId) &&
            !onbellek.TryGetValue(keycloakId, out _))
        {
            var gonderici = context.RequestServices.GetRequiredService<ISender>();
            await gonderici.Send(new ProvisionUserRequest(
                keycloakId,
                kullanici.Identity.Name ?? keycloakId.ToString(),
                kullanici.FindFirst("email")?.Value,
                kullanici.FindFirst("name")?.Value,
                tenantId));
            onbellek.Set(keycloakId, true, TimeSpan.FromMinutes(5));
        }

        await sonraki(context);
    }
}
