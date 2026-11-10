using Kds.Contracts.Alerts;
using Kds.Contracts.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>Eşik uyarıları (S20): bir raporun değeri sınırı aşınca e-posta.
///
/// <para><b>Müşteri rolü de kullanır</b> (kullanıcı kararı 19 Ağu 2026: "hem zamanlanmışları hem de
/// uyarıları müşteriye de gösterelim"). Rol kapısı kaldırılmaz, DARALTILIR: müşteri yalnız kendi
/// uyarılarını görür/yönetir ve yalnız erişebildiği raporları izleyebilir — kapı handler'larda
/// (AlertCommon.Gorunur / RaporErisimDogrula), böylece kural tek yerde durur ve ileride eklenecek
/// bir uç yanlışlıkla açık kalmaz.</para></summary>
[ApiController]
[Route("api/uyarilar")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici},{Roles.Musteri}")]
public sealed class AlertsController(ISender gonderici) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<AlertSummary>> Listele(CancellationToken cancellationToken) =>
        gonderici.Send(new ListAlertsRequest(), cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<AlertDetail> Getir(Guid id, CancellationToken cancellationToken) =>
        gonderici.Send(new GetAlertRequest(id), cancellationToken);

    [HttpPost]
    public async Task<ActionResult<AlertSummary>> Ekle([FromBody] SaveAlertRequest komut, CancellationToken cancellationToken) =>
        Ok(await gonderici.Send(komut with { Id = null }, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AlertSummary>> Guncelle(Guid id, [FromBody] SaveAlertRequest komut, CancellationToken cancellationToken) =>
        Ok(await gonderici.Send(komut with { Id = id }, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Sil(Guid id, CancellationToken cancellationToken) =>
        await gonderici.Send(new DeleteAlertRequest(id), cancellationToken) ? NoContent() : NotFound();

    /// <summary>"Şimdi Dene": ölçer ve durumu yazar ama <b>e-posta ATMAZ</b> — kurulum sırasında
    /// deneme yapan kişi alıcılara posta yağdırmasın.
    ///
    /// <para>Önce <see cref="GetAlertRequest"/> ile erişim doğrulanır: koşum handler'ı zamanlayıcıyla
    /// paylaşıldığından kendi içinde sahiplik kapısı taşıyamaz (orada kullanıcı bağlamı yoktur).</para></summary>
    [HttpPost("{id:guid}/dene")]
    public async Task<ActionResult<AlertRunResult>> Dene(Guid id, CancellationToken cancellationToken)
    {
        await gonderici.Send(new GetAlertRequest(id), cancellationToken);   // erişilemezse 404 fırlatır
        return Ok(await gonderici.Send(new RunAlertRequest(id, BildirimGonder: false), cancellationToken));
    }
}
