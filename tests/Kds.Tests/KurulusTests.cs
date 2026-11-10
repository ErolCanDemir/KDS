using FluentValidation.TestHelper;
using Kds.Business.Organization;
using Kds.Business.Settings;
using Kds.Contracts.Organization;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S35 — Kuruluş Ayarları. Sınanan iki asıl karar: (1) limitler burada TANIMLANMAZ,
/// Sistem Ayarları'ndan okunup özetlenir; (2) boş bırakılan marka alanı "temizle" demektir.</summary>
public class KurulusTests
{
    private static readonly Guid Kiraci = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static async Task<KdsDbContext> BaglamAsync(SqliteConnection c, string ad = "LST Yazılım")
    {
        await c.OpenAsync();
        var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(Kiraci));
        await ctx.Database.EnsureCreatedAsync();
        ctx.Tenantlar.Add(new Tenant { Id = Kiraci, Ad = ad });
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task getir_kurulusu_ve_limit_ozetini_dondurur()
    {
        await using var c = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await BaglamAsync(c);
        var gonderici = new SahteAyarGonderici();

        var sonuc = await new GetOrganizationHandler(ctx, gonderici)
            .Handle(new GetOrganizationRequest(), CancellationToken.None);

        Assert.Equal(Kiraci, sonuc.KiraciId);
        Assert.Equal("LST Yazılım", sonuc.Ad);
        Assert.Equal("Aktif", sonuc.Durum);
        // Limitler Sistem Ayarları'ndan geldi ve İSTEM SIRASINI korudu (zaman aşımı → varsayılan
        // satır → sert tavan); ekrandaki okuma sırası buna dayanır.
        Assert.Equal(3, sonuc.Limitler.Count);
        Assert.Equal(AyarTanimlari.SorguZamanAsimiSn, sonuc.Limitler[0].Anahtar);
        Assert.Equal(AyarTanimlari.SorguEnCokSatir, sonuc.Limitler[2].Anahtar);
        // Kaynak rozeti korunur: kullanıcı değerin kiracıya mı özel olduğunu görebilmeli.
        Assert.Equal("kiraci", sonuc.Limitler[0].Kaynak);
        Assert.Equal("varsayilan", sonuc.Limitler[2].Kaynak);
    }

    [Fact]
    public async Task guncelle_marka_alanlarini_yazar()
    {
        await using var c = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await BaglamAsync(c);

        var sonuc = await new UpdateOrganizationHandler(ctx, new SahteAyarGonderici()).Handle(
            new UpdateOrganizationRequest("  Yeni Ad  ", "Kurumsal", "#0D8B8B", "data:image/png;base64,AAA"),
            CancellationToken.None);

        Assert.Equal("Yeni Ad", sonuc.Ad);          // kırpılır
        Assert.Equal("Kurumsal", sonuc.Plan);
        Assert.Equal("#0D8B8B", sonuc.MarkaRengi);
        Assert.Equal("data:image/png;base64,AAA", sonuc.LogoDataUri);
    }

    [Fact]
    public async Task guncelle_bos_alani_TEMIZLEME_sayar()
    {
        // "Logoyu kaldır" ayrı bir komut olmasın diye: boş/whitespace gelen marka alanı null'a düşer.
        await using var c = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await BaglamAsync(c);
        var gonderici = new SahteAyarGonderici();
        await new UpdateOrganizationHandler(ctx, gonderici).Handle(
            new UpdateOrganizationRequest("A", "Kurumsal", "#0D8B8B", "data:image/png;base64,AAA"),
            CancellationToken.None);

        var sonuc = await new UpdateOrganizationHandler(ctx, gonderici).Handle(
            new UpdateOrganizationRequest("A", "   ", "", null), CancellationToken.None);

        Assert.Null(sonuc.Plan);
        Assert.Null(sonuc.MarkaRengi);
        Assert.Null(sonuc.LogoDataUri);
    }

