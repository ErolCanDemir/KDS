namespace Kds.Business.Embedding;

/// <summary>S42 — gömme anahtarının imzalanması/çözülmesi. Uygulama Api katmanındadır (Data
/// Protection oradaki halkayı kullanır); iş katmanı yalnız bu sınırı bilir.
///
/// <para><b>Anahtarın içinde ne var:</b> yalnız kayıt kimliği. Yetki, süre ve izinli alanlar
/// DB'deki satırda durur — anahtarın kendisi bir "taşıyıcı"dır, yetki belgesi değil. Böylece
/// iptal ANINDA etkili olur; yetkiyi anahtarın içine gömseydik, dağıtılmış bir anahtarı geri
/// çağırmanın yolu olmazdı.</para></summary>
public interface IGommeAnahtarKoruyucu
{
    /// <summary>Kayıt kimliğinden imzalı anahtar üretir.</summary>
    string Uret(Guid anahtarId);

    /// <summary>Anahtarı çözer. Bozuk/kurcalanmış anahtarda <c>null</c> döner — çağıran bunu
    /// "geçersiz anahtar" diye karşılar, ayrıntı SIZDIRMAZ (saldırgana ipucu vermeyelim).</summary>
    Guid? Coz(string anahtar);
}
