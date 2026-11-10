using FluentValidation;
using Kds.Contracts.Metrics;

namespace Kds.Business.Metrics;

/// <summary>Metrik kaydetme doğrulaması — biçimsel kurallar burada; formülün İÇERİĞİ handler'da
/// üreticinin kendisiyle doğrulanır (harita gerektirir, validator'ın işi değil).</summary>
public sealed class SaveMetricValidation : AbstractValidator<SaveMetricRequest>
{
    public SaveMetricValidation()
    {
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(150);
        RuleFor(x => x.VeriHaritasiId).NotEmpty();
        RuleFor(x => x.Ifade).NotEmpty().MaximumLength(2000)
            .WithMessage("Formül boş olamaz — ör. SUM({Odemeler.Tutar}).");
        RuleFor(x => x.Bicim).IsInEnum();
        RuleFor(x => x.Aciklama).MaximumLength(500);
    }
}
