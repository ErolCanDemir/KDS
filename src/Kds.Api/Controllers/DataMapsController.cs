using Kds.Contracts.DataMaps;
using Kds.Contracts.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>Veri Haritaları (S6) — ham tabloları müşteri için kullanılabilir hâle getirme.
/// Yalnız Geliştirici + Admin. Şema uçları hedef DB'yi canlı okur (S5 şema keşfi).</summary>
[ApiController]
[Route("api/haritalar")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
public sealed class DataMapsController(ISender gonderici) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<DataMapSummary>> Listele(CancellationToken cancellationToken) =>
        gonderici.Send(new ListDataMapsRequest(), cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DataMapDetail>> Getir(Guid id, CancellationToken cancellationToken)
    {
        var d = await gonderici.Send(new GetDataMapRequest(id), cancellationToken);
        return d is null ? NotFound() : Ok(d);
    }

    [HttpPost]
    public async Task<ActionResult<DataMapSummary>> Ekle([FromBody] SaveDataMapRequest komut, CancellationToken cancellationToken) =>
        Ok(await gonderici.Send(komut with { Id = null }, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DataMapSummary>> Guncelle(Guid id, [FromBody] SaveDataMapRequest komut, CancellationToken cancellationToken) =>
        Ok(await gonderici.Send(komut with { Id = id }, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Sil(Guid id, CancellationToken cancellationToken) =>
        await gonderici.Send(new DeleteDataMapRequest(id), cancellationToken) ? NoContent() : NotFound();

    // --- Şema keşfi (canlı, hedef DB) ---

    [HttpGet("sema/tablolar")]
    public Task<IReadOnlyList<SchemaTableDto>> Tablolar(Guid baglantiId, string? arama, string? sema, int enCok, CancellationToken cancellationToken) =>
        gonderici.Send(new SearchSchemaTablesRequest(baglantiId, arama, sema, enCok <= 0 ? 30 : enCok), cancellationToken);

    [HttpGet("sema/kolonlar")]
    public Task<IReadOnlyList<SchemaColumnDto>> Kolonlar(Guid baglantiId, string sema, string tablo, CancellationToken cancellationToken) =>
        gonderici.Send(new GetTableColumnsRequest(baglantiId, sema, tablo), cancellationToken);

    [HttpGet("sema/iliskiler")]
    public Task<IReadOnlyList<SchemaRelationshipDto>> Iliskiler(Guid baglantiId, string sema, string tablo, CancellationToken cancellationToken) =>
        gonderici.Send(new RelationshipSuggestionsRequest(baglantiId, sema, tablo), cancellationToken);
}
