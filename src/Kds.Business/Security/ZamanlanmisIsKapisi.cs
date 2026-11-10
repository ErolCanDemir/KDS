using System.Text.Json;
using Kds.Contracts.QueryModel;
using Kds.Data;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Security;

/// <summary>Zamanlanmış işlerin (abonelik · uyarı) satır-düzeyi güvenlik kapısı.
///
/// <para><b>Problem:</b> bu işler arka planda, kullanıcı bağlamı OLMADAN koşar. RLS ise kimliğe
/// dayanır: rol-kapsamlı kural (<c>Roller='musteri'</c>) boş rol listesiyle eşleşmez ve <b>sessizce
/// düşer</b> — çözücünün fail-closed koruması bile devreye girmez, çünkü o yalnız kural
/// UYGULANACAKKEN değeri çözülemezse çalışır. Ham SQL raporlarda ise kural zaten hiç uygulanamaz
/// (rapor modelden geçmez). İki durumda da sonuç aynı: kısıtlı satırlar e-postayla dışarı çıkar.</para>
///
/// <para><b>Karar:</b> kimliği taşıyamadığımız bir koşumda, kuralı olan veriye dokunulmaz.
/// Ürünün RLS felsefesi baştan beri bu: kısıtlamayı sessizce delmektense işi çalıştırmamak.</para>
///
/// <para><b>Kapı neden KOŞUM tarafında:</b> ilk denemede yalnız kurulum (kaydetme) kapatılmıştı ve
/// bu yetmiyordu — (a) kapı eklenmeden önce kurulmuş işler koşmaya devam ediyordu, (b) kural
/// SONRADAN eklenince mevcut işler etkilenmiyordu, (c) kapı işi KURAN'ın kimliğine baktığı için
/// admin'in kurup alıcıya kurala tabi kullanıcıyı yazması sızıntıyı geri getiriyordu. Veriye bakan
/// ve her koşumda çalışan kapı üçünü birden kapatır. Kaydetme sırasında da aynı kontrol yapılır —
/// ama yalnız kullanıcıya ERKEN haber vermek için; güvenliği sağlayan koşum tarafıdır.</para></summary>
public static class ZamanlanmisIsKapisi
{
    /// <summary>Raporun dayandığı veri kurallıysa fırlatır. <paramref name="ne"/> mesajda geçer
    /// ("uyarı" / "rapor aboneliği").</summary>
    public static async Task DogrulaAsync(
        KdsDbContext context, ISatirKuraliCozucu cozucu, Guid raporId, string ne,
        Contracts.Identity.CalistiranKullanici? kosanKimlik, CancellationToken ct)
    {
        var r = await context.Raporlar.AsNoTracking()
            .Where(x => x.Id == raporId)
            .Select(x => new { x.HamSql, x.ModelJson })
            .FirstOrDefaultAsync(ct);
        if (r is null)
        {
            return;   // rapor yoksa koşum zaten kendi anlaşılır hatasını verir
        }

        var hamSql = r.HamSql is not null;
        var haritaId = hamSql ? (Guid?)null : HaritaCoz(r.ModelJson);
        if (!hamSql && haritaId is null)
        {
            return;   // model çözülemedi → koşum kendi hatasını verir
        }

        if (!await cozucu.VeriKuralliMiAsync(haritaId, ct))
        {
            return;   // veri kuralsız → kapı devrede değil
        }

        // Buradan sonrası: veri KURALLI. İki hâlde reddedilir.

        // (1) Kimlik yok. Anlık görüntüsü olmayan iş (kapı gelmeden önce kurulmuş eski kayıtlar)
        //     kimin adına koşacağını bilmez; boş kimlikle koşmak rol-kapsamlı kuralları sessizce
        //     düşürürdü. Çözümü kullanıcı için basit: işi bir kez kaydetmek görüntüyü tazeler.
        KimlikGerekli(ne, kosanKimlik);

        // (2) Ham SQL. Rapor modelden geçmediği için kural ENJEKTE EDİLEMEZ; kimlik bilinse bile
        //     uygulanacak yer yoktur. Kurala tabi olmayan kimlik için ise sorun yok — ekran
        //     yolundaki RunReportHandler de tam bu ayrımı yapıyor (tutarlılık bilinçli).
        if (hamSql && await cozucu.KuralaTabiMiAsync(kosanKimlik!, ct))   // KimlikGerekli null'da fırlattı
        {
            throw new SatirKuraliHatasi(
                $"Bu {ne} çalıştırılamaz: rapor elle yazılmış SQL ile kurulmuş, dolayısıyla satır-düzeyi " +
                "güvenlik kuralları uygulanamaz ve sahibi bu kurallara tabi. Raporu tasarımcıyla " +
                "yeniden kurun (kural o zaman uygulanabilir) ya da kurala tabi olmayan bir sahiple kurun.");
        }
    }

    /// <summary>Harita üstünden aynı kapı (S30 · metriğe bağlı uyarı): metrik uyarısında rapor
    /// yoktur, koşulacak model doğrudan metriğin haritasında kurulur — kurallıysa kimlik şarttır.
    /// Ham SQL hâli burada yoktur: metrik modeli her zaman üreticiden geçer, kural enjekte edilir.</summary>
    public static async Task DogrulaHaritaAsync(
        ISatirKuraliCozucu cozucu, Guid haritaId, string ne,
        Contracts.Identity.CalistiranKullanici? kosanKimlik, CancellationToken ct)
    {
        if (await cozucu.VeriKuralliMiAsync(haritaId, ct))
        {
            KimlikGerekli(ne, kosanKimlik);
        }
    }

    private static void KimlikGerekli(string ne, Contracts.Identity.CalistiranKullanici? kosanKimlik)
    {
        if (kosanKimlik is null)
        {
            throw new SatirKuraliHatasi(
                $"Bu {ne} çalıştırılamaz: dayandığı veride satır-düzeyi güvenlik kuralı var ama işin " +
                "sahip kimliği kayıtlı değil (kimlik saklanmadan önce kurulmuş). Kimlik olmadan kural " +
                // Ek, kelimeye göre değişir ("uyarıyı" ama "aboneliğini") — şablonla ek üretmek
                // "aboneliğiyı" gibi kırık metin doğuruyordu (kullanıcı ekranında görüldü); eksiz kuruldu.
                $"uygulanamaz ve kısıtlı satırlar dışarı çıkardı. İlgili {ne} kaydını açıp bir kez " +
                "kaydedin — sahip kimliği tazelenir ve iş yeniden çalışır.");
        }
    }

    /// <summary>Model JSON'undan harita kimliğini okur (dar-tipli parse — AGENTS §4.6 izinli bölge).
    /// Tam modeli çözmek yerine yalnız ihtiyaç duyulan alan alınır.</summary>
    private static Guid? HaritaCoz(string? modelJson)
    {
        if (string.IsNullOrWhiteSpace(modelJson))
        {
            return null;
        }

        try
        {
            using var belge = JsonDocument.Parse(modelJson);
            return belge.RootElement.TryGetProperty("haritaId", out var h) && h.TryGetGuid(out var id)
                ? id
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
