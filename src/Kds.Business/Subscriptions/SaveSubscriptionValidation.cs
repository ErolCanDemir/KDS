using FluentValidation;
using Kds.Business.Ingest;
using Kds.Contracts.Subscriptions;
using Kds.Export;

namespace Kds.Business.Subscriptions;

public sealed class SaveSubscriptionValidation : AbstractValidator<SaveSubscriptionRequest>
{
    public SaveSubscriptionValidation()
    {
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RaporId).NotEmpty();
        RuleFor(x => x.Bicim)
            .Must(b => Enum.TryParse<ExportFormat>(b, ignoreCase: true, out _))
            .WithMessage("Biçim Pdf, Xlsx veya Csv olmalı.");
        RuleFor(x => x.Alicilar).NotEmpty().WithMessage("En az bir alıcı e-posta gerekli.");
        RuleForEach(x => x.Alicilar).EmailAddress().WithMessage("Geçersiz e-posta adresi.");
        // Zamanlama isteğe bağlı (boş = yalnız elle gönderilir); doluysa geçerli cron olmalı (İçe Alım ile aynı kural).
        RuleFor(x => x.Zamanlama)
            .MaximumLength(100)   // DB kolonu nvarchar(100); sınırı DB hatası değil doğrulama söylesin
            .Must(z => string.IsNullOrWhiteSpace(z) || CronZamanlama.Gecerli(z))
            .WithMessage("Zamanlama geçerli bir cron ifadesi olmalı (5 alan, ya da saniyeli 6 alan).");
    }
}
