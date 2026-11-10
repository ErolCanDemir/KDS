using Kds.Business.Identity;
using Kds.Business.Reports;
using Kds.Contracts.Reports;
using Kds.Data;
using Kds.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S27 — rol-bazlı rapor/klasör görünürlüğü: liste süzgeci ve tekil erişim
/// (<see cref="RaporGorunurluk.GorebilirAsync"/>) AYNI kuralı uygular; buradaki senaryolar
/// "listede yok ama URL ile açılıyor" sızıntısına karşı iki kapıyı birlikte sınar.</summary>
public sealed class RolPaylasimTests
{
    private static readonly Guid Kiraci = Guid.Parse("44444444-0000-0000-0000-000000000001");

    private sealed class SahteKb(Guid? keycloakId, bool admin, IReadOnlyCollection<string> roller) : IUserContext
    {
        public Guid? KeycloakId { get; } = keycloakId;
        public bool IsAdmin { get; } = admin;
        public bool CanDesign => IsAdmin;
        public IReadOnlyCollection<string> Roller { get; } = roller;
    }

    private static async Task<(KdsDbContext Ctx, SqliteConnection C, Guid BayiUserId, Guid SahipUserId)> OrtamAsync()
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(Kiraci));
        await ctx.Database.EnsureCreatedAsync();
        ctx.Tenantlar.Add(new Tenant { Id = Kiraci, Ad = "Test Kiraci" });

        var sahip = new User { TenantId = Kiraci, KeycloakId = Guid.NewGuid(), KullaniciAdi = "sahip" };
        var bayi = new User { TenantId = Kiraci, KeycloakId = Guid.NewGuid(), KullaniciAdi = "bayi" };
        ctx.Kullanicilar.AddRange(sahip, bayi);

        var klasor = new ReportFolder { TenantId = Kiraci, Ad = "Servis Raporlari", ErisimRolleri = "servis_bayi" };
        ctx.RaporKlasorleri.Add(klasor);
        await ctx.SaveChangesAsync();

        ctx.Raporlar.AddRange(
            new Report { TenantId = Kiraci, Ad = "EkipRaporu", Paylasim = ReportSharing.Ekip, SahipId = sahip.Id },
            new Report { TenantId = Kiraci, Ad = "OzelRaporu", Paylasim = ReportSharing.Ozel, SahipId = sahip.Id },
            new Report { TenantId = Kiraci, Ad = "BolgeRaporu", Paylasim = ReportSharing.Roller, PaylasimRolleri = "bolge_muduru", SahipId = sahip.Id },
            new Report { TenantId = Kiraci, Ad = "KlasorEkip", Paylasim = ReportSharing.Ekip, SahipId = sahip.Id, KlasorId = klasor.Id },
            new Report { TenantId = Kiraci, Ad = "KlasorRoller", Paylasim = ReportSharing.Roller, PaylasimRolleri = "baska_rol", SahipId = sahip.Id, KlasorId = klasor.Id },
            new Report { TenantId = Kiraci, Ad = "KlasorOzel", Paylasim = ReportSharing.Ozel, SahipId = sahip.Id, KlasorId = klasor.Id });
        await ctx.SaveChangesAsync();
        return (ctx, c, bayi.KeycloakId, sahip.KeycloakId);
    }

    /* ---------- RolKurallari birim ---------- */

    [Theory]
    [InlineData("bolge_muduru", new[] { "musteri", "bolge_muduru" }, true)]
    [InlineData("bolge_muduru, servis_bayi", new[] { "servis_bayi" }, true)]
    [InlineData("BOLGE_MUDURU", new[] { "bolge_muduru" }, true)]   // büyük/küçük duyarsız
    [InlineData("bolge_muduru", new[] { "musteri" }, false)]
    [InlineData(null, new[] { "musteri" }, false)]
    [InlineData("bolge_muduru", new string[0], false)]
    public void kesisim_dogru_calisir(string? liste, string[] roller, bool bekleneni) =>
        Assert.Equal(bekleneni, RolKurallari.Kesisir(liste, roller));

    [Fact]
    public void normallestir_kirpar_kucultur_tekiller()
    {
        Assert.Equal("bolge_muduru,servis_bayi",
            RolKurallari.Normallestir([" Bolge_Muduru ", "servis_bayi", "bolge_muduru", ""]));
        Assert.Null(RolKurallari.Normallestir([]));
        Assert.Null(RolKurallari.Normallestir(null));
    }

    [Theory]
    [InlineData("bolge_muduru", true)]
    [InlineData("servis-bayi2", true)]
    [InlineData("Bolge", false)]      // büyük harf yasak
    [InlineData("a", false)]          // çok kısa
    [InlineData("rol adı", false)]    // boşluk/Türkçe karakter yasak
    public void rol_adi_deseni(string ad, bool gecerli) =>
        Assert.Equal(gecerli, RolKurallari.AdDeseni().IsMatch(ad));

    /* ---------- liste süzgeci ---------- */

    [Fact]
    public async Task rol_kesisimli_rapor_listede_gorunur_kesismeyen_gorunmez()
    {
        var (ctx, c, bayiKid, _) = await OrtamAsync();
        await using var _1 = c; await using var _2 = ctx;

        var bolgeci = new SahteKb(bayiKid, admin: false, ["musteri", "bolge_muduru"]);
        var liste = await new ListReportsHandler(ctx, bolgeci).Handle(new ListReportsRequest(), CancellationToken.None);

        Assert.Contains(liste, r => r.Ad == "BolgeRaporu");     // rol kesişti
        Assert.Contains(liste, r => r.Ad == "EkipRaporu");      // ekip zaten açık
        Assert.DoesNotContain(liste, r => r.Ad == "OzelRaporu");        // özel yalnız sahibinin
        Assert.DoesNotContain(liste, r => r.Ad == "KlasorRoller");      // ne rol ne klasör kesişti
    }

    [Fact]
    public async Task klasor_rol_erisimi_ozel_olmayan_raporlari_acar_ozeli_acmaz()
    {
        var (ctx, c, bayiKid, _) = await OrtamAsync();
        await using var _1 = c; await using var _2 = ctx;

        var bayi = new SahteKb(bayiKid, admin: false, ["musteri", "servis_bayi"]);
        var liste = await new ListReportsHandler(ctx, bayi).Handle(new ListReportsRequest(), CancellationToken.None);

        Assert.Contains(liste, r => r.Ad == "KlasorEkip");      // klasör izni (zaten ekip)
        Assert.Contains(liste, r => r.Ad == "KlasorRoller");    // klasör izni ROL kesişmese de açar
        Assert.DoesNotContain(liste, r => r.Ad == "KlasorOzel");        // özel her durumda kapalı
        Assert.DoesNotContain(liste, r => r.Ad == "BolgeRaporu");       // başka rolün raporu kapalı
    }

    /* ---------- tekil erişim (Run/Get ile aynı kapı) ---------- */

    [Fact]
    public async Task tekil_erisim_liste_kuraliyla_ayni()
    {
        var (ctx, c, bayiKid, _) = await OrtamAsync();
        await using var _1 = c; await using var _2 = ctx;

        var bayi = new SahteKb(bayiKid, admin: false, ["musteri", "servis_bayi"]);
        var bayiId = (Guid?)await ctx.Kullanicilar.Where(u => u.KeycloakId == bayiKid).Select(u => u.Id).FirstAsync();

        var klasorRoller = await ctx.Raporlar.FirstAsync(r => r.Ad == "KlasorRoller");
        var klasorOzel = await ctx.Raporlar.FirstAsync(r => r.Ad == "KlasorOzel");
        var bolge = await ctx.Raporlar.FirstAsync(r => r.Ad == "BolgeRaporu");

        Assert.True(await RaporGorunurluk.GorebilirAsync(ctx, bayi, klasorRoller, bayiId, CancellationToken.None));
        Assert.False(await RaporGorunurluk.GorebilirAsync(ctx, bayi, klasorOzel, bayiId, CancellationToken.None));
        Assert.False(await RaporGorunurluk.GorebilirAsync(ctx, bayi, bolge, bayiId, CancellationToken.None));
    }

    [Fact]
    public async Task admin_ve_sahip_her_zaman_gorur()
    {
        var (ctx, c, _, sahipKid) = await OrtamAsync();
        await using var _1 = c; await using var _2 = ctx;

        var admin = new SahteKb(Guid.NewGuid(), admin: true, ["admin"]);
        var sahip = new SahteKb(sahipKid, admin: false, ["gelistirici"]);
        var sahipId = (Guid?)await ctx.Kullanicilar.Where(u => u.KeycloakId == sahipKid).Select(u => u.Id).FirstAsync();

        var ozel = await ctx.Raporlar.FirstAsync(r => r.Ad == "OzelRaporu");
        Assert.True(await RaporGorunurluk.GorebilirAsync(ctx, admin, ozel, null, CancellationToken.None));
        Assert.True(await RaporGorunurluk.GorebilirAsync(ctx, sahip, ozel, sahipId, CancellationToken.None));
    }

    /* ---------- klasör sayaçları ---------- */

    [Fact]
    public async Task klasor_sayaci_rol_erisiminde_ozel_olmayanlari_sayar()
    {
        var (ctx, c, bayiKid, _) = await OrtamAsync();
        await using var _1 = c; await using var _2 = ctx;

        var bayi = new SahteKb(bayiKid, admin: false, ["musteri", "servis_bayi"]);
        var klasorler = await new ListFoldersHandler(ctx, bayi).Handle(new ListFoldersRequest(), CancellationToken.None);

        var k = Assert.Single(klasorler);
        Assert.Equal(2, k.RaporSayisi);   // KlasorEkip + KlasorRoller; KlasorOzel sayılmaz
        Assert.Contains("servis_bayi", k.ErisimRolleri ?? []);
    }
}
