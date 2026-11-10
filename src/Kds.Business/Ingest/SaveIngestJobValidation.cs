using FluentValidation;
using Kds.Contracts.Ingest;

namespace Kds.Business.Ingest;

public sealed class SaveIngestJobValidation : AbstractValidator<SaveIngestJobRequest>
{
    public SaveIngestJobValidation()
    {
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BaglantiId).NotEmpty();
        RuleFor(x => x.Tablolar).NotEmpty().WithMessage("En az bir tablo seçilmeli.");
        // S44: artımlı kolon, kolon SEÇİMİ yapılmışsa seçilenler arasında olmalı — dışındaysa
        // koşucu her koşuda sessizce tam yüklemeye düşerdi; bunu kayıt anında açıkça söyleriz.
        RuleForEach(x => x.Tablolar)
            .Must(t => t.ArtimliKolon is null || t.Kolonlar is not { Count: > 0 }
                || t.Kolonlar.Contains(t.ArtimliKolon, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Artımlı kolon, tablonun seçili kolonları arasında olmalı.");
        // Zamanlama isteğe bağlı (boş = yalnız elle çalışır); doluysa geçerli cron olmalı.
        RuleFor(x => x.Zamanlama)
            .Must(z => string.IsNullOrWhiteSpace(z) || CronZamanlama.Gecerli(z))
            .WithMessage("Zamanlama geçerli bir cron ifadesi olmalı (5 alan, ya da saniyeli 6 alan).");
    }
}
