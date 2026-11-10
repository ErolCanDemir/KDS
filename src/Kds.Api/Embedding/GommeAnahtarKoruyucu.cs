using Kds.Business.Embedding;
using Microsoft.AspNetCore.DataProtection;

namespace Kds.Api.Embedding;

/// <summary>S42 — gömme anahtarını Data Protection ile imzalar/çözer. Halka KALICI veri
/// dizinindedir (Program.cs); container yeniden yaratılınca uçmaz — yoksa dağıtılmış bütün gömme
/// anahtarları bir gecede geçersizleşirdi.
///
/// <para>Amaç dizesi bağlantı parolalarınkinden AYRIDIR: bir amaçla üretilen belirteç diğerinde
/// çözülemez, yani "gömme anahtarı" bir yerden alınıp parola çözmek için kullanılamaz.</para></summary>
public sealed class GommeAnahtarKoruyucu(IDataProtectionProvider saglayici) : IGommeAnahtarKoruyucu
{
    private readonly IDataProtector koruyucu = saglayici.CreateProtector("Kds.Gomme.Anahtar.v1");

    public string Uret(Guid anahtarId) => koruyucu.Protect(anahtarId.ToString("N"));

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
            /* Kurcalanmış/başka halkayla üretilmiş anahtar. Sebep YUTULUR ve çağırana yalnız
             * "geçersiz" bilgisi gider: hata ayrıntısı saldırgana anahtarın hangi aşamada
             * bozulduğunu söyler (kriptografik hata sınıflandırması sızıntıdır). IO/kripto
             * sınırında try/catch meşru (AGENTS §4.6). */
            return null;
        }
    }
}