    [Fact]
    public async Task guncelle_kurulus_yoksa_KeyNotFound()
    {
        await using var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(Guid.NewGuid()));
        await ctx.Database.EnsureCreatedAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new UpdateOrganizationHandler(ctx, new SahteAyarGonderici())
                .Handle(new UpdateOrganizationRequest("A", null, null, null), CancellationToken.None));
    }

    [Theory]
    [InlineData("#0D8B8B", true)]
    [InlineData("#abcdef", true)]
    [InlineData("", true)]            // boş = temizle
    [InlineData("0D8B8B", false)]     // # yok
    [InlineData("#0D8B8", false)]     // 5 hane
    [InlineData("kirmizi", false)]
    public void dogrulama_marka_rengi_bicimini_zorlar(string renk, bool gecerli)
    {
        var sonuc = new UpdateOrganizationValidation()
            .TestValidate(new UpdateOrganizationRequest("A", null, renk, null));
        if (gecerli)
        {
            sonuc.ShouldNotHaveValidationErrorFor(x => x.MarkaRengi);
        }
        else
        {
            sonuc.ShouldHaveValidationErrorFor(x => x.MarkaRengi);
        }
    }

    [Fact]
    public void dogrulama_logoyu_gorsel_ve_boyutla_sinirlar()
    {
        var dogrulayici = new UpdateOrganizationValidation();

        // Görsel olmayan data URI reddedilir (ör. HTML kaçırma denemesi).
        dogrulayici.TestValidate(new UpdateOrganizationRequest("A", null, null, "data:text/html;base64,PHNjcmlwdD4="))
            .ShouldHaveValidationErrorFor(x => x.LogoDataUri);

        // Kolon sınırını (400 KB) aşacak logo, veritabanına gitmeden reddedilir.
        var kocaman = "data:image/png;base64," + new string('A', 300_001);
        dogrulayici.TestValidate(new UpdateOrganizationRequest("A", null, null, kocaman))
            .ShouldHaveValidationErrorFor(x => x.LogoDataUri);

        dogrulayici.TestValidate(new UpdateOrganizationRequest("A", null, null, "data:image/svg+xml;base64,AAA"))
            .ShouldNotHaveValidationErrorFor(x => x.LogoDataUri);
    }

    [Fact]
    public async Task marka_ucu_yalniz_cizim_alanlarini_dondurur()
    {
        // Bu uç TÜM rollere açıktır (logo herkesin ekranında görünmeli) — bu yüzden plan, limit,
        // kiracı kimliği gibi yönetim bilgisi TAŞIMAMALI. Sözleşme bunu tip düzeyinde kısıtlar;
        // test, alanların doğru dolduğunu sabitler.
        await using var c = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await BaglamAsync(c);
        await new UpdateOrganizationHandler(ctx, new SahteAyarGonderici()).Handle(
            new UpdateOrganizationRequest("LST", "Kurumsal", "#123456", "data:image/png;base64,AAA"),
            CancellationToken.None);

        var marka = await new GetBrandingHandler(ctx).Handle(new GetBrandingRequest(), CancellationToken.None);

        Assert.Equal("LST", marka.Ad);
        Assert.Equal("#123456", marka.MarkaRengi);
        Assert.Equal("data:image/png;base64,AAA", marka.LogoDataUri);
    }

    [Fact]
    public async Task marka_kurulus_kaydi_yoksa_urun_adiyla_doner()
    {
        // Kabuk her hâlükârda çizilmek zorunda: kayıt yoksa hata değil, ürünün kendi kimliği döner.
        await using var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(Guid.NewGuid()));
        await ctx.Database.EnsureCreatedAsync();

        var marka = await new GetBrandingHandler(ctx).Handle(new GetBrandingRequest(), CancellationToken.None);

        Assert.Equal("LST-KDS", marka.Ad);
        Assert.Null(marka.LogoDataUri);
    }

    [Fact]
    public void dogrulama_ad_zorunlu()
    {
        new UpdateOrganizationValidation()
            .TestValidate(new UpdateOrganizationRequest("   ", null, null, null))
            .ShouldHaveValidationErrorFor(x => x.Ad);
    }

    /// <summary>Yalnız <see cref="ListSettingsRequest"/>'e cevap veren sahte gönderici: limit
    /// özetinin Sistem Ayarları'ndan OKUNDUĞUNU (burada yeniden tanımlanmadığını) sabitler.</summary>
    private sealed class SahteAyarGonderici : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is not ListSettingsRequest)
            {
                throw new NotSupportedException($"Beklenmeyen istek: {request.GetType().Name}");
            }

            IReadOnlyList<AyarDto> ayarlar =
            [
                new(AyarTanimlari.SorguEnCokSatir, "Sorgu", "Sert satır tavanı", "Sayi", "", "10000", "10000", "varsayilan"),
                new(AyarTanimlari.EpostaSunucu, "E-posta", "SMTP sunucusu", "Metin", "", "", "posta.firma.com", "kurulum"),
                new(AyarTanimlari.SorguZamanAsimiSn, "Sorgu", "Sorgu zaman aşımı (sn)", "Sayi", "", "60", "90", "kiraci"),
                new(AyarTanimlari.SorguVarsayilanSatir, "Sorgu", "Varsayılan satır sayısı", "Sayi", "", "1000", "1000", "varsayilan"),
            ];
            return Task.FromResult((TResponse)(object)ayarlar);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
