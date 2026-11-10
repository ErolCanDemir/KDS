using System.Security.Claims;
using Kds.Contracts.Identity;
using Kds.Contracts.SystemStatus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

[ApiController]
[Route("api/durum")]
public sealed class StatusController(ISender gonderici) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public Task<SystemStatusResponse> Getir(CancellationToken cancellationToken) =>
        gonderici.Send(new SystemStatusRequest(), cancellationToken);

    /// <summary>Ürünün tanıdığı roller — whoami yalnız bunları döndürür.</summary>
    private static readonly string[] UrunRolleri = [Roles.Admin, Roles.Gelistirici, Roles.Musteri];

    /// <summary>Kimlik hattının dikey kanıtı: token'sız 401; geçerli Keycloak
    /// token'ıyla kullanıcı adı + ÜRÜN rolleri + tenant claim'ini döndürür.</summary>
    [HttpGet("kimlik")]
    [Authorize]
    public IActionResult KimlikGetir() => Ok(new
    {
        Ad = User.Identity?.Name,
        // JSON wire Türkçe kalır (frontend `roller` okur — İngilizce geçişinde yanlışlıkla
        // `Roles` olmuştu → istemci rolleri hep boş; rol-korumalı menü/butonlar gizleniyordu).
        // Yalnız ÜRÜN rolleri süzülür: Keycloak token'ında default-roles-kds / offline_access /
        // uma_authorization gibi teknik roller de gelir ve ekrandaki rol rozetinde aynen
        // görünüyordu (ekran testi bulgusu: bayi rozeti "default-roles-kds · offline_a…" diyordu).
        // Menü/buton görünürlüğü de ürün rollerini beklediği için süzme davranış bozmaz.
        Roller = User.FindAll(ClaimTypes.Role).Select(r => r.Value)
            .Where(r => UrunRolleri.Contains(r, StringComparer.OrdinalIgnoreCase)),
        TenantId = User.FindFirst("tenant_id")?.Value,
    });

    /// <summary>Rol zorlamasının kanıtı: yalnız 'admin' rolü erişir, diğer roller 403.</summary>
    [HttpGet("admin-alani")]
    [Authorize(Roles = Roles.Admin)]
    public IActionResult AdminAlani() => Ok(new { Mesaj = "Admin alanına erişim var." });
}
