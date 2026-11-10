using Kds.Contracts.Identity;
using Kds.Contracts.Queries;
using Kds.Contracts.QueryModel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>Sorgu tasarımcısı (S7 motoru + S8 UI'ı): sorgu modelinden PARAMETRELİ SQL üretir
/// ("SQL'i Göster") ve isteğe göre haritanın bağlantısında çalıştırır ("Çalıştır"). Model, existing
/// tenant'ın Veri Haritası'na göre doğrulanır (yalnız haritadaki tablo/kolon). Yalnız Geliştirici + Admin.</summary>
[ApiController]
[Route("api/sorgu")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Gelistirici}")]
public sealed class QueryController(ISender gonderici) : ControllerBase
{
    /// <summary>Sorgu modelini doğrular ve üretilen parametreli SQL + parametreleri döndürür (çalıştırmaz).
    /// Gövde: <c>{ model, parametreDegerleri? }</c> — çalıştırma-anı parametre değerleri (parametreli rapor).</summary>
    [HttpPost("sql")]
    public Task<GeneratedQuery> Generate([FromBody] GenerateQuerySqlRequest istek, CancellationToken cancellationToken) =>
        gonderici.Send(istek, cancellationToken);

    /// <summary>Modeli SQL'e çevirir ve haritanın bağlantısında çalıştırır; kolon + satır sonucu döndürür.
    /// Gövde: <c>{ model, parametreDegerleri? }</c>.</summary>
    [HttpPost("calistir")]
    public Task<QueryRunResult> Calistir([FromBody] RunQueryModelRequest istek, CancellationToken cancellationToken) =>
        gonderici.Send(istek, cancellationToken);

    /// <summary>Ham SQL ekranı (S9): elle yazılan SELECT'i seçilen bağlantıda çalıştırır.
    /// Aynı tek kapı — SELECT-only bekçisi + satır sınırı + zaman aşımı + denetim kaydı.
    /// <para><b>RLS kapısı:</b> satır-düzeyi kurala tabi bir kullanıcı buradan sorgu çalıştıramaz.
    /// Ham SQL modelden geçmediği için kural UYGULANAMAZ; izin vermek kısıtlamayı sessizce
    /// delerdi. (Kuralsız kullanıcılar ve admin etkilenmez.)</para></summary>
    [HttpPost("ham")]
    public async Task<QueryRunResult> Ham(
        [FromBody] RunQueryRequest komut,
        [FromServices] Kds.Business.Security.ISatirKuraliCozucu satirKuralCozucu,
        [FromServices] Kds.Business.Identity.IUserContext kullanici,
        CancellationToken cancellationToken)
    {
        var kim = new Kds.Contracts.Identity.CalistiranKullanici(
            kullanici.Roller, kullanici.KullaniciAdi, kullanici.EPosta, kullanici.Iddialar);
        if (await satirKuralCozucu.KuralaTabiMiAsync(kim, cancellationToken))
            throw new Kds.Business.Security.SatirKuraliHatasi(
                "Satır-düzeyi güvenlik kuralına tabi kullanıcılar elle SQL çalıştıramaz — kural yalnız " +
                "tasarımcıyla kurulan sorgulara uygulanabilir. Sorguyu Sorgu Tasarımcısı'nda kurun.");
        return await gonderici.Send(komut, cancellationToken);
    }

    /// <summary>M33 "Sorun Ne?" (tasarımcı): modeli SQL'e çevirip haritanın bağlantısında TANILAR —
    /// çalıştırmaz; tahmini plandan bulgular döner. Gövde: <c>{ model, parametreDegerleri? }</c>.</summary>
    [HttpPost("tanila-model")]
    public Task<QueryDiagnosis> TanilaModel([FromBody] DiagnoseQueryModelRequest istek, CancellationToken cancellationToken) =>
        gonderici.Send(istek, cancellationToken);

    /// <summary>M33 "Sorun Ne?" (ham SQL): elle yazılan SELECT'i seçilen bağlantıda TANILAR (çalıştırmaz).</summary>
    [HttpPost("tanila")]
    public Task<QueryDiagnosis> Tanila([FromBody] DiagnoseQueryRequest istek, CancellationToken cancellationToken) =>
        gonderici.Send(istek, cancellationToken);

    // M10: editör yüzeyinden dışa aktarım kaldırıldı (ham/disa-aktar ucu söküldü) — dışa aktarım
    // yalnız KAYITLI rapor üzerinden: GET /api/raporlar/{id}/disa-aktar (dosya adı = rapor adı).
}
