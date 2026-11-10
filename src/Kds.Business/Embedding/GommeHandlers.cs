using System.Text.Json;
using FluentValidation;
using Kds.Business.Identity;
using Kds.Contracts.Embedding;
using Kds.Contracts.Identity;
using Kds.Contracts.QueryModel;
using Kds.Contracts.Queries;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Embedding;

/// <summary>S42 — gömme anahtarının kurulması. Kimliğin rol/iddia ANLIK GÖRÜNTÜSÜ burada dondurulur
/// (abonelik/uyarı işlerindeki desen): gömülü koşum Keycloak'a gidip güncel rolleri soramaz.
/// Bedeli bilinçli — kullanıcının rolü sonradan değişirse anahtar eski kimlikle koşar; bu yüzden
/// yönetim ekranı anahtarı iptal edip yenisini üretmeyi kolaylaştırır.</summary>
public sealed class CreateEmbedTokenHandler(
    KdsDbContext context, IGommeAnahtarKoruyucu koruyucu, IUserContext kb)
    : IRequestHandler<CreateEmbedTokenRequest, CreateEmbedTokenResult>
{
    public async Task<CreateEmbedTokenResult> Handle(CreateEmbedTokenRequest request, CancellationToken cancellationToken)
    {
        if (request.PanoId is not null)
        {
            /* v1 KAPSAM: yalnız RAPOR gömülür. Pano gömmek her kartı ayrı koşturmak demek; sonucun
             * biçimi de (tek tablo değil, N sonuç) farklı. Yarım çalışan bir pano gömmesi
             * sunmaktansa açıkça reddetmek doğru — menü/ekran da rapor diyor. */
            throw new ValidationException("Şimdilik yalnız RAPOR gömülebilir; pano gömme sonraki dilimde.");
        }

        var rapor = await context.Raporlar.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RaporId, cancellationToken)
            ?? throw new KeyNotFoundException("Gömülecek rapor bulunamadı.");

        /* HAM SQL RAPORU GÖMÜLEMEZ. Satır güvenliği yalnız tasarımcıyla kurulmuş modellere
         * uygulanabiliyor (RunReportHandler aynı sebeple kurala tabi kullanıcıyı reddediyor).
         * Gömme DIŞARIYA açılan bir yüzeydir: RLS'in uygulanamadığı içeriği oraya koymak,
         * kuralları sessizce delmenin en kolay yolu olurdu. */
        if (rapor.ModelJson is null)
        {
            throw new ValidationException(
                "Bu rapor elle yazılmış SQL ile kurulmuş ve gömülemez: satır-düzeyi güvenlik yalnız " +
                "tasarımcıyla kurulmuş raporlara uygulanabilir. Raporu tasarımcıyla yeniden kurun.");
        }

        var hedefKullanici = await context.Kullanicilar.AsNoTracking()
            .FirstOrDefaultAsync(u => u.KeycloakId == request.KosanKeycloakId, cancellationToken)
            ?? throw new KeyNotFoundException("Anahtarın koşacağı kullanıcı bulunamadı.");

        /* KİMLİK ANLIK GÖRÜNTÜSÜ — gömülü koşumun RLS'i buna göre uygulanır.
         *
         * Roller İSTEKTEN gelir, kullanıcı kaydından değil: roller Keycloak'ta yaşıyor, yerel
         * User satırında yok. Admin'in rolleri açıkça seçmesi ayrıca DAHA GÜVENLİ — "bu anahtar
         * hangi yetkiyle koşuyor" sorusu, kurulum anında gözle görülür bir karara dönüşür.
         * Kullanıcı adı ve e-posta ise kayıttan alınır ("yalnız kendi kayıtları" tipi kurallar
         * bunlara bakar). */
        var kimlik = new CalistiranKullanici(
            [.. request.Roller.Select(r => r.Trim()).Where(r => r.Length > 0)],
            hedefKullanici.KullaniciAdi,
            hedefKullanici.EPosta,
            new Dictionary<string, string>());

        var anahtar = new EmbedToken
        {
            Ad = request.Ad.Trim(),
            RaporId = rapor.Id,
            KimlikJson = JsonSerializer.Serialize(kimlik),
            IzinliAlanlar = string.Join(",", request.IzinliAlanlar.Select(a => a.Trim().ToLowerInvariant())),
            GecerlilikBitis = DateTimeOffset.UtcNow.AddDays(Math.Clamp(request.GecerlilikGun, 1, 3650)),
            OlusturanId = await Reports.ReportCommon.MyIdAsync(context, kb, cancellationToken) ?? Guid.Empty,
        };

        context.GommeAnahtarlari.Add(anahtar);
        await context.SaveChangesAsync(cancellationToken);

        /* Anahtar YALNIZ BURADA döner ve saklanmaz: listede bir daha gösterilmez. Saklanan bir sır,
         * sızabilecek ikinci bir kopyadır (parola deseni). Kaybedilirse yenisi üretilir. */
        var uretilen = koruyucu.Uret(anahtar.Id);
        return new CreateEmbedTokenResult(anahtar.Id, uretilen, GomKodu(uretilen));
    }

    /// <summary>Kopyalanabilir iframe parçacığı. Adres yer tutucu kalır ({KDS_ADRESI}) — sunucu
    /// kendi dış adresini güvenilir biçimde bilemez (ters vekil arkasında Host başlığı
    /// değişebilir); ekran bunu tarayıcıdaki gerçek adresle doldurur.</summary>
    private static string GomKodu(string anahtar) =>
        "<iframe src=\"{KDS_ADRESI}/gomulu?anahtar=" + Uri.EscapeDataString(anahtar) +
        "\" width=\"100%\" height=\"480\" frameborder=\"0\"></iframe>";
}

