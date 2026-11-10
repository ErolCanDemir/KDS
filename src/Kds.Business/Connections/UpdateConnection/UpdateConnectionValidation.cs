using FluentValidation;

namespace Kds.Business.Connections;

public sealed class UpdateConnectionValidation : AbstractValidator<UpdateConnectionRequest>
{
    public UpdateConnectionValidation()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Sunucu).NotEmpty().MaximumLength(400);
        RuleFor(x => x.Veritabani).NotEmpty().MaximumLength(200);
        RuleFor(x => x.KullaniciAdi).MaximumLength(200);
    }
}
