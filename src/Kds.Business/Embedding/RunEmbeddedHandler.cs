using System.Text.Json;
using Kds.Contracts.Embedding;
using Kds.Contracts.Identity;
using Kds.Contracts.QueryModel;
using Kds.Contracts.Queries;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Embedding;

/// <summary>S42 — GÖMÜLÜ KOŞUM. Dış siteden gelen, OTURUMSUZ istek. Güvenliğin tamamı burada
/// toplanır; sıra bilinçlidir ve her adım bir saldırı yolunu kapatır:
///
/// <list type="number">
/// <item><b>İmza:</b> anahtar çözülemiyorsa (kurcalanmış / başka halka) hemen düşer.</item>
/// <item><b>Kayıt:</b> yetki anahtarın İÇİNDE değil, DB satırındadır — iptal anında etkilidir.</item>
/// <item><b>İptal / süre:</b> ikisi de ayrı ayrı bakılır.</item>
/// <item><b>Kaynak (Origin):</b> anahtar doğru olsa bile izinsiz siteden çalışmaz — çalınan bir
///   anahtar saldırganın kendi sayfasında işe yaramaz.</item>
/// <item><b>Kiracı bağlama:</b> bundan SONRAKİ her sorgu o kiracının verisini görür.</item>
/// <item><b>Kimlik + RLS:</b> koşum anahtarın taşıdığı kimlikle yapılır; satır kuralları uygulanır.</item>
/// </list>
///
/// <para>Hata mesajları BİLEREK ayrımsızdır ("geçersiz ya da süresi dolmuş"): saldırgana anahtarın
/// var olup olmadığını, hangi aşamada düştüğünü söylemek numaralandırma saldırısına yardım eder.
/// Yönetim ekranı zaten gerçek durumu (iptal / süresi dolmuş) gösteriyor.</para></summary>
public sealed class RunEmbeddedHandler(
    KdsDbContext context, IGommeAnahtarKoruyucu koruyucu, IKiraciKapsami kiraciKapsami, ISender gonderici)
    : IRequestHandler<RunEmbeddedRequest, EmbeddedResult>
{
    public async Task<EmbeddedResult> Handle(RunEmbeddedRequest request, CancellationToken cancellationToken)
    {
        var anahtarId = koruyucu.Coz(request.Anahtar) ?? throw new GommeReddi();

        /* Kiracı HENÜZ bilinmiyor (oturum yok) — kayıt bu yüzden query filter'sız okunur. Okunan
         * TEK şey anahtarın kendi satırıdır; kiracı öğrenilir öğrenilmez kapsam bağlanır ve
         * sonraki bütün sorgular normal izolasyonla koşar. */
        var anahtar = await context.GommeAnahtarlari.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == anahtarId, cancellationToken)
            ?? throw new GommeReddi();

        if (anahtar.IptalTarihi is not null || anahtar.GecerlilikBitis < DateTimeOffset.UtcNow)
        {
            throw new GommeReddi();
        }

        if (!KaynakIzinli(anahtar.IzinliAlanlar, request.Kaynak))
        {
            throw new GommeReddi();
        }

        using var _ = kiraciKapsami.Baglan(anahtar.TenantId);

        var rapor = await context.Raporlar.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == anahtar.RaporId && r.TenantId == anahtar.TenantId, cancellationToken)
            ?? throw new GommeReddi();

        // Anahtar kurulurken ham SQL raporu reddedilir; rapor SONRADAN ham SQL'e çevrilmişse burada
        // da durur — RLS uygulanamayan içerik dışarıya asla açılmaz.
        var model = rapor.ModelJson is null
            ? throw new GommeReddi()
            : JsonSerializer.Deserialize<QueryModel>(rapor.ModelJson, Reports.ReportCommon.JsonAyar)
              ?? throw new GommeReddi();

        var kimlik = JsonSerializer.Deserialize<CalistiranKullanici>(anahtar.KimlikJson)
            ?? throw new GommeReddi();

        var sonuc = await gonderici.Send(
            new RunQueryModelRequest(model, ParametreDegerleri: null, RaporId: rapor.Id, KosanKimlik: kimlik),
            cancellationToken);

        // Son kullanım damgası: "bu anahtar hâlâ kullanılıyor mu?" sorusunun cevabı. Koşum
        // BAŞARILIYSA yazılır — reddedilen denemeler burayı tazelemez.
        await context.GommeAnahtarlari.Where(a => a.Id == anahtar.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.SonKullanim, DateTimeOffset.UtcNow), cancellationToken);

        var kiraci = await context.Tenantlar.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == anahtar.TenantId, cancellationToken);

        return new EmbeddedResult(
            rapor.Ad,
            "Rapor",
            rapor.Gorsellestirme.ToString(),
            sonuc,
            new EmbedMarka(kiraci?.Ad ?? "LST-KDS", kiraci?.MarkaRengi, kiraci?.LogoDataUri));
    }

    /// <summary>Kaynak, izinli alan adlarından biriyle eşleşiyor mu? Karşılaştırma HOST üzerinden
    /// yapılır: "https://portal.firma.com/sayfa" → "portal.firma.com". Alt alan adı joker'i
    /// bilerek YOK — "*.firma.com" yazan bir kural, ele geçirilmiş tek bir alt alan adının
    /// anahtarı kullanmasına izin verirdi.</summary>
    private static bool KaynakIzinli(string izinliAlanlar, string? kaynak)
    {
        var izinliler = izinliAlanlar.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (izinliler.Length == 0 || string.IsNullOrWhiteSpace(kaynak))
        {
            // İzinli liste boşsa hiçbir yerden çalışmaz; kaynağı olmayan istek de (doğrudan çağrı)
            // reddedilir — güvenli varsayılan "kapalı".
            return false;
        }

        var host = Uri.TryCreate(kaynak, UriKind.Absolute, out var u) ? u.Host : kaynak.Trim();
        return izinliler.Any(a => string.Equals(a, host, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Gömülü isteğin reddi. Sebep BİLEREK taşınmaz: çağıran tek bir ayrımsız mesaj görür,
/// böylece anahtarın var olup olmadığı / hangi aşamada düştüğü dışarı sızmaz.</summary>
public sealed class GommeReddi() : Exception("Bu gömme anahtarı geçersiz, süresi dolmuş ya da bu adresten kullanılamıyor.");
