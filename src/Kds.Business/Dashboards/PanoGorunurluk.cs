using Kds.Business.Identity;
using Kds.Contracts.Reports;

namespace Kds.Business.Dashboards;

/// <summary>Pano görünürlüğü İŞ KURALI — tek yer. Kopyalanırsa kopyalar zamanla ayrışır ve
/// "listede yok ama URL ile açılıyor" sızıntısı doğar (aynı uyarı rapor tarafında
/// <c>RaporGorunurluk</c> için de yazılı). Kural: admin | sahibi | Ekip | Roller-kesişimi.</summary>
public static class PanoGorunurluk
{
    /// <summary>Liste süzgeci projeksiyon üzerinden, tekil erişim entity üzerinden çağırır;
    /// ikisi de AYNI kararı versin diye kural alan alan alınır, tip değil.</summary>
    public static bool Gorebilir(IUserContext kb, ReportSharing paylasim, string? paylasimRolleri, Guid sahipId, Guid? myId) =>
        kb.IsAdmin
        || (myId is Guid ben && sahipId == ben)
        || paylasim == ReportSharing.Ekip
        || (paylasim == ReportSharing.Roller && RolKurallari.Kesisir(paylasimRolleri, kb.Roller));
}