public sealed class CreateEmbedTokenValidation : AbstractValidator<CreateEmbedTokenRequest>
{
    public CreateEmbedTokenValidation()
    {
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(200)
            .WithMessage("Anahtara tanınabilir bir ad verin (ör. \"Bayi portalı — ciro kartı\").");
        RuleFor(x => x.KosanKeycloakId).NotEmpty()
            .WithMessage("Gömülü koşumun kimin adına yapılacağı seçilmeli — satır güvenliği buna göre uygulanır.");
        RuleFor(x => x.RaporId).NotEmpty().When(x => x.PanoId is null)
            .WithMessage("Gömülecek rapor seçilmeli.");
        /* İzinli alan BOŞ BIRAKILAMAZ: güvenli varsayılan "kapalı"dır. "Her yerden gömülebilir"
         * seçeneği bilerek yok — böyle bir seçenek, sızan anahtarı sınırsız kullanılabilir yapardı. */
        RuleFor(x => x.IzinliAlanlar).NotEmpty()
            .WithMessage("En az bir izinli alan adı girin — anahtar yalnız o adreslerden çalışır.");
        RuleForEach(x => x.IzinliAlanlar)
            .Must(a => !string.IsNullOrWhiteSpace(a) && !a.Contains('/') && !a.Contains(' '))
            .WithMessage("Alan adı yalnız ana bilgisayar adı olmalı (ör. portal.firma.com) — yol ya da boşluk içeremez.");
        RuleFor(x => x.GecerlilikGun).InclusiveBetween(1, 3650);

        /* ROL ZORUNLU: satır kuralları "hangi role uygulanır" diye bakar ve ROL TAŞIMAYAN bir
         * kimlik, role bağlı kuralları ATLAR — yani anahtar beklenenden geniş veri görür.
         * Rolsüz gömme, RLS'i sessizce delmenin en kolay yoludur. */
        RuleFor(x => x.Roller).NotEmpty()
            .WithMessage("Anahtarın hangi rollerle koşacağı seçilmeli — satır güvenliği kuralları " +
                         "role bakar ve rolsüz bir anahtar o kuralları atlar.");

        /* ADMIN YASAK: admin RLS'ten MUAFTIR (SatirKuraliCozucu.Muaf). Admin rolü taşıyan bir
         * gömme anahtarı bütün satır kurallarını devre dışı bırakır ve dış siteye kısıtsız veri
         * açardı — bu, gömmenin tüm güvenlik modelini boşa çıkarır. */
        RuleFor(x => x.Roller)
            .Must(r => !r.Any(x => string.Equals(x.Trim(), "admin", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Gömme anahtarına admin rolü verilemez: admin satır güvenliğinden muaftır, " +
                         "yani anahtar bütün kuralları devre dışı bırakır.");
    }
}

public sealed class ListEmbedTokensHandler(KdsDbContext context)
    : IRequestHandler<ListEmbedTokensRequest, IReadOnlyList<EmbedTokenDto>>
{
    public async Task<IReadOnlyList<EmbedTokenDto>> Handle(ListEmbedTokensRequest request, CancellationToken cancellationToken)
    {
        var simdi = DateTimeOffset.UtcNow;
        var anahtarlar = await context.GommeAnahtarlari.AsNoTracking()
            .OrderByDescending(a => a.OlusturmaTarihi)
            .Select(a => new
            {
                a.Id, a.Ad, a.RaporId, a.PanoId, a.IzinliAlanlar, a.GecerlilikBitis, a.IptalTarihi, a.SonKullanim,
                RaporAd = context.Raporlar.Where(r => r.Id == a.RaporId).Select(r => r.Ad).FirstOrDefault(),
                a.KimlikJson,
            })
            .ToListAsync(cancellationToken);

        return [.. anahtarlar.Select(a => new EmbedTokenDto(
            a.Id,
            a.Ad,
            a.PanoId is not null ? "Pano" : "Rapor",
            a.RaporAd,
            KullaniciAdiCoz(a.KimlikJson),
            a.IzinliAlanlar.Split(',', StringSplitOptions.RemoveEmptyEntries),
            a.GecerlilikBitis,
            a.SonKullanim,
            a.IptalTarihi is not null ? "iptal"
                : a.GecerlilikBitis < simdi ? "suresi-dolmus"
                : "gecerli"))];
    }

    /// <summary>Kimlik anlık görüntüsünden yalnız görünen adı çıkarır — bozuk JSON ekranı
    /// düşürmesin diye sessizce boş döner (liste bir teşhis ekranı değil).</summary>
    private static string? KullaniciAdiCoz(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<CalistiranKullanici>(json)?.KullaniciAdi;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Anahtarı İPTAL eder — silmez. Kayıt kalır ki "hangi anahtar ne zaman kapatıldı"
/// denetlenebilsin; silmek, sızıntı sonrası soruşturmayı imkânsız kılardı.</summary>
public sealed class RevokeEmbedTokenHandler(KdsDbContext context)
    : IRequestHandler<RevokeEmbedTokenRequest, bool>
{
    public async Task<bool> Handle(RevokeEmbedTokenRequest request, CancellationToken cancellationToken)
    {
        var a = await context.GommeAnahtarlari.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (a is null || a.IptalTarihi is not null)
        {
            return false;
        }

        a.IptalTarihi = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
