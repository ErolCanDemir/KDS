using Kds.Business.Settings;
using Kds.Contracts.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>S16/A3 — Sistem Ayarları. Okuma/yazma yalnız Admin'e açıktır; <c>onyuz</c> ucu
/// her oturuma açıktır çünkü önyüzün davranış eşikleri (ör. "yavaş sorgu" sınırı) oradan gelir
/// — derlemeye gömülmesin diye.</summary>
[ApiController]
[Route("api/ayarlar")]
public sealed class SettingsController(ISender gonderici, ISistemAyarlari ayarlar) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public Task<IReadOnlyList<AyarDto>> Listele(CancellationToken cancellationToken) =>
        gonderici.Send(new ListSettingsRequest(), cancellationToken);

    /// <summary>Gövde: <c>{ "Sorgu.ZamanAsimiSn": "90", ... }</c>. Varsayılana eşit değerler silinir.</summary>
    [HttpPut]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Kaydet(
        [FromBody] Dictionary<string, string> degerler, CancellationToken cancellationToken)
    {
        var degisen = await gonderici.Send(new SaveSettingsRequest(degerler), cancellationToken);
        return Ok(new { degisen });
    }

    /// <summary>Önyüzün ihtiyaç duyduğu eşikler (kimliği doğrulanmış her kullanıcı).</summary>
    [HttpGet("onyuz")]
    public OnyuzAyarlari Onyuz() => new(ayarlar.Sayi(AyarTanimlari.TaniYavasEsikMs));
}

/// <summary>Önyüze açılan ayar yüzeyi — bilinçli olarak DAR: yalnız arayüz davranışını
/// etkileyen eşikler (sır/bağlantı asla).</summary>
public sealed record OnyuzAyarlari(int YavasEsikMs);
