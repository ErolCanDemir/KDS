using FluentValidation;
using Kds.Contracts.Reports;

namespace Kds.Business.Reports;

public sealed class AddFolderValidation : AbstractValidator<AddFolderRequest>
{
    public AddFolderValidation()
    {
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(120);
    }
}
