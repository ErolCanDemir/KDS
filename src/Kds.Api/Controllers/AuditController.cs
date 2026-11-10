using Kds.Contracts.Audit;
using Kds.Contracts.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>Denetim (S13 · audit ekranı): kim/hangi SQL/ne zaman/sonuç. Yalnız Admin. Yazım S5'ten beri
/// (<c>QueryAuditBehavior</c>); bu uç okuma tarafıdır — filtreli + sayfalı.</summary>
[ApiController]
[Route("api/denetim")]
[Authorize(Roles = Roles.Admin)]
public sealed class AuditController(ISender gonderici) : ControllerBase
{
    [HttpGet]
    public Task<AuditPage> Listele([FromQuery] ListAuditRequest istek, CancellationToken cancellationToken) =>
        gonderici.Send(istek, cancellationToken);
}
