using System.Text.Json;
using Kds.Business.Identity;
using Kds.Contracts.Reports;
using Kds.Data;
using Kds.Data.Entities;
using Kds.Export;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Subscriptions;

/// <summary>Rapor aboneliği ortak parçaları: alıcı e-posta listesi JSON dönüşümü (dar-tipli parse —
/// AGENTS §4.6 izinli bölge), biçim string'ini <see cref="ExportFormat"/>'a çözme ve
/// <b>sahiplik kapısı</b>.</summary>
internal static class SubscriptionCommon
{
    /// <summary>Müşteri yalnız KENDİ aboneliklerini görür; admin/geliştirici kiracının tamamını.
    /// (19 Ağu 2026 — abonelikler müşteri rolüne açıldı; kapı erişimi açmaz, DARALTIR.)</summary>
    public static IQueryable<ReportSubscription> Gorunur(
        IQueryable<ReportSubscription> kaynak, IUserContext kb, Guid? benimId) =>
        TumunuGorurMu(kb) ? kaynak : kaynak.Where(a => a.SahipId == benimId);

    public static bool TumunuGorurMu(IUserContext kb) => kb.IsAdmin || kb.CanDesign;

    /// <summary>Tek kayda erişim: yoksa 404, başkasınınsa yine <b>404</b> — "var ama senin değil"
    /// bilgisi bile başkasının aboneliğinin varlığını sızdırır.</summary>
    public static async Task<ReportSubscription> ErisilebilirAsync(
        KdsDbContext context, IUserContext kb, Guid? benimId, Guid id, CancellationToken ct) =>
        await Gorunur(context.Abonelikler, kb, benimId).FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException("Abonelik bulunamadı.");

    public static Task<Guid?> BenimIdAsync(KdsDbContext context, IUserContext kb, CancellationToken ct) =>
        kb.KeycloakId is not Guid kid
            ? Task.FromResult<Guid?>(null)
            : context.Kullanicilar.Where(k => k.KeycloakId == kid).Select(k => (Guid?)k.Id).FirstOrDefaultAsync(ct);

    /// <summary>Abone olunacak rapora erişim var mı? Müşteri yalnız kendi Özel raporuna ya da
    /// Ekiple paylaşılana abone olabilir — aksi hâlde abonelik, göremediği bir raporun içeriğini
    /// e-postayla kendine yollamanın yolu olurdu.</summary>
    public static async Task RaporErisimDogrulaAsync(
        KdsDbContext context, IUserContext kb, Guid? benimId, Guid raporId,
        Security.ISatirKuraliCozucu satirKuralCozucu, CancellationToken ct)
    {
        var rapor = await context.Raporlar.AsNoTracking()
            .Where(r => r.Id == raporId)
            .Select(r => new { r.Paylasim, r.SahipId })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Abone olunacak rapor bulunamadı.");

        if (!TumunuGorurMu(kb) && rapor.Paylasim != ReportSharing.Ekip && rapor.SahipId != benimId)
        {
            throw new KeyNotFoundException("Abone olunacak rapor bulunamadı.");
        }

        // Erken uyarı; güvenliği sağlayan kapı KOŞUM tarafında (ZamanlanmisIsKapisi).
        await Security.ZamanlanmisIsKapisi.DogrulaAsync(context, satirKuralCozucu, raporId, "rapor aboneliği",
            new Contracts.Identity.CalistiranKullanici(kb.Roller, kb.KullaniciAdi, kb.EPosta, kb.Iddialar), ct);
    }

    public static IReadOnlyList<string> AlicilariCoz(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    public static string AlicilariYaz(IEnumerable<string> alicilar) =>
        JsonSerializer.Serialize(alicilar.Select(a => a.Trim()).Where(a => a.Length > 0).ToList());

    /// <summary>"Pdf"/"Xlsx"/"Csv" → <see cref="ExportFormat"/>; tanınmazsa PDF (validator zaten süzer).</summary>
    public static ExportFormat BicimCoz(string bicim) =>
        Enum.TryParse<ExportFormat>(bicim, ignoreCase: true, out var f) ? f : ExportFormat.Pdf;

    /// <summary>Parametre değerleri JSON dönüşümü (M24 — dar-tipli parse; bozuksa null).</summary>
    public static IReadOnlyDictionary<string, object?>? ParametreCoz(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<Dictionary<string, object?>>(json); }
        catch (JsonException) { return null; }
    }

    public static string? ParametreYaz(IReadOnlyDictionary<string, object?>? degerler) =>
        degerler is { Count: > 0 } ? JsonSerializer.Serialize(degerler) : null;
}
