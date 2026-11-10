using Kds.Business;
using Kds.Contracts.SystemStatus;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kds.Tests;

public sealed class CqrsPipelineTests
{
    [Fact]
    public async Task sistem_durumu_sorgusu_hattan_gecip_surum_dondurur()
    {
        await using var saglayici = new ServiceCollection().AddKdsBusiness().BuildServiceProvider();
        var gonderici = saglayici.GetRequiredService<ISender>();

        var yanit = await gonderici.Send(new SystemStatusRequest());

        Assert.Matches(@"^\d+\.\d+\.\d+$", yanit.Surum);
        Assert.NotEqual(default, yanit.SunucuZamani);
    }
}
