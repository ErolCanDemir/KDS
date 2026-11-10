using FluentValidation;
using Kds.Contracts.QueryModel;

namespace Kds.Business.Queries;

/// <summary>Sorgu modelinin YAPISAL kuralları — modeli taşıyan HER request (SQL üret + model
/// çalıştır) aynı kurallardan geçer; kurallar tek yerde yaşar. Haritayla tutarlılık (kolon var mı,
/// tablolar ilişkili mi) üreticide (<c>QueryModelException</c>) denetlenir — orası haritayı görür.</summary>
public sealed class QueryModelValidation : AbstractValidator<QueryModel>
{
    public QueryModelValidation()
    {
        RuleFor(x => x.HaritaId).NotEmpty();
        // S22: hesaplanan kolon ya da katalog metriği tek başına geçerli sorgudur ("yalnız Toplam
        // Ciro" = tek KPI; Metrikler ekranındaki Ölç bu yolu kullanır) — kural üreticiyle AYNI.
        RuleFor(x => x.Alanlar).NotEmpty()
            .When(x => (x.HesaplananAlanlar is null || x.HesaplananAlanlar.Count == 0)
                && (x.MetrikAlanlar is null || x.MetrikAlanlar.Count == 0))
            .WithMessage("En az bir alan ya da hesaplanan kolon/metrik seçilmeli.");
        RuleForEach(x => x.Alanlar).ChildRules(a =>
        {
            a.RuleFor(x => x.TabloId).NotEmpty();
            a.RuleFor(x => x.Kolon).NotEmpty();
        });
        RuleFor(x => x.SatirLimiti!.Value).GreaterThan(0).When(x => x.SatirLimiti.HasValue);
    }
}
