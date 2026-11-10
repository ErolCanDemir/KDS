using System.Text.Json;
using Kds.Business.Identity;
using Kds.Contracts.Identity;

namespace Kds.Business.Security;

/// <summary>Zamanlanmış işin (abonelik · uyarı) <b>sahip kimliği anlık görüntüsü</b>: iş kurulurken
/// token'dan okunan rol + iddia kümesi işle birlikte saklanır, koşumda geri yüklenir.
///
/// <para><b>Neden anlık görüntü, neden canlı sorgu değil:</b> arka plan koşumu HTTP isteği taşımaz;
/// kullanıcının güncel rollerini almak için Keycloak'a gitmek koşumu dış servise ve ağa bağımlı
/// kılardı (kimlik sağlayıcı erişilemezse ya iş düşer ya da — daha kötüsü — filtresiz koşma
/// baskısı doğar). Anlık görüntü koşumu kendi kendine yeter kılar.</para>
///
/// <para><b>Bilinçli bedeli:</b> kullanıcının rolü/niteliği sonradan değişirse iş ESKİ kimlikle
/// koşar. Bu yüzden görüntü, iş her kaydedildiğinde tazelenir ve yetki daraltıldığında yöneticinin
/// işi güncellemesi/pasife alması gerektiği dokümanda yazılıdır. Alternatifi (canlı sorgu) daha
/// kırılgan, tümden yasak (önceki kapı) ise özelliği kullanılmaz kılıyordu.</para></summary>
public static class KimlikAnlikGoruntusu
{
    private static readonly JsonSerializerOptions Ayar = new(JsonSerializerDefaults.Web);

    private sealed record Kayit(string[] Roller, string? KullaniciAdi, string? EPosta, Dictionary<string, string> Iddialar);

    /// <summary>İstek bağlamındaki kullanıcıdan görüntü üretir. Kimliksiz bağlamda (test/arka plan)
    /// null döner — çağıran bunu "kimlik yok" olarak ele alır.</summary>
    public static string? Yaz(IUserContext kb)
    {
        if (kb.KeycloakId is null && kb.Roller.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(
            new Kayit([.. kb.Roller], kb.KullaniciAdi, kb.EPosta, new Dictionary<string, string>(kb.Iddialar)), Ayar);
    }

    /// <summary>Saklanan görüntüyü geri yükler; yoksa/bozuksa null (dar-tipli parse — AGENTS §4.6).</summary>
    public static CalistiranKullanici? Oku(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var k = JsonSerializer.Deserialize<Kayit>(json, Ayar);
            return k is null ? null : new CalistiranKullanici(k.Roller, k.KullaniciAdi, k.EPosta, k.Iddialar);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
