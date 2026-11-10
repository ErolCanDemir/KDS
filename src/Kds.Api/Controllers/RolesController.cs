using Kds.Contracts.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>Roles &amp; Yetkiler (S27): rol tanımı + üye yönetimi. Roller Keycloak realm rolleridir —
/// burada açılan rol girişte token'a yazılır; RLS ve rapor/klasör görünürlüğü aynı ada bakar.
///
/// <para><b>Roller:</b> yalnız admin — rol açmak/üye atamak yönetişim işidir. Üye değişikliği
/// kullanıcının SONRAKİ girişinde token'a yansır (oturumdaki token değişmez; ekran bunu söyler).</para></summary>
[ApiController]
[Route("api/roller")]
[Authorize(Roles = Roles.Admin)]
public sealed class RolesController(ISender gonderici) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<RolOzeti>> Listele(CancellationToken cancellationToken) =>
        gonderici.Send(new ListRolesRequest(), cancellationToken);

    [HttpPost]
    public Task<RolOzeti> Olustur([FromBody] CreateRoleRequest istek, CancellationToken cancellationToken) =>
        gonderici.Send(istek, cancellationToken);

    [HttpDelete("{ad}")]
    public async Task<IActionResult> Sil(string ad, CancellationToken cancellationToken) =>
        await gonderici.Send(new DeleteRoleRequest(ad), cancellationToken) ? NoContent() : NotFound();

    [HttpGet("{ad}/uyeler")]
    public Task<IReadOnlyList<RolUyesi>> Uyeler(string ad, CancellationToken cancellationToken) =>
        gonderici.Send(new ListRoleMembersRequest(ad), cancellationToken);

    [HttpPost("{ad}/uyeler/{keycloakId:guid}")]
    public async Task<IActionResult> UyeEkle(string ad, Guid keycloakId, CancellationToken cancellationToken)
    {
        await gonderici.Send(new AddRoleMemberRequest(ad, keycloakId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{ad}/uyeler/{keycloakId:guid}")]
    public async Task<IActionResult> UyeCikar(string ad, Guid keycloakId, CancellationToken cancellationToken)
    {
        await gonderici.Send(new RemoveRoleMemberRequest(ad, keycloakId), cancellationToken);
        return NoContent();
    }

    /// <summary>Üye ekleme adayları — yalnız isteği yapanın kiracısındaki kullanıcılar.</summary>
    [HttpGet("kullanicilar")]
    public Task<IReadOnlyList<RolUyesi>> KullaniciAra([FromQuery] string? arama, CancellationToken cancellationToken) =>
        gonderici.Send(new SearchUsersRequest(arama), cancellationToken);

    /// <summary>Servis hazır mı? Değilse sebep, kurulum yönergesidir (ekran karta basar).</summary>
    [HttpGet("durum")]
    public Task<RolYonetimDurumu> Durum(CancellationToken cancellationToken) =>
        gonderici.Send(new RolYonetimDurumuRequest(), cancellationToken);
}
