using System.Text.Json;
using System.Text.Json.Serialization;
using Kds.Business.Identity;
using Kds.Data;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Reports;

/// <summary>Report handler'larının ortak parçaları: model JSON ayarı (wire ile aynı biçim —
/// camelCase + string enum; polimorfik filtre <c>tur</c> ayrımcısı tip üstündeki attribute'tan),
/// local kullanıcı çözümü ve görünürlük kuralı.</summary>
internal static class ReportCommon
{
    /// <summary>ModelJson biçimi frontend'in gönderdiğiyle birebir — DB'deki JSON okunaklı ve
    /// API'ye olduğu gibi taşınabilir.</summary>
    public static readonly JsonSerializerOptions JsonAyar = Olustur();

    private static JsonSerializerOptions Olustur()
    {
        var a = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        a.Converters.Add(new JsonStringEnumConverter());
        return a;
    }

    /// <summary>Token'daki kullanıcının local <c>User.Id</c>'si (JIT provisioning garanti eder;
    /// yarış anında henüz yoksa null).</summary>
    public static Task<Guid?> MyIdAsync(KdsDbContext context, IUserContext kb, CancellationToken cancellationToken) =>
        kb.KeycloakId is not Guid kid
            ? Task.FromResult<Guid?>(null)
            : context.Kullanicilar.Where(k => k.KeycloakId == kid).Select(k => (Guid?)k.Id).FirstOrDefaultAsync(cancellationToken);

}

/// <summary>S27 — TEKİL rapor görünürlüğü İŞ KURALI (liste süzgeciyle AYNI; iki yer koparsa
/// "listede yok ama URL ile açılıyor" sızıntısı doğar): admin | sahibi | Ekip | Roller-kesişimi |
/// (özel değil + klasörünün rol erişimi kesişiyor). Public: testler kapıyı doğrudan sınar.</summary>
public static class RaporGorunurluk
{
    public static async Task<bool> GorebilirAsync(
        KdsDbContext context, IUserContext kb, Data.Entities.Report r, Guid? myId, CancellationToken ct)
    {
        if (kb.IsAdmin || r.SahipId == myId) return true;
        if (r.Paylasim == Contracts.Reports.ReportSharing.Ekip) return true;
        if (r.Paylasim == Contracts.Reports.ReportSharing.Roller
            && RolKurallari.Kesisir(r.PaylasimRolleri, kb.Roller))
        {
            return true;
        }

        if (r.Paylasim != Contracts.Reports.ReportSharing.Ozel && r.KlasorId is Guid klasorId)
        {
            var erisim = await context.RaporKlasorleri.Where(k => k.Id == klasorId)
                .Select(k => k.ErisimRolleri).FirstOrDefaultAsync(ct);
            if (RolKurallari.Kesisir(erisim, kb.Roller)) return true;
        }

        return false;
    }
}
