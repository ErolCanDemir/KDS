using Kds.Business.Connections;
using Microsoft.AspNetCore.DataProtection;

namespace Kds.Api.Connections;

/// <summary><see cref="IPasswordProtector"/> uygulaması: ASP.NET Core Data Protection ile
/// şifreler/çözer. Anahtar halkası KALICI veri dizinindedir (Program.cs'te
/// <c>PersistKeysToFileSystem(VeriDizini.UygulamaAnahtarDizini())</c>) — varsayılan konum
/// container yeniden yaratıldığında uçar ve kayıtlı parolalar çözülemez hâle gelirdi.
/// Vault entegrasyonu araştırma kuyruğunda (00-analiz §7).</summary>
public sealed class PasswordProtector(IDataProtectionProvider saglayici) : IPasswordProtector
{
    private readonly IDataProtector koruyucu = saglayici.CreateProtector("Kds.Connection.Sifre.v1");

    public string Protect(string duzMetin) =>
        string.IsNullOrEmpty(duzMetin) ? string.Empty : koruyucu.Protect(duzMetin);

    public string Unprotect(string sifreli) =>
        string.IsNullOrEmpty(sifreli) ? string.Empty : koruyucu.Unprotect(sifreli);
}
