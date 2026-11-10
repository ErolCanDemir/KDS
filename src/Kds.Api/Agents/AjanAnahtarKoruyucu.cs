using Kds.Business.Agents;
using Microsoft.AspNetCore.DataProtection;

namespace Kds.Api.Agents;

/// <summary>S45 — ajan anahtarını Data Protection ile imzalar/çözer (GommeAnahtarKoruyucu'nun eşi).
/// Halka KALICI veri dizinindedir; amaç dizesi gömme ve parola amaçlarından AYRIDIR — bir amaçla
/// üretilen belirteç diğerinde çözülemez.</summary>
public sealed class AjanAnahtarKoruyucu(IDataProtectionProvider saglayici) : IAjanAnahtarKoruyucu
{
    private readonly IDataProtector koruyucu = saglayici.CreateProtector("Kds.Ajan.Anahtar.v1");

    public string Uret(Guid ajanId) => koruyucu.Protect(ajanId.ToString("N"));

    public Guid? Coz(string anahtar)
    {
        if (string.IsNullOrWhiteSpace(anahtar))
        {
            return null;
        }

        try
        {
            return Guid.TryParse(koruyucu.Unprotect(anahtar.Trim()), out var id) ? id : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Kurcalanmış/başka halkayla üretilmiş anahtar: sebep yutulur, yalnız "geçersiz"
            // bilgisi döner (Gömme'deki karar — kripto hata sınıfı sızıntıdır). IO/kripto
            // sınırında try/catch meşru (AGENTS §4.6).
            return null;
        }
    }
}
