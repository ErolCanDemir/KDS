using Kds.Business.Queries;
using Kds.Contracts.QueryModel;
using Xunit;

namespace Kds.Tests;

/// <summary>Kod inceleme sohbeti dönüşü: modeli taşıyan İKİ request de (SQL üret + model çalıştır)
/// aynı yapısal kurallardan geçmeli — çalıştırma yolu doğrulamasız kalmıştı.</summary>
public sealed class QueryModelValidationTests
{
    private static QueryModel GecerliModel() => new(
        Guid.NewGuid(), [new SelectedField(Guid.NewGuid(), "Ad")], null, [], SatirLimiti: null);

    [Fact]
    public void gecerli_model_iki_yoldan_da_gecer()
    {
        var model = GecerliModel();
        Assert.True(new GenerateQuerySqlValidation().Validate(new GenerateQuerySqlRequest(model)).IsValid);
        Assert.True(new RunQueryModelValidation().Validate(new RunQueryModelRequest(model)).IsValid);
    }

    [Fact]
    public void alansiz_model_iki_yoldan_da_reddedilir()
    {
        var bos = GecerliModel() with { Alanlar = [] };
        Assert.False(new GenerateQuerySqlValidation().Validate(new GenerateQuerySqlRequest(bos)).IsValid);
        Assert.False(new RunQueryModelValidation().Validate(new RunQueryModelRequest(bos)).IsValid);
    }

    [Fact]
    public void sifir_satir_limiti_calistirma_yolunda_da_reddedilir()
    {
        var model = GecerliModel() with { SatirLimiti = 0 };
        Assert.False(new RunQueryModelValidation().Validate(new RunQueryModelRequest(model)).IsValid);
    }
}
