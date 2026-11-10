using FluentValidation;
using Kds.Contracts.QueryModel;

namespace Kds.Business.Queries;

/// <summary>"SQL'i Göster" isteğinin doğrulaması (400) — model kuralları paylaşılan
/// <see cref="QueryModelValidation"/>'den gelir.</summary>
public sealed class GenerateQuerySqlValidation : AbstractValidator<GenerateQuerySqlRequest>
{
    public GenerateQuerySqlValidation()
    {
        RuleFor(x => x.Model).NotNull();
        RuleFor(x => x.Model).SetValidator(new QueryModelValidation())
            .When(x => x.Model is not null);
    }
}
