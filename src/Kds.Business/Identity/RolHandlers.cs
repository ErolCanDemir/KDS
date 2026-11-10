using System.Text.RegularExpressions;
using FluentValidation;
using Kds.Contracts.Identity;
using Kds.Data;
using MediatR;

namespace Kds.Business.Identity;

/// <summary>S27 — rol yönetimi handler'ları: ince delege katmanı; tüm IO <see cref="IRolYonetimi"/>
/// adaptöründedir. Sistem rolü koruması BURADA (iş kuralı, adaptörün işi değil).</summary>
public sealed class ListRolesHandler(IRolYonetimi roller) : IRequestHandler<ListRolesRequest, IReadOnlyList<RolOzeti>>
{
    public Task<IReadOnlyList<RolOzeti>> Handle(ListRolesRequest request, CancellationToken ct) =>
        roller.RolleriListeleAsync(ct);
}

public sealed class CreateRoleHandler(IRolYonetimi roller) : IRequestHandler<CreateRoleRequest, RolOzeti>
{
    public Task<RolOzeti> Handle(CreateRoleRequest request, CancellationToken ct) =>
        roller.RolOlusturAsync(request.Ad.Trim(), request.Aciklama?.Trim(), ct);
}

public sealed class DeleteRoleHandler(IRolYonetimi roller) : IRequestHandler<DeleteRoleRequest, bool>
{
    public async Task<bool> Handle(DeleteRoleRequest request, CancellationToken ct)
    {
        if (RolKurallari.SistemRolu(request.Ad))
        {
            // İş kuralı reti (400) — RolYonetimHatasi (503) yalnız Keycloak arızaları içindir.
            throw new FluentValidation.ValidationException($"'{request.Ad}' ürünle gelen sistem rolüdür — silinemez.");
        }

        await roller.SilAsync(request.Ad, ct);
        return true;
    }
}

public sealed class ListRoleMembersHandler(IRolYonetimi roller) : IRequestHandler<ListRoleMembersRequest, IReadOnlyList<RolUyesi>>
{
    public Task<IReadOnlyList<RolUyesi>> Handle(ListRoleMembersRequest request, CancellationToken ct) =>
        roller.UyeleriListeleAsync(request.Ad, ct);
}

public sealed class AddRoleMemberHandler(IRolYonetimi roller) : IRequestHandler<AddRoleMemberRequest, bool>
{
    public async Task<bool> Handle(AddRoleMemberRequest request, CancellationToken ct)
    {
        await roller.UyeEkleAsync(request.RolAdi, request.KeycloakId, ct);
        return true;
    }
}

public sealed class RemoveRoleMemberHandler(IRolYonetimi roller) : IRequestHandler<RemoveRoleMemberRequest, bool>
{
    public async Task<bool> Handle(RemoveRoleMemberRequest request, CancellationToken ct)
    {
        await roller.UyeCikarAsync(request.RolAdi, request.KeycloakId, ct);
        return true;
    }
}

public sealed class SearchUsersHandler(IRolYonetimi roller, ITenantContext kiraci)
    : IRequestHandler<SearchUsersRequest, IReadOnlyList<RolUyesi>>
{
    public Task<IReadOnlyList<RolUyesi>> Handle(SearchUsersRequest request, CancellationToken ct) =>
        roller.KullaniciAraAsync(
            request.Arama?.Trim(),
            kiraci.CurrentTenantId ?? throw new InvalidOperationException("Kiracı bağlamı yok."),
            ct);
}

public sealed class RolYonetimDurumuHandler(IRolYonetimi roller) : IRequestHandler<RolYonetimDurumuRequest, RolYonetimDurumu>
{
    public Task<RolYonetimDurumu> Handle(RolYonetimDurumuRequest request, CancellationToken ct) =>
        roller.DurumAsync(ct);
}

/// <summary>Rol adı/koruma kuralları — tek yerde: RLS (<c>RowRule.Roller</c> virgüllü liste) ve
/// rapor paylaşımı (<c>PaylasimRolleri</c>) bu adları AYRAÇSIZ ve küçük harf bekler.</summary>
public static partial class RolKurallari
{
    public static readonly IReadOnlyList<string> SistemRolleri = [Roles.Admin, Roles.Gelistirici, Roles.Musteri];

    public static bool SistemRolu(string ad) =>
        SistemRolleri.Contains(ad, StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex("^[a-z0-9_-]{2,50}$")]
    public static partial Regex AdDeseni();

    /// <summary>Virgüllü rol listesi (DB alanı: PaylasimRolleri/ErisimRolleri/RowRule.Roller) ile
    /// kullanıcının token rolleri KESİŞİYOR mu? Boş liste = kesişim yok.</summary>
    public static bool Kesisir(string? virgulluListe, IReadOnlyCollection<string> kullaniciRolleri)
    {
        if (string.IsNullOrWhiteSpace(virgulluListe) || kullaniciRolleri.Count == 0) return false;
        return virgulluListe.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(r => kullaniciRolleri.Contains(r, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>UI'dan gelen rol listesini normalleştirir: kırp, küçült, tekille; boşsa null.</summary>
    public static string? Normallestir(IEnumerable<string>? roller)
    {
        var temiz = (roller ?? [])
            .Select(r => r.Trim().ToLowerInvariant())
            .Where(r => r.Length > 0)
            .Distinct()
            .ToList();
        return temiz.Count == 0 ? null : string.Join(',', temiz);
    }
}

public sealed class CreateRoleValidation : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleValidation()
    {
        RuleFor(x => x.Ad).NotEmpty()
            .Must(a => RolKurallari.AdDeseni().IsMatch(a ?? string.Empty))
            .WithMessage("Rol adı 2-50 karakter; yalnız küçük harf, rakam, '-' ve '_' (ör. bolge_muduru). " +
                         "Ad; token'a, RLS kurallarına ve paylaşım listelerine aynen yazılır.")
            .Must(a => !RolKurallari.SistemRolu(a ?? string.Empty))
            .WithMessage("Bu ad ürünün sistem rolüyle çakışıyor.");
        RuleFor(x => x.Aciklama).MaximumLength(300);
    }
}
