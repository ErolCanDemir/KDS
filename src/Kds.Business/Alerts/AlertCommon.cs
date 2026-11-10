using System.Text.Json;
using Kds.Business.Identity;
using Kds.Contracts.Alerts;
using Kds.Contracts.Reports;
using Kds.Data;
using Kds.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Alerts;

/// <summary>Uyarı handler'larının ortak parçaları: JSON dönüşümleri, enum çözümü ve
/// <b>sahiplik kapısı</b>.</summary>
internal static class AlertCommon
{
    public static IReadOnlyList<string> AlicilariCoz(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    public static string AlicilariYaz(IEnumerable<string> alicilar) =>
        JsonSerializer.Serialize(alicilar.Select(a => a.Trim()).Where(a => a.Length > 0).ToList());

    public static IReadOnlyDictionary<string, object?>? ParametreCoz(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<Dictionary<string, object?>>(json); }
        catch (JsonException) { return null; }
    }

    public static string? ParametreYaz(IReadOnlyDictionary<string, object?>? degerler) =>
        degerler is { Count: > 0 } ? JsonSerializer.Serialize(degerler) : null;

    public static OzetTuru OzetCoz(string s) => Enum.TryParse<OzetTuru>(s, true, out var o) ? o : OzetTuru.SatirSayisi;

    public static UyariOp OpCoz(string s) => Enum.TryParse<UyariOp>(s, true, out var o) ? o : UyariOp.Buyuk;

    public static UyariDurumu DurumCoz(Alert u) => u.SonDurum switch
    {
        "Hata" => UyariDurumu.Hata,
        "Tetiklendi" => UyariDurumu.Tetiklendi,
        "Sakin" => UyariDurumu.Sakin,
        _ => UyariDurumu.HicKosmadi,
    };

    /// <summary>
    /// Sahiplik kapısı — <b>müşteri yalnız KENDİ uyarılarını</b> görür/yönetir; admin ve geliştirici
    /// kiracının tamamını görür.
    /// <para>Neden: Uyarılar ve Zamanlanmış Raporlar 19 Ağu 2026'da kullanıcı kararıyla müşteri
    /// rolüne de açıldı ("müşteriye de gösterelim"). Rol kapısını tamamen kaldırmak, bir müşterinin
    /// başkasının uyarısını silmesine/alıcı listesini görmesine izin verirdi; kapı role göre
    /// DARALTMA yapar, erişimi tümden açmaz.</para>
    /// </summary>
    public static IQueryable<Alert> Gorunur(IQueryable<Alert> kaynak, IUserContext kb, Guid? benimId) =>
        TumunuGorurMu(kb) ? kaynak : kaynak.Where(u => u.SahipId == benimId);

    /// <summary>Admin/geliştirici kiracının tamamını yönetir; müşteri yalnız kendisininkini.</summary>
    public static bool TumunuGorurMu(IUserContext kb) => kb.IsAdmin || kb.CanDesign;

    /// <summary>Tek kayda erişim kapısı: yoksa 404, başkasınınsa yine <b>404</b> (403 değil) —
    /// "var ama senin değil" bilgisi bile başkasının uyarısının varlığını sızdırır.</summary>
    public static async Task<Alert> ErisilebilirAsync(
        KdsDbContext context, IUserContext kb, Guid? benimId, Guid id, CancellationToken ct)
    {
        var uyari = await Gorunur(context.Uyarilar, kb, benimId).FirstOrDefaultAsync(u => u.Id == id, ct);
        return uyari ?? throw new KeyNotFoundException("Uyarı bulunamadı.");
    }

    /// <summary>Token'daki kullanıcının local <c>User.Id</c>'si (rapor tarafındaki desenle aynı).</summary>
    public static Task<Guid?> BenimIdAsync(KdsDbContext context, IUserContext kb, CancellationToken ct) =>
        kb.KeycloakId is not Guid kid
            ? Task.FromResult<Guid?>(null)
            : context.Kullanicilar.Where(k => k.KeycloakId == kid).Select(k => (Guid?)k.Id).FirstOrDefaultAsync(ct);

    /// <summary>Uyarının hedef raporuna kullanıcının erişimi var mı? Müşteri yalnız KENDİ Özel
    /// raporunu ya da Ekiple paylaşılanı izleyebilir — aksi hâlde uyarı, göremediği bir raporun
    /// değerini sızdıran bir yan kanal olurdu ("eşik aşıldı" bilgisi de veridir).</summary>
    public static async Task RaporErisimDogrulaAsync(
        KdsDbContext context, IUserContext kb, Guid? benimId, Guid raporId,
        Security.ISatirKuraliCozucu satirKuralCozucu, CancellationToken ct)
    {
        var rapor = await context.Raporlar.AsNoTracking()
            .Where(r => r.Id == raporId)
            .Select(r => new { r.Paylasim, r.SahipId })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Uyarının izleyeceği rapor bulunamadı.");

        if (!TumunuGorurMu(kb) && rapor.Paylasim != ReportSharing.Ekip && rapor.SahipId != benimId)
        {
            throw new KeyNotFoundException("Uyarının izleyeceği rapor bulunamadı.");
        }

        // Erken uyarı: kuralı olan veriye uyarı kurulamaz. Güvenliği sağlayan kapı KOŞUM
        // tarafındadır (ZamanlanmisIsKapisi); buradaki çağrı kullanıcıya hatayı kaydetme anında söyler.
        await Security.ZamanlanmisIsKapisi.DogrulaAsync(context, satirKuralCozucu, raporId, "uyarı",
            new Contracts.Identity.CalistiranKullanici(kb.Roller, kb.KullaniciAdi, kb.EPosta, kb.Iddialar), ct);
    }

    /// <summary>S30 — metrik uyarısının erişim + erken RLS kontrolü. Metrik kataloğu kiracı içinde
    /// herkese açıktır (sahiplik kapısı yok); ölçüm sahibin kimliğiyle RLS'ten geçer. Buradaki iş:
    /// metrik gerçekten var mı + haritası kurallıysa kaydeden kişiye erken haber ver.</summary>
    public static async Task MetrikErisimDogrulaAsync(
        KdsDbContext context, IUserContext kb, Guid metrikId,
        Security.ISatirKuraliCozucu satirKuralCozucu, CancellationToken ct)
    {
        var haritaId = await context.Metrikler.AsNoTracking()
            .Where(m => m.Id == metrikId)
            .Select(m => (Guid?)m.VeriHaritasiId)
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Uyarının izleyeceği metrik bulunamadı.");

        await Security.ZamanlanmisIsKapisi.DogrulaHaritaAsync(satirKuralCozucu, haritaId, "uyarı",
            new Contracts.Identity.CalistiranKullanici(kb.Roller, kb.KullaniciAdi, kb.EPosta, kb.Iddialar), ct);
    }
}
