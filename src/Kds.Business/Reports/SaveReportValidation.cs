using FluentValidation;
using Kds.Contracts.Reports;

namespace Kds.Business.Reports;

public sealed class SaveReportValidation : AbstractValidator<SaveReportRequest>
{
    public SaveReportValidation()
    {
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);
        // İki tasarım türünden TAM BİRİ: model (tasarımcı) YA DA ham SQL + bağlantı (SQL Editörü).
        RuleFor(x => x)
            .Must(x => (x.Model is not null) ^ !string.IsNullOrWhiteSpace(x.HamSql))
            .WithMessage("Report ya sorgu modeli ya da ham SQL içermeli (ikisi birden değil).");
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.HaritaId).NotEmpty();
            RuleFor(x => x.Model!.Alanlar).NotEmpty().WithMessage("Report en az bir alan içermeli.");
        });
        When(x => !string.IsNullOrWhiteSpace(x.HamSql), () =>
        {
            RuleFor(x => x.BaglantiId).NotEmpty().WithMessage("SQL raporu için bağlantı seçilmeli.");
        });
    }
}
