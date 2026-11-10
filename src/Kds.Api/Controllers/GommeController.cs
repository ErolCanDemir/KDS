using Kds.Business.Embedding;
using Kds.Contracts.Embedding;
using Kds.Contracts.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>S42 — Yönetim → Gömme &amp; Marka. Anahtar YÖNETİMİ yalnız Admin'e açıktır; gömülü
/// KOŞUM ise oturumsuzdur ve ayrı bir uçta (aşağıda) yaşar.</summary>
[ApiController]
[Route("api/gomme")]
[Authorize(Roles = Roles.Admin)]
public sealed class GommeController(ISender gonderici) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<EmbedTokenDto>> Listele(CancellationToken cancellationToken) =>
        gonderici.Send(new ListEmbedTokensRequest(), cancellationToken);

    /// <summary>Anahtar üretir. Üretilen dize YALNIZ bu yanıtta döner; saklanmaz ve listede bir
    /// daha gösterilmez (parola deseni).</summary>
    [HttpPost]
    public Task<CreateEmbedTokenResult> Uret(
        [FromBody] CreateEmbedTokenRequest istek, CancellationToken cancellationToken) =>
        gonderici.Send(istek, cancellationToken);

    /// <summary>Anahtarı iptal eder (silmez) — sızıntı senaryosunda anında kapatmak için.</summary>
    [HttpPost("{id:guid}/iptal")]
    public async Task<IActionResult> Iptal(Guid id, CancellationToken cancellationToken) =>
        await gonderici.Send(new RevokeEmbedTokenRequest(id), cancellationToken)
            ? NoContent()
            : NotFound();
}

/// <summary>S42 — GÖMÜLÜ KOŞUM: dış siteden gelen, OTURUMSUZ uç.
///
/// <para><b>Neden <c>AllowAnonymous</c>:</b> istek müşterinin kendi portalından gelir, orada KDS
/// oturumu yoktur. Yetkilendirme Keycloak yerine ANAHTARIN kendisiyle yapılır: imza + iptal/süre
/// kaydı + izinli alan kontrolü + anahtarın taşıdığı kimlikle RLS. Bu zincir
/// <c>RunEmbeddedHandler</c>'da toplanmıştır.</para>
///
/// <para><b>Kaynak sunucudan okunur:</b> izinli alan denetimi <c>Origin</c>/<c>Referer</c>
/// başlığına bakar — istemciden gelen bir gövde alanına DEĞİL. Gövdeye güvenseydik saldırgan
/// izinli alanı kendisi yazardı.</para></summary>
[ApiController]
[Route("api/gomulu")]
[AllowAnonymous]
[EnableCors("GomuluCors")]   // ana politika önyüz adresine kilitli; gömme müşterinin portalından gelir
public sealed class GomuluController(ISender gonderici) : ControllerBase
{
    [HttpPost("calistir")]
    public async Task<IActionResult> Calistir(
        [FromBody] GomuluIstek istek, CancellationToken cancellationToken)
    {
        var kaynak = Request.Headers.Origin.FirstOrDefault()
            ?? Request.Headers.Referer.FirstOrDefault();

        try
        {
            var sonuc = await gonderici.Send(
                new RunEmbeddedRequest(istek.Anahtar, kaynak), cancellationToken);
            return Ok(sonuc);
        }
        catch (GommeReddi hata)
        {
            // 403: anahtar yok/iptal/süresi dolmuş/izinsiz kaynak — hepsi AYNI cevap. Ayrım
            // yapmak, saldırgana geçerli anahtar aramada geri bildirim vermek olurdu.
            return StatusCode(StatusCodes.Status403Forbidden, new { hata = hata.Message });
        }
    }

    /// <param name="Anahtar">İmzalı gömme anahtarı.</param>
    public sealed record GomuluIstek(string Anahtar);
}
