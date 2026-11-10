using FluentValidation;
using Kds.Contracts.DataMaps;

namespace Kds.Business.DataMaps;

public sealed class SaveDataMapValidation : AbstractValidator<SaveDataMapRequest>
{
    public SaveDataMapValidation()
    {
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BaglantiId).NotEmpty();
        RuleForEach(x => x.Tablolar).ChildRules(t =>
        {
            t.RuleFor(x => x.HamAd).NotEmpty().MaximumLength(256);
            t.RuleFor(x => x.DostcaAd).NotEmpty().MaximumLength(200);
        });
    }
}
