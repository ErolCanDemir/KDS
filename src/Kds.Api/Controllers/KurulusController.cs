using Kds.Contracts.Identity;
using Kds.Contracts.Organization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>S35 — Kuruluş Ayarları (Yönetim). Kiracının kendi adı, planı ve markası; yalnız Admin.
///
/// <para>Kiracı ayrımı burada rota/parametreyle DEĞİL, DbContext'in global query filter'ıyla
/// yapılır — uç bir kiracı kimliği ALMAZ, dolayısıyla "başka kiracının kuruluşunu düzenleme"
/// diye bir istek biçimi yoktur.</para></summary>
[ApiController]
[Route("api/kurulus")]
[Authorize(Roles = Roles.Admin)]
public sealed class KurulusController(ISender gonderici) : ControllerBase
{
    [HttpGet]
    public Task<OrganizationResult> Getir(CancellationToken cancellationToken) =>
        gonderici.Send(new GetOrganizationRequest(), cancellationToken);

    [HttpPut]
    public Task<OrganizationResult> Guncelle(
        [FromBody] UpdateOrganizationRequest istek, CancellationToken cancellationToken) =>
        gonderici.Send(istek, cancellationToken);

    /// <summary>Marka (logo + vurgu rengi) — uygulama kabuğu her açılışta çizmek için çağırır,
    /// bu yüzden Admin kısıtının DIŞINDADIR: aksi hâlde kuruluşun logosu yalnız yöneticinin
    /// ekranında görünürdü. Yönetim bilgisi (plan, limit, kiracı kimliği) bu uçtan dönmez.</summary>
    [HttpGet("marka")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici},{Roles.Musteri}")]
    public Task<BrandingResult> Marka(CancellationToken cancellationToken) =>
        gonderici.Send(new GetBrandingRequest(), cancellationToken);
}
