using FluentValidation;
using Kds.Contracts.Dashboards;

namespace Kds.Business.Dashboards;

public sealed class SaveDashboardValidation : AbstractValidator<SaveDashboardRequest>
{
    public SaveDashboardValidation()
    {
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);
    }
}
