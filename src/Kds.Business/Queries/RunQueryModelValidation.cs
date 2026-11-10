using FluentValidation;
using Kds.Contracts.QueryModel;

namespace Kds.Business.Queries;

/// <summary>Tasarımcının "Çalıştır" isteğinin doğrulaması — kod inceleme sohbeti dönüşü: SQL
/// üretme yolu yapısal doğrulanırken AYNI modeli taşıyan çalıştırma yolu doğrulamasız kalmıştı;
/// iki yol da paylaşılan <see cref="QueryModelValidation"/>'den geçer.</summary>
public sealed class RunQueryModelValidation : AbstractValidator<RunQueryModelRequest>
{
    public RunQueryModelValidation()
    {
        RuleFor(x => x.Model).NotNull();
        RuleFor(x => x.Model).SetValidator(new QueryModelValidation())
            .When(x => x.Model is not null);
    }
}
