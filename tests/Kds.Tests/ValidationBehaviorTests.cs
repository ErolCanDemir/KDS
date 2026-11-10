using FluentValidation;
using Kds.Common.Cqrs;
using Kds.Common.Pipeline;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kds.Tests;

public sealed class ValidationBehaviorTests
{
    internal sealed record OrnekKomut(string Ad) : ICommand<string>;

    internal sealed class OrnekKomutDogrulayici : AbstractValidator<OrnekKomut>
    {
        public OrnekKomutDogrulayici() => RuleFor(k => k.Ad).NotEmpty();
    }

    internal sealed class OrnekKomutHandler : IRequestHandler<OrnekKomut, string>
    {
        public Task<string> Handle(OrnekKomut request, CancellationToken cancellationToken) => Task.FromResult(request.Ad);
    }

    private static ServiceProvider SaglayiciKur() =>
        new ServiceCollection()
            .AddMediatR(a => a.RegisterServicesFromAssembly(typeof(ValidationBehaviorTests).Assembly))
            .AddValidatorsFromAssembly(typeof(ValidationBehaviorTests).Assembly, includeInternalTypes: true)
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))
            .BuildServiceProvider();

    [Fact]
    public async Task gecersiz_istek_handler_calismadan_reddedilir()
    {
        await using var saglayici = SaglayiciKur();
        var gonderici = saglayici.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<ValidationException>(() => gonderici.Send(new OrnekKomut("")));
    }

    [Fact]
    public async Task gecerli_istek_handler_a_ulasir()
    {
        await using var saglayici = SaglayiciKur();
        var gonderici = saglayici.GetRequiredService<ISender>();

        Assert.Equal("kds", await gonderici.Send(new OrnekKomut("kds")));
    }
}
