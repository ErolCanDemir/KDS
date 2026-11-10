using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Kds.Api.Identity;

/// <summary>Keycloak realm rollerini (<c>realm_access.roles</c> claim'i, JSON dizi)
/// ASP.NET'in beklediği <see cref="ClaimTypes.Role"/> claim'lerine çevirir; böylece
/// <c>[Authorize(Roles = Roles.Admin)]</c> doğrudan çalışır.</summary>
public sealed class KeycloakRoleMapping : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal ilke)
    {
        if (ilke.Identity is not ClaimsIdentity kimlik ||
            ilke.FindFirst("realm_access")?.Value is not { } realmAccess)
        {
            return Task.FromResult(ilke);
        }

        // Dar-tipli parse (AGENTS §4.6 izinli bölge): claim imzalı token'dan gelir ama
        // biçim beklenmedikse (bozuk JSON / roles dizi değil) rol eklemeden geç —
        // her istekte çalışan bu dönüşüm 500'e dönüşmesin.
        JsonDocument belge;
        try { belge = JsonDocument.Parse(realmAccess); }
        catch (JsonException) { return Task.FromResult(ilke); }

        using (belge)
        {
            if (belge.RootElement.TryGetProperty("roles", out var roller) &&
                roller.ValueKind == JsonValueKind.Array)
            {
                foreach (var rol in roller.EnumerateArray())
                {
                    if (rol.GetString() is { } ad && !kimlik.HasClaim(ClaimTypes.Role, ad))
                        kimlik.AddClaim(new Claim(ClaimTypes.Role, ad));
                }
            }
        }

        return Task.FromResult(ilke);
    }
}
