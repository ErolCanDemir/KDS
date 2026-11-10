using System.Text.RegularExpressions;
using FluentValidation;
using Kds.Business.Settings;
using Kds.Contracts.Organization;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Organization;

/// <summary>S35 — Kuruluş Ayarları. Kiracı satırı zaten global query filter ile süzülüdür
/// (<c>t.Id == CurrentTenantId</c>), yani buradaki sorgular başka kiracının kaydına ERİŞEMEZ;
/// ayrıca elle bir kiracı koşulu yazılmaz (yazılsaydı filtreyi çift uygulamış olurduk).</summary>
public sealed class GetOrganizationHandler(KdsDbContext context, ISender gonderici)
    : IRequestHandler<GetOrganizationRequest, OrganizationResult>
{
    public async Task<OrganizationResult> Handle(GetOrganizationRequest request, CancellationToken cancellationToken)
    {
        var kiraci = await context.Tenantlar.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Kuruluş kaydı bulunamadı.");

        return new OrganizationResult(
            kiraci.Id, kiraci.Ad, kiraci.Durum.ToString(), kiraci.Plan,
            kiraci.MarkaRengi, kiraci.LogoDataUri, kiraci.OlusturmaTarihi,
            await LimitOzetiAsync(gonderici, cancellationToken));
    }

    /// <summary>Limitler burada TANIMLANMAZ, Sistem Ayarları'ndan OKUNUR — kaynak rozetiyle
    /// birlikte (kiracı/kurulum/varsayılan). Böylece ekran "bu kuruluş için yürürlükteki değer
    /// nedir" sorusunu cevaplar ama ikinci bir düzenleme yeri açmaz.</summary>
    internal static async Task<IReadOnlyList<LimitOzeti>> LimitOzetiAsync(ISender gonderici, CancellationToken ct)
    {
        var ayarlar = await gonderici.Send(new ListSettingsRequest(), ct);
        string[] ilgili =
        [
            AyarTanimlari.SorguZamanAsimiSn,
            AyarTanimlari.SorguVarsayilanSatir,
            AyarTanimlari.SorguEnCokSatir,
        ];

        return [.. ayarlar
            .Where(a => ilgili.Contains(a.Anahtar))
            .OrderBy(a => Array.IndexOf(ilgili, a.Anahtar))
            .Select(a => new LimitOzeti(a.Anahtar, a.Etiket, a.Deger, a.Kaynak))];
    }
}

/// <summary>Markayı uygulama kabuğuna veren dar okuma — TÜM roller çağırabilir (logo herkesin
/// ekranında görünmeli). Kiracı süzgeci yine query filter'dan gelir; kuruluş kaydı yoksa ürünün
/// kendi adıyla döner, çünkü kabuk her hâlükârda çizilmek zorundadır.</summary>
public sealed class GetBrandingHandler(KdsDbContext context)
    : IRequestHandler<GetBrandingRequest, BrandingResult>
{
    public async Task<BrandingResult> Handle(GetBrandingRequest request, CancellationToken cancellationToken)
    {
        var kiraci = await context.Tenantlar.AsNoTracking()
            .Select(t => new BrandingResult(t.Ad, t.MarkaRengi, t.LogoDataUri))
            .FirstOrDefaultAsync(cancellationToken);

        return kiraci ?? new BrandingResult("LST-KDS", null, null);
    }
}

public sealed class UpdateOrganizationHandler(KdsDbContext context, ISender gonderici)
    : IRequestHandler<UpdateOrganizationRequest, OrganizationResult>
{
    public async Task<OrganizationResult> Handle(UpdateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var kiraci = await context.Tenantlar.FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Kuruluş kaydı bulunamadı.");

        kiraci.Ad = request.Ad.Trim();
        // Boş gelen marka alanı "temizle" demektir: ayrı bir "logoyu kaldır" komutu gerekmesin.
        kiraci.Plan = Bosalt(request.Plan);
        kiraci.MarkaRengi = Bosalt(request.MarkaRengi);
        kiraci.LogoDataUri = Bosalt(request.LogoDataUri);
        await context.SaveChangesAsync(cancellationToken);

        return new OrganizationResult(
            kiraci.Id, kiraci.Ad, kiraci.Durum.ToString(), kiraci.Plan,
            kiraci.MarkaRengi, kiraci.LogoDataUri, kiraci.OlusturmaTarihi,
            await GetOrganizationHandler.LimitOzetiAsync(gonderici, cancellationToken));
    }

    private static string? Bosalt(string? deger) =>
        string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}

public sealed partial class UpdateOrganizationValidation : AbstractValidator<UpdateOrganizationRequest>
{
    /// <summary>Logo <c>data:</c> URI olarak SATIR İÇİNDE saklanır; sınır kolon boyutuyla (400 KB)
    /// uyumlu tutulur. Base64 ham dosyayı ~%33 büyüttüğü için 300 KB'lık sınır yaklaşık 220 KB'lık
    /// bir görsele denk gelir — logo için fazlasıyla yeterli, veritabanını şişirmeye yetmez.</summary>
    private const int LogoEnCokKarakter = 300_000;

    public UpdateOrganizationValidation()
    {
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(200)
            .WithMessage("Kuruluş adı boş olamaz ve 200 karakteri aşamaz.");
        RuleFor(x => x.Plan).MaximumLength(100);

        RuleFor(x => x.MarkaRengi)
            .Must(r => string.IsNullOrWhiteSpace(r) || RenkDeseni().IsMatch(r))
            .WithMessage("Marka rengi #RRGGBB biçiminde olmalı (ör. #0D8B8B).");

        RuleFor(x => x.LogoDataUri)
            .Must(l => string.IsNullOrWhiteSpace(l) || l.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Logo bir görsel dosyası olmalı.")
            .Must(l => string.IsNullOrWhiteSpace(l) || l.Length <= LogoEnCokKarakter)
            .WithMessage("Logo çok büyük — yaklaşık 220 KB'ın altında bir görsel seçin.");
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex RenkDeseni();
}
