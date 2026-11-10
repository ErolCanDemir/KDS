using Kds.Api.Kurulum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api.Controllers;

/// <summary>S16/A2 — kurulum sihirbazının uçları. Uygulama HENÜZ KURULMAMIŞKEN açıktır
/// (kimlik sağlayıcı da bilinmediği için <see cref="AllowAnonymousAttribute"/>); güvenlik
/// tek kullanımlık <b>kurulum anahtarı</b> ile sağlanır (<see cref="KurulumAnahtari"/>).
/// Kurulum tamamlandıktan sonra <c>durum</c> dışındaki uçlar <b>410 Gone</b> döner — yani
/// kurulu bir sistemde bu yüzey yoktur.</summary>
[ApiController]
[Route("api/kurulum")]
[AllowAnonymous]
public sealed class KurulumController(
    KurulumDurumu durum, KurulumAnahtari anahtar, KurulumHizmeti hizmet) : ControllerBase
{
    private const string AnahtarBaslik = "X-Kurulum-Anahtari";

    /// <summary>Önyüzün açılışta sorduğu tek uç: kurulu mu? (anahtar istemez)</summary>
    [HttpGet("durum")]
    public KurulumDurumYanit Durum() => new(
        durum.Yapilandirildi,
        KurulumAyarlari.GecerliSurum,
        durum.Yapilandirildi ? null : VeriDizini.KurulumAnahtariDosyasi());

    [HttpPost("veritabani-test")]
    public async Task<IActionResult> VeritabaniTest([FromBody] VeritabaniIstek istek, CancellationToken ct)
    {
        if (Kapi() is IActionResult engel) return engel;
        return Ok(await hizmet.VeritabaniTestAsync(istek, ct));
    }

    [HttpPost("kimlik-test")]
    public async Task<IActionResult> KimlikTest([FromBody] KimlikIstek istek, CancellationToken ct)
    {
        if (Kapi() is IActionResult engel) return engel;
        return Ok(await hizmet.KimlikTestAsync(istek, ct));
    }

    [HttpPost("tamamla")]
    public async Task<IActionResult> Tamamla([FromBody] TamamlaIstek istek, CancellationToken ct)
    {
        if (Kapi() is IActionResult engel) return engel;
        var sonuc = await hizmet.TamamlaAsync(istek, ct);
        return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
    }

    /// <summary>Ortak kapı: kurulu sistemde uçlar yok (410); kurulum modunda anahtar zorunlu (401);
    /// çok yanlış denemede kilit (423).</summary>
    private IActionResult? Kapi()
    {
        if (durum.Yapilandirildi)
            return StatusCode(StatusCodes.Status410Gone, new { mesaj = "Uygulama zaten kurulu; kurulum uçları kapalı." });
        if (anahtar.Kilitli)
            return StatusCode(StatusCodes.Status423Locked,
                new { mesaj = "Çok sayıda hatalı kurulum anahtarı denendi; uygulamayı yeniden başlatın." });
        if (!anahtar.Dogrula(Request.Headers[AnahtarBaslik].ToString()))
            return Unauthorized(new { mesaj = "Kurulum anahtarı geçersiz. Sunucudaki kurulum-anahtari.txt dosyasına bakın." });
        return null;
    }
}
