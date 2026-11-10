using Kds.Contracts.Identity;
using Kds.Contracts.Reports;
using Kds.Export;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>Raporlar (S10): rapor = sorgu modeli + görselleştirme tercihi. Listeleme/görüntüleme/favori
/// TÜM rollere açık (musteri yalnız Ekip paylaşımlıları görür — görünürlük handler'da); kaydetme/silme
/// ve klasör yönetimi yalnız Admin + Geliştirici.</summary>
[ApiController]
[Route("api/raporlar")]
public sealed class ReportsController(ISender gonderici, IResultExporter disaAktarici) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ReportSummary>> Listele(CancellationToken cancellationToken) =>
        gonderici.Send(new ListReportsRequest(), cancellationToken);

    [HttpGet("klasorler")]
    public Task<IReadOnlyList<FolderDto>> Klasorler(CancellationToken cancellationToken) =>
        gonderici.Send(new ListFoldersRequest(), cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReportDetail>> Getir(Guid id, CancellationToken cancellationToken)
    {
        var d = await gonderici.Send(new GetReportRequest(id), cancellationToken);
        return d is null ? NotFound() : Ok(d);
    }

    /// <summary>Raporu çalıştırır — TÜM kimlikli roller (kod incelemesi K1): görünürlük handler'da
    /// (Özel → sahibi + admin; Ekip → herkes) denetlenir; musteri paylaşılan raporu böylece
    /// koşabilir. İcra yine tek kapıdan (bekçi + satır sınırı + denetim) geçer. Gövde: parametreli
    /// raporun çalıştırma-anı değerleri (anahtar→değer; boş {} → parametre varsayılanları).</summary>
    [HttpPost("{id:guid}/calistir")]
    public Task<Kds.Contracts.Queries.QueryRunResult> Calistir(
        Guid id, [FromBody] Dictionary<string, object?>? parametreDegerleri, CancellationToken cancellationToken) =>
        gonderici.Send(new RunReportRequest(id, parametreDegerleri), cancellationToken);

    /// <summary>Raporu çalıştırır ve sonucu CSV/Excel/PDF dosyası olarak indirir (S13). Görünürlük +
    /// tek kapı (bekçi/sınır/denetim) yine geçerli — çalıştırmayla aynı yol.</summary>
    [HttpGet("{id:guid}/disa-aktar")]
    public async Task<IActionResult> DisaAktar(Guid id, [FromQuery] ExportFormat bicim, CancellationToken cancellationToken)
    {
        var sonuc = await gonderici.Send(new RunReportRequest(id), cancellationToken);
        var detay = await gonderici.Send(new GetReportRequest(id), cancellationToken);
        var dosya = disaAktarici.DisaAktar(sonuc, bicim, detay?.Ad ?? "rapor");
        return File(dosya.Icerik, dosya.IcerikTipi, dosya.DosyaAdi);
    }

    [HttpPost("{id:guid}/favori")]
    public async Task<IActionResult> Favori(Guid id, [FromQuery] bool favori, CancellationToken cancellationToken) =>
        await gonderici.Send(new SetReportFavoriteRequest(id, favori), cancellationToken) ? NoContent() : NotFound();

    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
    public async Task<ActionResult<ReportSummary>> Ekle([FromBody] SaveReportRequest komut, CancellationToken cancellationToken) =>
        Ok(await gonderici.Send(komut with { Id = null }, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
    public async Task<ActionResult<ReportSummary>> Guncelle(Guid id, [FromBody] SaveReportRequest komut, CancellationToken cancellationToken) =>
        Ok(await gonderici.Send(komut with { Id = id }, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
    public async Task<IActionResult> Sil(Guid id, CancellationToken cancellationToken) =>
        await gonderici.Send(new DeleteReportRequest(id), cancellationToken) ? NoContent() : NotFound();

    [HttpPost("klasorler")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
    public Task<FolderDto> KlasorEkle([FromBody] AddFolderRequest komut, CancellationToken cancellationToken) =>
        gonderici.Send(komut, cancellationToken);

    [HttpDelete("klasorler/{id:guid}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
    public async Task<IActionResult> KlasorSil(Guid id, CancellationToken cancellationToken) =>
        await gonderici.Send(new DeleteFolderRequest(id), cancellationToken) ? NoContent() : NotFound();

    /// <summary>S27 — klasöre rol erişimi ver/güncelle (boş liste kaldırır). Rol tanımı yönetişim
    /// işi olduğundan yalnız admin.</summary>
    [HttpPut("klasorler/{id:guid}/roller")]
    [Authorize(Roles = Roles.Admin)]
    public Task<FolderDto> KlasorRolleri(Guid id, [FromBody] IReadOnlyList<string>? roller, CancellationToken cancellationToken) =>
        gonderici.Send(new SetFolderRolesRequest(id, roller), cancellationToken);
}
