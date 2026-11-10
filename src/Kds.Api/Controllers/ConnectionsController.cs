using Kds.Business.Connections;
using Kds.Contracts.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>Kaynak veritabanı bağlantı profilleri (S4). Yalnız Geliştirici + Admin.
/// Parola şifreli saklanır ve hiçbir uçtan geri dönmez.</summary>
[ApiController]
[Route("api/baglantilar")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
public sealed class ConnectionsController(ISender gonderici) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ConnectionSummary>> Listele(CancellationToken cancellationToken) =>
        gonderici.Send(new ListConnectionsRequest(), cancellationToken);

    [HttpPost]
    public async Task<ActionResult<ConnectionSummary>> Ekle([FromBody] AddConnectionRequest komut, CancellationToken cancellationToken) =>
        Ok(await gonderici.Send(komut, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ConnectionSummary>> Guncelle(Guid id, [FromBody] UpdateConnectionRequest govde, CancellationToken cancellationToken)
    {
        var result = await gonderici.Send(govde with { Id = id }, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Sil(Guid id, CancellationToken cancellationToken) =>
        await gonderici.Send(new DeleteConnectionRequest(id), cancellationToken) ? NoContent() : NotFound();

    /// <summary>Form değerleriyle (kaydetmeden) dener.</summary>
    [HttpPost("test")]
    public Task<ConnectionTestResult> Test([FromBody] TestConnectionRequest komut, CancellationToken cancellationToken) =>
        gonderici.Send(komut, cancellationToken);

    /// <summary>Kayıtlı bağlantıyı dener ve son test sonucunu işler.</summary>
    [HttpPost("{id:guid}/test")]
    public Task<ConnectionTestResult> TestId(Guid id, CancellationToken cancellationToken) =>
        gonderici.Send(new TestConnectionByIdRequest(id), cancellationToken);
}
