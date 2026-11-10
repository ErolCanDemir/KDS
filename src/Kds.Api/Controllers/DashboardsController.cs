using Kds.Contracts.Dashboards;
using Kds.Contracts.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>Panolar (S11): pano = adlandırılmış, sıralı rapor koleksiyonu. Listeleme/görüntüleme
/// TÜM kimlikli roller (görünürlük handler'da: Özel → sahibi + admin, Ekip → herkes); oluştur/
/// güncelle/sil yalnız Admin + Geliştirici.</summary>
[ApiController]
[Route("api/panolar")]
public sealed class DashboardsController(ISender gonderici) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<DashboardSummary>> Listele(CancellationToken cancellationToken) =>
        gonderici.Send(new ListDashboardsRequest(), cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DashboardDetail>> Getir(Guid id, CancellationToken cancellationToken)
    {
        var d = await gonderici.Send(new GetDashboardRequest(id), cancellationToken);
        return d is null ? NotFound() : Ok(d);
    }

    /// <summary>S43 — ana sayfa panosu. OKUMA tarafı olduğu için (favori ucundaki gibi) rol
    /// kısıtı YOK: pano oluşturamayan müşteri de görebildiği bir panoyu ana sayfası yapabilir.</summary>
    [HttpGet("ana-sayfam")]
    public Task<HomeDashboardInfo> AnaSayfam(CancellationToken cancellationToken) =>
        gonderici.Send(new GetHomeDashboardRequest(), cancellationToken);

    [HttpPut("ana-sayfam")]
    public async Task<IActionResult> AnaSayfamSec([FromQuery] Guid? panoId, CancellationToken cancellationToken) =>
        await gonderici.Send(new SetHomeDashboardRequest(panoId), cancellationToken) ? NoContent() : NotFound();

    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
    public async Task<ActionResult<DashboardSummary>> Ekle([FromBody] SaveDashboardRequest komut, CancellationToken cancellationToken) =>
        Ok(await gonderici.Send(komut with { Id = null }, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
    public async Task<ActionResult<DashboardSummary>> Guncelle(Guid id, [FromBody] SaveDashboardRequest komut, CancellationToken cancellationToken) =>
        Ok(await gonderici.Send(komut with { Id = id }, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
    public async Task<IActionResult> Sil(Guid id, CancellationToken cancellationToken) =>
        await gonderici.Send(new DeleteDashboardRequest(id), cancellationToken) ? NoContent() : NotFound();
}
