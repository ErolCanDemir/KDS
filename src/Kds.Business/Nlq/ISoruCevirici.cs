using Kds.Contracts.Nlq;

namespace Kds.Business.Nlq;

/// <summary>Soru çevirmeni SINIRI — iş katmanı "istem ver, ham model çıktısı al"dan fazlasını
/// bilmez. Somut uygulama Api katmanındaki Ollama adaptörüdür (<c>IEpostaGonderici</c> ile aynı
/// desen: IO kabuğu dışarıda, iş kuralı içeride ve sahte uygulamayla test edilir).</summary>
public interface ISoruCevirici
{
    /// <summary>İstemi yerel modele gönderir, HAM çıktıyı döndürür (ayıklama/doğrulama
    /// <see cref="ModelKurtarici"/>'nındır). Sağlayıcıya ulaşılamazsa <see cref="SoruCeviriHatasi"/>.</summary>
    Task<string> CevirAsync(string istem, CancellationToken ct);

    /// <summary>Sağlayıcı sağlık denetimi — ekran açılışta "Yerel model hazır mı?" rozetini doldurur.</summary>
    Task<NlqStatusResult> DurumAsync(CancellationToken ct);

    /// <summary>ISITMA (S34): istemin değişmeyen önekini (<see cref="SoruIstemleri.OnekKur"/>)
    /// modele önceden işletir — model belleğe yüklenir ve önek önbelleğe girer, böylece kullanıcının
    /// İLK sorusu da "ikinci soru" hızında cevaplanır.
    ///
    /// <para>Ölçüm (24 Ağu 2026): 928 token'lık önekin işlenmesi 49.7 sn sürüyor (19 tok/s); aynı
    /// önek ikinci kez 0.2 sn. Kullanıcı soruyu yazarken bu süre harcanırsa bekleme algılanmaz.</para>
    ///
    /// <para>ATEŞLE-UNUT: hata sessizce yutulur — ısıtma bir iyileştirmedir, ekranın çalışması
    /// ona bağlı değildir.</para></summary>
    Task IsitAsync(string onek, CancellationToken ct);
}

/// <summary>Yerel modele ulaşılamadı / model cevap veremedi — YAPILANDIRMA/ortam sorunudur
/// (Ollama kapalı, model indirilmemiş). Mesaj "ne yapmalı"yı söyler; API 503'e çevirir.</summary>
public sealed class SoruCeviriHatasi(string mesaj, Exception? ic = null) : Exception(mesaj, ic);
