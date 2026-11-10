using Kds.Contracts.Identity;
using Kds.Contracts.QueryModel;
using Kds.Data;
using Kds.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Security;

/// <summary>Satır-düzeyi güvenlik (RLS) çözücüsü: bir veri haritası için, çalıştıran kullanıcıya
/// UYGULANACAK kuralları bulur ve üreticinin anlayacağı <see cref="RowFilter"/> listesine çevirir.
/// <para><b>Fail-closed:</b> kural uygulanacakken değeri çözülemiyorsa (beklenen iddia token'da
/// yok, kuralda değer boş…) liste döndürmez — HATA fırlatır. Sessizce filtresiz koşmak, veri
/// sızdırmanın en sinsi biçimidir: kullanıcı her şeyi görür ve kimse fark etmez.</para>
/// <para><b>Admin muaf:</b> yöneticinin veriyi bütün görmesi gerekir (kuralları da o tanımlar).</para></summary>
public interface ISatirKuraliCozucu
{
    Task<IReadOnlyList<RowFilter>> CozAsync(Guid veriHaritasiId, CalistiranKullanici kullanici, CancellationToken ct);

    /// <summary>Kullanıcı bu haritada herhangi bir kurala tabi mi? Ham SQL kapısı bunu sorar:
    /// RLS'e tabi kullanıcı, modelden geçmeyen (dolayısıyla kural uygulanamayan) ham SQL
    /// çalıştıramaz — açık yasak, sessiz sızıntıya yeğdir.</summary>
    Task<bool> KuralaTabiMiAsync(CalistiranKullanici kullanici, CancellationToken ct);

    /// <summary>Bu <b>VERİ</b> kurala tabi mi? Kullanıcıdan bağımsız: <paramref name="veriHaritasiId"/>
    /// için aktif kural var mı; null ise kiracıda herhangi bir aktif kural var mı (ham SQL için —
    /// hangi haritaya dokunduğu bilinemez).
    ///
    /// <para><b>Neden kimlikten bağımsız bir soru gerekli:</b> zamanlanmış işler (abonelik/uyarı)
    /// arka planda <b>kullanıcı bağlamı olmadan</b> koşar. Orada "bu kullanıcı kurala tabi mi?"
    /// sorusu yanıltıcıdır — boş rol/iddia görülür, rol-kapsamlı kurallar eşleşmez ve sessizce
    /// düşer. Sorulabilecek tek dürüst soru şudur: <i>bu verinin kuralı var mı?</i> Varsa ve kimliği
    /// taşıyamıyorsak iş çalıştırılmaz (fail-closed).</para></summary>
    Task<bool> VeriKuralliMiAsync(Guid? veriHaritasiId, CancellationToken ct);
}

public sealed class SatirKuraliCozucu(KdsDbContext db) : ISatirKuraliCozucu
{
    public async Task<IReadOnlyList<RowFilter>> CozAsync(
        Guid veriHaritasiId, CalistiranKullanici kullanici, CancellationToken ct)
    {
        if (Muaf(kullanici)) return [];

        var kurallar = await db.SatirKurallari.AsNoTracking()
            .Where(k => k.VeriHaritasiId == veriHaritasiId && k.Aktif)
            .ToListAsync(ct);

        var sonuc = new List<RowFilter>();
        foreach (var k in kurallar.Where(k => RoleUygulanir(k, kullanici)))
            sonuc.Add(new RowFilter(k.HaritaTabloId, k.Kolon, k.Op, Degerler(k, kullanici), k.Ad));
        return sonuc;
    }

    public async Task<bool> KuralaTabiMiAsync(CalistiranKullanici kullanici, CancellationToken ct)
    {
        if (Muaf(kullanici)) return false;
        var kurallar = await db.SatirKurallari.AsNoTracking().Where(k => k.Aktif).ToListAsync(ct);
        return kurallar.Any(k => RoleUygulanir(k, kullanici));
    }

    public Task<bool> VeriKuralliMiAsync(Guid? veriHaritasiId, CancellationToken ct) =>
        db.SatirKurallari.AsNoTracking()
            .AnyAsync(k => k.Aktif && (veriHaritasiId == null || k.VeriHaritasiId == veriHaritasiId), ct);

    private static bool Muaf(CalistiranKullanici kullanici) =>
        kullanici.Roller.Contains(Roles.Admin, StringComparer.OrdinalIgnoreCase);

    /// <summary>Roller boşsa kural (admin dışında) herkese; doluysa yalnız listedekilere uygulanır.</summary>
    private static bool RoleUygulanir(RowRule k, CalistiranKullanici kullanici)
    {
        if (string.IsNullOrWhiteSpace(k.Roller)) return true;
        var hedef = k.Roller.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return hedef.Any(r => kullanici.Roller.Contains(r, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Kuralın karşılaştırma değerini çözer. Çözülemiyorsa AÇIKÇA patlar (fail-closed):
    /// mesaj hangi kuralın neden çalışmadığını söyler ki yönetici düzeltebilsin.</summary>
    private static IReadOnlyList<object?> Degerler(RowRule k, CalistiranKullanici kullanici)
    {
        switch (k.DegerKaynagi)
        {
            case KuralDegerKaynagi.SabitDeger:
                var sabit = (k.Deger ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (sabit.Length == 0)
                    throw new SatirKuraliHatasi($"'{k.Ad}' kuralında değer boş — kuralı düzeltin ya da pasife alın.");
                return sabit;

            case KuralDegerKaynagi.TokenIddiasi:
                var iddiaAdi = (k.Deger ?? "").Trim();
                if (iddiaAdi.Length == 0)
                    throw new SatirKuraliHatasi($"'{k.Ad}' kuralında iddia (claim) adı boş — kuralı düzeltin.");
                if (!kullanici.Iddialar.TryGetValue(iddiaAdi, out var deger) || string.IsNullOrWhiteSpace(deger))
                    throw new SatirKuraliHatasi(
                        $"'{k.Ad}' kuralı '{iddiaAdi}' bilgisini istiyor ama kullanıcının kimliğinde bu bilgi yok. " +
                        "Sorgu ÇALIŞTIRILMADI (filtresiz çalıştırmak tüm satırları açardı). " +
                        "Kimlik sağlayıcıda bu niteliği tanımlayın ya da kuralı düzeltin.");
                // Çok değerli iddia (ör. "12,34") birden çok değere açılır.
                return deger.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            case KuralDegerKaynagi.KullaniciAdi:
                return [Zorunlu(kullanici.KullaniciAdi, k, "kullanıcı adı")];

            case KuralDegerKaynagi.EPosta:
                return [Zorunlu(kullanici.EPosta, k, "e-posta")];

            default:
                throw new SatirKuraliHatasi($"'{k.Ad}' kuralında bilinmeyen değer kaynağı.");
        }
    }

    private static string Zorunlu(string? deger, RowRule k, string ne) =>
        string.IsNullOrWhiteSpace(deger)
            ? throw new SatirKuraliHatasi(
                $"'{k.Ad}' kuralı kullanıcının {ne} bilgisini istiyor ama kimlikte yok. Sorgu çalıştırılmadı.")
            : deger;
}

/// <summary>RLS kuralı uygulanamadı — sorgu ÇALIŞTIRILMAZ. (API bunu 400'e çevirir; mesaj
/// yöneticinin düzeltebileceği kadar açıktır ama veri sızdırmaz.)</summary>
public sealed class SatirKuraliHatasi(string mesaj) : Exception(mesaj);
