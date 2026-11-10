using Kds.Contracts.Identity;
using Kds.Contracts.Ingest;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>İçe alım işleri (S12 · Mod B): İçe Alım modlu kaynaktan seçili tabloları tenant deposuna
/// tam yükleme. Yalnız Admin+Geliştirici. Rota Türkçe kalır (İngilizce geçiş kuralı).</summary>
[ApiController]
[Route("api/ice-alim")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
public sealed class IngestJobsController(ISender gonderici) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<IngestJobSummary>> Listele(CancellationToken cancellationToken) =>
        gonderici.Send(new ListIngestJobsRequest(), cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IngestJobDetail>> Getir(Guid id, CancellationToken cancellationToken) =>
        await gonderici.Send(new GetIngestJobRequest(id), cancellationToken) is { } detay ? Ok(detay) : NotFound();

    [HttpPost]
    public Task<IngestJobSummary> Ekle([FromBody] SaveIngestJobRequest komut, CancellationToken cancellationToken) =>
        gonderici.Send(komut, cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<IngestJobSummary> Guncelle(Guid id, [FromBody] SaveIngestJobRequest govde, CancellationToken cancellationToken) =>
        gonderici.Send(govde with { Id = id }, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Sil(Guid id, CancellationToken cancellationToken) =>
        await gonderici.Send(new DeleteIngestJobRequest(id), cancellationToken) ? NoContent() : NotFound();

    /// <summary>İşi ŞİMDİ çalıştırır (elle tetik); sonuç tablo/satır/süre. S44:
    /// <c>tamYukleme=true</c> → artımlı su seviyeleri sıfırlanır, her tablo baştan yüklenir
    /// (artımlının taşımadığı silinmiş satırlar böyle temizlenir).</summary>
    [HttpPost("{id:guid}/calistir")]
    public Task<IngestRunResult> Calistir(Guid id, [FromQuery] bool tamYukleme, CancellationToken cancellationToken) =>
        gonderici.Send(new RunIngestJobRequest(id, tamYukleme), cancellationToken);
}
