namespace Kds.Business.Agents;

/// <summary>S45 — ajan anahtarının imzalanması/çözülmesi (Gömme'deki desenin eşi; uygulama Api
/// katmanındaki Data Protection halkasında, AMAÇ DİZESİ FARKLI — gömme anahtarı ajan anahtarı
/// olarak kullanılamaz, tersi de). Anahtarın içinde yalnız kayıt kimliği taşınır; yetki DB
/// satırındadır → iptal ANINDA etkili.</summary>
public interface IAjanAnahtarKoruyucu
{
    string Uret(Guid ajanId);

    /// <summary>Bozuk/kurcalanmış anahtarda <c>null</c> — ayrıntı sızdırılmaz.</summary>
    Guid? Coz(string anahtar);
}
