using Kds.Business.Agents;
using Kds.Contracts.Agents;
using Kds.Contracts.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>S45 — Yönetim → Ajanlar: müşteri ağına kurulan KDS Ajanı kayıtları. Yönetim yalnız
/// Admin; ajanın kendi protokol uçları ayrı controller'da (aşağıda) ve oturumsuzdur.</summary>
[ApiController]
[Route("api/ajanlar")]
[Authorize(Roles = Roles.Admin)]
public sealed class AjanController(ISender gonderici) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<AgentDto>> Listele(CancellationToken cancellationToken) =>
        gonderici.Send(new ListAgentsRequest(), cancellationToken);

    /// <summary>Ajan kaydı + anahtarı üretir. Anahtar YALNIZ bu yanıtta döner (parola deseni).</summary>
    [HttpPost]
    public Task<CreateAgentResult> Uret(
        [FromBody] CreateAgentRequest istek, CancellationToken cancellationToken) =>
        gonderici.Send(istek, cancellationToken);

    /// <summary>Ajanı iptal eder (silmez) — anahtar bir sonraki istekte reddedilir.</summary>
    [HttpPost("{id:guid}/iptal")]
    public async Task<IActionResult> Iptal(Guid id, CancellationToken cancellationToken) =>
        await gonderici.Send(new RevokeAgentRequest(id), cancellationToken)
            ? NoContent()
            : NotFound();
}

/// <summary>S45 — AJAN PROTOKOLÜ: müşteri ağından DIŞARI DOĞRU gelen, OTURUMSUZ uçlar.
///
/// <para><b>Neden <c>AllowAnonymous</c>:</b> ajan bir arka plan sürecidir, Keycloak oturumu
/// olamaz. Yetkilendirme ANAHTARLA yapılır: imza + iptal kaydı + iş-ajan eşleşmesi
/// (<c>AjanKapisi</c> + handler'lar). Reddin sebebi bilerek ayrımsızdır (403) — Gömme'deki
/// numaralandırma korumasının aynısı.</para></summary>
[ApiController]
[Route("api/ajan")]
[AllowAnonymous]
public sealed class AjanProtokolController(ISender gonderici) : ControllerBase
{
    [HttpPost("isler")]
    public Task<IActionResult> Isler([FromBody] AjanIsleriRequest istek, CancellationToken ct) =>
        SarAsync(gonderici, istek, ct);

    [HttpPost("tablo-basla")]
    public Task<IActionResult> TabloBasla([FromBody] AjanTabloBaslaRequest istek, CancellationToken ct) =>
        SarAsync(gonderici, istek, ct);

    [HttpPost("satirlar")]
    public Task<IActionResult> Satirlar([FromBody] AjanSatirlarRequest istek, CancellationToken ct) =>
        SarAsync(gonderici, istek, ct);

    [HttpPost("tablo-bitir")]
    public Task<IActionResult> TabloBitir([FromBody] AjanTabloBitirRequest istek, CancellationToken ct) =>
        SarAsync(gonderici, istek, ct);

    [HttpPost("is-bitir")]
    public Task<IActionResult> IsBitir([FromBody] AjanIsBitirRequest istek, CancellationToken ct) =>
        SarAsync(gonderici, istek, ct);

    private static async Task<IActionResult> SarAsync<T>(ISender gonderici, MediatR.IRequest<T> istek, CancellationToken ct)
    {
        try
        {
            return new OkObjectResult(await gonderici.Send(istek, ct));
        }
        catch (AjanReddi hata)
        {
            // 403: anahtar yok/iptal ya da iş bu ajanın değil — hepsi AYNI cevap (numaralandırma koruması).
            return new ObjectResult(new { hata = hata.Message }) { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
