using Kds.Business.Alerts;
using Kds.Business.Identity;
using Kds.Business.Security;
using Kds.Business.Subscriptions;
using Kds.Contracts.Alerts;
using Kds.Contracts.QueryModel;
using Kds.Contracts.Reports;
using Kds.Contracts.Subscriptions;
using Kds.Data;
using Kds.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S20 sahiplik kapısı. Uyarılar ve Zamanlanmış Raporlar 19 Ağu 2026'da müşteri rolüne
/// açıldı; rol kapısını kaldırmak tek başına yapılsaydı bir müşteri BAŞKA bir müşterinin uyarısını
/// görebilir, silebilir ve alıcı listesini okuyabilirdi. Kapı erişimi açmaz, role göre DARALTIR.</summary>
public sealed class UyariSahiplikTests
{
    private static readonly Guid Kiraci = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AhmetKc = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid MerveKc = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private sealed record SahteKb(Guid? KeycloakId, bool IsAdmin, bool CanDesign) : IUserContext;

    private static IUserContext Musteri(Guid kc) => new SahteKb(kc, IsAdmin: false, CanDesign: false);
    private static IUserContext Admin() => new SahteKb(null, IsAdmin: true, CanDesign: true);

    private static async Task<(KdsDbContext Ctx, SqliteConnection C, Guid EkipRapor, Guid AhmetOzelRapor)> BaglamAsync()
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(Kiraci));
        await ctx.Database.EnsureCreatedAsync();

        var ahmet = new User { Id = Guid.NewGuid(), TenantId = Kiraci, KeycloakId = AhmetKc, KullaniciAdi = "ahmet" };
        var merve = new User { Id = Guid.NewGuid(), TenantId = Kiraci, KeycloakId = MerveKc, KullaniciAdi = "merve" };
        ctx.Kullanicilar.AddRange(ahmet, merve);

        var ekip = new Report
        {
            Id = Guid.NewGuid(), TenantId = Kiraci, Ad = "Ekip Raporu",
            Paylasim = ReportSharing.Ekip, Gorsellestirme = VisualizationType.Tablo, SahipId = ahmet.Id,
        };
        var ozel = new Report
        {
            Id = Guid.NewGuid(), TenantId = Kiraci, Ad = "Ahmet'in Özel Raporu",
            Paylasim = ReportSharing.Ozel, Gorsellestirme = VisualizationType.Tablo, SahipId = ahmet.Id,
        };
        ctx.Raporlar.AddRange(ekip, ozel);
        await ctx.SaveChangesAsync();
        return (ctx, c, ekip.Id, ozel.Id);
    }

    private static SaveAlertRequest Istek(Guid raporId, string ad = "Eşik") =>
        new(null, ad, raporId, null, OzetTuru.SatirSayisi, UyariOp.Buyuk, 0, ["a@x.com"], null, true, false);

    [Fact]
    public async Task musteri_baskasinin_uyarisini_GOREMEZ_ve_SILEMEZ()
    {
        var (ctx, c, ekipRapor, _) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;

        var ahmetin = await new SaveAlertHandler(ctx, Musteri(AhmetKc), new KuralsizCozucu())
            .Handle(Istek(ekipRapor, "Ahmet'in uyarısı"), default);

        // Merve aynı kiracıda ama uyarı onun değil → listede yok.
        var merveninListesi = await new ListAlertsHandler(ctx, Musteri(MerveKc)).Handle(new ListAlertsRequest(), default);
        Assert.Empty(merveninListesi);

        // Tek kayda erişim de kapalı — ve 403 değil 404: "var ama senin değil" bilgisi bile sızıntıdır.
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new GetAlertHandler(ctx, Musteri(MerveKc)).Handle(new GetAlertRequest(ahmetin.Id), default));

        // Silme sessizce false döner; kayıt yerinde kalır.
        Assert.False(await new DeleteAlertHandler(ctx, Musteri(MerveKc)).Handle(new DeleteAlertRequest(ahmetin.Id), default));
        Assert.Equal(1, await ctx.Uyarilar.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task admin_kiracinin_tumunu_gorur()
    {
        var (ctx, c, ekipRapor, _) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;

        await new SaveAlertHandler(ctx, Musteri(AhmetKc), new KuralsizCozucu()).Handle(Istek(ekipRapor, "Ahmet"), default);
        await new SaveAlertHandler(ctx, Musteri(MerveKc), new KuralsizCozucu()).Handle(Istek(ekipRapor, "Merve"), default);

        Assert.Equal(2, (await new ListAlertsHandler(ctx, Admin()).Handle(new ListAlertsRequest(), default)).Count);
        Assert.Single(await new ListAlertsHandler(ctx, Musteri(AhmetKc)).Handle(new ListAlertsRequest(), default));
    }

    [Fact]
    public async Task musteri_goremedigi_raporu_izleyemez()
    {
        var (ctx, c, _, ahmetOzel) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;

        // Merve, Ahmet'in ÖZEL raporuna uyarı kuramaz: "eşik aşıldı" bilgisi de veridir —
        // aksi hâlde uyarı, göremediği bir raporun değerini sızdıran yan kanal olurdu.
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new SaveAlertHandler(ctx, Musteri(MerveKc), new KuralsizCozucu()).Handle(Istek(ahmetOzel), default));

        // Sahibi kurabilir.
        var kendi = await new SaveAlertHandler(ctx, Musteri(AhmetKc), new KuralsizCozucu()).Handle(Istek(ahmetOzel), default);
        Assert.True(kendi.BenimMi);
    }

    [Fact]
    public async Task esik_degisince_durum_sifirlanir()
    {
        var (ctx, c, ekipRapor, _) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;

        var ozet = await new SaveAlertHandler(ctx, Musteri(AhmetKc), new KuralsizCozucu()).Handle(Istek(ekipRapor), default);
        var satir = await ctx.Uyarilar.FirstAsync(u => u.Id == ozet.Id);
        satir.Tetiklendi = true;
        satir.SonDurum = "Tetiklendi";
        satir.SonDeger = 42;
        await ctx.SaveChangesAsync();

        // Eşik değişti → eski karar yeni kurala ait değil. Taşınsaydı ilk koşumda ya yanlış
        // "normale döndü" postası giderdi ya da gerçek aşım sessiz kalırdı.
        await new SaveAlertHandler(ctx, Musteri(AhmetKc), new KuralsizCozucu()).Handle(
            new SaveAlertRequest(ozet.Id, "Eşik", ekipRapor, null, OzetTuru.SatirSayisi, UyariOp.Buyuk,
                99, ["a@x.com"], null, true, false), default);

        var guncel = await ctx.Uyarilar.AsNoTracking().FirstAsync(u => u.Id == ozet.Id);
        Assert.False(guncel.Tetiklendi);
        Assert.Null(guncel.SonDurum);
        Assert.Null(guncel.SonDeger);
    }
}


