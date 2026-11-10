using Kds.Contracts.Identity;
using Kds.Contracts.Subscriptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>Rapor abonelikleri (S15 · "Zamanlanmış Raporlar"): bir raporu periyodik (cron) ya da elle
/// çalıştırıp PDF/Excel/CSV ekiyle e-postayla teslim. Rota Türkçe kalır.
///
/// <para><b>Müşteri rolü de abone olur</b> (kullanıcı kararı 19 Ağu 2026: "hem zamanlanmışları hem
/// de uyarıları müşteriye de gösterelim"). Rol kapısı kaldırılmaz, DARALTILIR: müşteri yalnız KENDİ
/// aboneliklerini görür/yönetir ve yalnız erişebildiği raporlara abone olur. Kural handler'larda
/// (<c>SubscriptionCommon.Gorunur</c> / <c>RaporErisimDogrula</c>) — tek yerde durduğu için ileride
/// eklenecek bir uç yanlışlıkla açık kalmaz.</para></summary>
[ApiController]
[Route("api/abonelikler")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici},{Roles.Musteri}")]
public sealed class SubscriptionsController(ISender gonderici) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<SubscriptionSummary>> Listele(CancellationToken cancellationToken) =>
        gonderici.Send(new ListSubscriptionsRequest(), cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SubscriptionDetail>> Getir(Guid id, CancellationToken cancellationToken) =>
        await gonderici.Send(new GetSubscriptionRequest(id), cancellationToken) is { } detay ? Ok(detay) : NotFound();

    [HttpPost]
    public Task<SubscriptionSummary> Ekle([FromBody] SaveSubscriptionRequest komut, CancellationToken cancellationToken) =>
        gonderici.Send(komut, cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<SubscriptionSummary> Guncelle(Guid id, [FromBody] SaveSubscriptionRequest govde, CancellationToken cancellationToken) =>
        gonderici.Send(govde with { Id = id }, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Sil(Guid id, CancellationToken cancellationToken) =>
        await gonderici.Send(new DeleteSubscriptionRequest(id), cancellationToken) ? NoContent() : NotFound();

    /// <summary>Aboneliği ŞİMDİ gönderir (elle tetik) — rapor koşulur, biçimlenir, alıcılara e-postalanır.
    ///
    /// <para>Önce <see cref="GetSubscriptionRequest"/> ile <b>erişim doğrulanır</b>: koşum handler'ı
    /// zamanlayıcıyla paylaşıldığı için kendi içinde sahiplik kapısı taşıyamaz (orada kullanıcı
    /// bağlamı yoktur). "Kimse başkasının id'sini bilemez" bir güvenlik kapısı değildir — müşteri
    /// başkasının aboneliğini elle tetikleyip yabancı alıcılara posta attıramaz.</para></summary>
    [HttpPost("{id:guid}/gonder")]
    public async Task<ActionResult<SubscriptionRunResult>> Gonder(Guid id, CancellationToken cancellationToken)
    {
        if (await gonderici.Send(new GetSubscriptionRequest(id), cancellationToken) is null)
        {
            return NotFound();
        }

        return Ok(await gonderici.Send(new RunSubscriptionRequest(id), cancellationToken));
    }
}
