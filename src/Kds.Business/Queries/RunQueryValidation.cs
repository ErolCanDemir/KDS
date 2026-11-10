using FluentValidation;
using Kds.Contracts.Queries;

namespace Kds.Business.Queries;

/// <summary>Ham SQL yürütmenin yapısal doğrulaması (S9 ucu; iç göndericiler de geçer).
/// İçerik güvenliği bekçinin işidir — burada yalnız boş/aşırı girdiler elenir.</summary>
public sealed class RunQueryValidation : AbstractValidator<RunQueryRequest>
{
    public RunQueryValidation()
    {
        RuleFor(x => x.BaglantiId).NotEmpty();
        RuleFor(x => x.Sql).NotEmpty().WithMessage("SQL boş olamaz.")
            .MaximumLength(100_000).WithMessage("SQL metni çok uzun.");
    }
}
