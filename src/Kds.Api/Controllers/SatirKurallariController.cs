using Kds.Contracts.Identity;
using Kds.Contracts.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>Satır-düzeyi güvenlik (RLS) kuralları — kiracı İÇİNDE kullanıcı/rol bazlı daraltma
/// ("bayi yalnız kendi servisinin taleplerini görsün").
/// <para>Uç TAMAMEN Admin'e kapalıdır: kuralı görebilen, hangi verinin kimden saklandığını da
/// görür; ayrıca kuralı düzenleyebilen kısıtlamayı kaldırabilir. Kuralların uygulanması sorgu
/// üretiminde olur (kullanıcının dokunamadığı yer) — bu uç yalnız TANIMLAR.</para></summary>
[ApiController]
[Route("api/satir-kurallari")]
[Authorize(Roles = Roles.Admin)]
public sealed class SatirKurallariController(ISender gonderici) : ControllerBase
{
    /// <summary>Kuralları listeler; <paramref name="haritaId"/> verilirse yalnız o haritanınkiler.</summary>
    [HttpGet]
    public Task<IReadOnlyList<SatirKuraliDto>> Listele(
        [FromQuery] Guid? haritaId, CancellationToken cancellationToken) =>
        gonderici.Send(new ListSatirKurallariRequest(haritaId), cancellationToken);

    /// <summary>Kural ekler/günceller. Tablo-harita tutarlılığı ve kolonun varlığı BURADA
    /// doğrulanır: yanlış kural çalıştırma anında sorguyu tamamen durdurur (fail-closed).</summary>
    [HttpPost]
    public Task<SatirKuraliDto> Kaydet(
        [FromBody] SaveSatirKuraliRequest istek, CancellationToken cancellationToken) =>
        gonderici.Send(istek, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Sil(Guid id, CancellationToken cancellationToken) =>
        await gonderici.Send(new DeleteSatirKuraliRequest(id), cancellationToken)
            ? NoContent() : NotFound();
}
