using Kds.Contracts.Identity;
using Kds.Contracts.Usage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>S41 — Yönetim → Kullanım: "neyi kullanıyoruz, ne yavaş?". Kaynak denetim kayıtlarıdır
/// (S5), yani yönetim verisi — bu yüzden yalnız Admin. Kiracı ayrımı DbContext'in global query
/// filter'ından gelir; uç bir kiracı kimliği ALMAZ.</summary>
[ApiController]
[Route("api/kullanim")]
[Authorize(Roles = Roles.Admin)]
public sealed class KullanimController(ISender gonderici) : ControllerBase
{
    /// <param name="gun">Bakılacak dönem (1–365 arası kırpılır).</param>
    [HttpGet]
    public Task<UsageResult> Getir([FromQuery] int gun = 30, CancellationToken cancellationToken = default) =>
        gonderici.Send(new GetUsageRequest(gun), cancellationToken);
}
