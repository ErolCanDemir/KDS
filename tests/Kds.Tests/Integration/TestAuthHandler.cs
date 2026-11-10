using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kds.Tests.Integration;

/// <summary>Entegrasyon testleri için Keycloak yerine geçen kimlik şeması. İsteğin başlıklarından
/// tenant/sub/rol okur; böylece testler farklı tenant ve rolleri taklit edebilir. <c>X-Test-Auth</c>
/// başlığı yoksa kimlik verilmez (anonim → 401 doğrulanabilir).</summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> secenekler,
    ILoggerFactory gunlukcu,
    UrlEncoder kodlayici)
    : AuthenticationHandler<AuthenticationSchemeOptions>(secenekler, gunlukcu, kodlayici)
{
    public const string SchemeName = "Test";
    public const string TenantA = "11111111-1111-1111-1111-111111111111";
    public const string TenantB = "22222222-2222-2222-2222-222222222222";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-Auth"))
            return Task.FromResult(AuthenticateResult.NoResult());

        var tenant = Request.Headers.TryGetValue("X-Test-Tenant", out var t) ? t.ToString() : TenantA;
        var sub = Request.Headers.TryGetValue("X-Test-Sub", out var s) ? s.ToString()
            : "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        var roller = Request.Headers.TryGetValue("X-Test-Roles", out var r) ? r.ToString() : "admin";

        var claimler = new List<Claim>
        {
            new("sub", sub),
            new("tenant_id", tenant),
            new("preferred_username", "test-" + roller),
            new("email", "test@example.com"),
            new("name", "Test User"),
        };
        foreach (var rol in roller.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            claimler.Add(new Claim(ClaimTypes.Role, rol));

        var kimlik = new ClaimsIdentity(claimler, SchemeName, "preferred_username", ClaimTypes.Role);
        var bilet = new AuthenticationTicket(new ClaimsPrincipal(kimlik), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(bilet));
    }
}
