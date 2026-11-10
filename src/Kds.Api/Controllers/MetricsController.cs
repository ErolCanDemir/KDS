using Kds.Contracts.Identity;
using Kds.Contracts.Metrics;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>Metrikler (S22): merkezi ölçüt kataloğu. Rota Türkçe kalır.
///
/// <para><b>Roller:</b> katalog herkese açık (müşteri dahil — menüde M/G/A), çünkü metrik
/// tanımları gizli değil ortak sözlüktür; "Ölç" de herkese açıktır ve normal koşum hattından
/// geçtiği için RLS kuralları ölçen kişiye aynen uygulanır. <b>Yazma</b> (tanımlama/silme) yalnız
/// admin + geliştiricidedir — ölçütün tanımı bir yönetişim kararıdır, "iki farklı ciro" sorununu
/// çözmek için tanım tek elde tutulur.</para></summary>
[ApiController]
[Route("api/metrikler")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici},{Roles.Musteri}")]
public sealed class MetricsController(ISender gonderici) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<MetricSummary>> Listele(CancellationToken cancellationToken) =>
        gonderici.Send(new ListMetricsRequest(), cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<MetricSummary> Getir(Guid id, CancellationToken cancellationToken) =>
        gonderici.Send(new GetMetricRequest(id), cancellationToken);

    /// <summary>Metriği kullanan raporların adları — silme onayı "şu raporlar kullanıyor" der.</summary>
    [HttpGet("{id:guid}/kullanim")]
    public Task<IReadOnlyList<string>> Kullanim(Guid id, CancellationToken cancellationToken) =>
        gonderici.Send(new GetMetricUsageRequest(id), cancellationToken);

    /// <summary>Güncel değeri ölçer. Normal koşum hattından geçer: RLS/sınır/denetim aynen uygulanır.</summary>
    [HttpPost("{id:guid}/olc")]
    public Task<MetricMeasureResult> Olc(Guid id, CancellationToken cancellationToken) =>
        gonderici.Send(new MeasureMetricRequest(id), cancellationToken);

    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
    public async Task<ActionResult<MetricSummary>> Ekle([FromBody] SaveMetricRequest komut, CancellationToken cancellationToken) =>
        Ok(await gonderici.Send(komut with { Id = null }, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
    public async Task<ActionResult<MetricSummary>> Guncelle(Guid id, [FromBody] SaveMetricRequest komut, CancellationToken cancellationToken) =>
        Ok(await gonderici.Send(komut with { Id = id }, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
    public async Task<IActionResult> Sil(Guid id, CancellationToken cancellationToken) =>
        await gonderici.Send(new DeleteMetricRequest(id), cancellationToken) ? NoContent() : NotFound();

    // ——— S30 · Sürüm geçmişi ———

    /// <summary>Sürüm geçmişi (en yenisi üstte). Okuma herkese açık — katalogla aynı gerekçe.</summary>
    [HttpGet("{id:guid}/surumler")]
    public Task<IReadOnlyList<MetricVersionSummary>> Surumler(Guid id, CancellationToken cancellationToken) =>
        gonderici.Send(new ListMetricVersionsRequest(id), cancellationToken);

    /// <summary>Seçili sürümü geri yükler — normal kaydetme yolundan geçer (doğrulama + yeni sürüm).</summary>
    [HttpPost("{id:guid}/surumler/{no:int}/don")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
    public Task<MetricSummary> SurumeDon(Guid id, int no, CancellationToken cancellationToken) =>
        gonderici.Send(new RestoreMetricVersionRequest(id, no), cancellationToken);

    // ——— S30 · Dışa / içe aktarma ———

    /// <summary>Kataloğu taşınabilir biçimde verir (harita ADI ile — ortamlar arası taşınır).</summary>
    [HttpGet("disa")]
    public Task<IReadOnlyList<MetricExportItem>> DisaAktar([FromQuery] Guid? haritaId, CancellationToken cancellationToken) =>
        gonderici.Send(new ExportMetricsRequest(haritaId), cancellationToken);

    /// <summary>Dışa aktarılmış metrikleri içe alır; sonuç öğe öğe ne olduğunu söyler.</summary>
    [HttpPost("ice")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
    public Task<IReadOnlyList<MetricImportItemResult>> IceAl([FromBody] ImportMetricsRequest komut, CancellationToken cancellationToken) =>
        gonderici.Send(komut, cancellationToken);

    /// <summary>S47 — formül düzenleyicinin fonksiyon ipucu (bekçiyle tek kaynak). Metrik
    /// tanımlayabilen herkese açık; sabit içerik.</summary>
    [HttpGet("fonksiyonlar")]
    public Task<IReadOnlyList<MetricFunctionGroup>> Fonksiyonlar(CancellationToken cancellationToken) =>
        gonderici.Send(new ListMetricFunctionsRequest(), cancellationToken);

    /// <summary>S46 — semantik katmanı OSI (Apache Ossie) YAML biçiminde verir: satıcıdan
    /// bağımsız değişim standardı. haritaId boş = tüm haritalar.</summary>
    [HttpGet("disa-osi")]
    public async Task<IActionResult> DisaAktarOsi([FromQuery] Guid? haritaId, CancellationToken cancellationToken) =>
        Content(await gonderici.Send(new ExportOsiMetricsRequest(haritaId), cancellationToken),
            "application/yaml; charset=utf-8");

    /// <summary>S46 — OSI YAML dosyasından metrikleri içe alır; sonuç sözleşmesi S30 ile aynı.</summary>
    [HttpPost("ice-osi")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
    public Task<IReadOnlyList<MetricImportItemResult>> IceAlOsi([FromBody] ImportOsiMetricsRequest komut, CancellationToken cancellationToken) =>
        gonderici.Send(komut, cancellationToken);
}
