using FluentValidation;

namespace Kds.Business.Connections;

public sealed class AddConnectionValidation : AbstractValidator<AddConnectionRequest>
{
    public AddConnectionValidation()
    {
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Sunucu).NotEmpty().MaximumLength(400);
        RuleFor(x => x.Veritabani).NotEmpty().MaximumLength(200);
        RuleFor(x => x.KullaniciAdi).MaximumLength(200);
    }
}
