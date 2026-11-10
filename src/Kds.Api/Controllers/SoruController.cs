using Kds.Contracts.Identity;
using Kds.Contracts.Nlq;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>Soru Sor (S23 · NLQ): Türkçe soru → yerel model → JSON sorgu modeli → normal koşum.
///
/// <para><b>Roller:</b> herkese açık (müşteri dahil) — çünkü AI için AYRI bir koşum yolu yoktur:
/// üretilen model <c>RunQueryModelRequest</c> ile koşulur ve RLS/satır sınırı/bekçi/denetim soran
/// kişiye aynen uygulanır. Müşterinin soruyla görebileceği, tasarımcıda elle kurup göreceğinden
/// fazlası değildir.</para></summary>
[ApiController]
[Route("api/soru")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici},{Roles.Musteri}")]
public sealed class SoruController(ISender gonderici) : ControllerBase
{
    /// <summary>Sorar. Yerel model CPU'da uzun düşünebilir (60-120 sn olağan) — önyüz bekleme
    /// durumunu "yerel model düşünüyor" diye gösterir; bu bir hata değildir.</summary>
    [HttpPost]
    public Task<AskQuestionResult> Sor([FromBody] AskQuestionRequest istek, CancellationToken cancellationToken) =>
        gonderici.Send(istek, cancellationToken);

    /// <summary>Yerel model hazır mı? Ekran açılışta rozeti bununla doldurur; değilse kurulum
    /// yönergesi döner (AI yoksa ürün çökmez, yol gösterir — SQLST dayanıklılık dersi).</summary>
    [HttpGet("durum")]
    public Task<NlqStatusResult> Durum(CancellationToken cancellationToken) =>
        gonderici.Send(new NlqStatusRequest(), cancellationToken);

    /// <summary>S34 — ISITMA: ekran açılıp harita seçilince çağrılır, modele istem önekini önceden
    /// işletir. Kullanıcı sorusunu yazarken çalıştığı için beklenmez; önyüz sonucu YOK SAYAR.
    /// Ölçüm: önek işleme ilk seferde 49.7 sn, önbellekten 0.2 sn — yani ısıtma, ilk sorunun
    /// bekleme süresini yazma süresine gizler.</summary>
    [HttpPost("isit")]
    public Task<NlqIsitResult> Isit([FromBody] NlqIsitRequest istek, CancellationToken cancellationToken) =>
        gonderici.Send(istek, cancellationToken);
}
