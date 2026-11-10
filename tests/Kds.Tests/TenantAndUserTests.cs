using Kds.Business.Users;
using Kds.Contracts.Users;
using Kds.Data;
using Kds.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

public sealed class TenantAndUserTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static KdsDbContext Baglam(SqliteConnection baglanti, Guid? tenant) =>
        new(new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(baglanti).Options, new FixedTenantContext(tenant));

    [Fact]
    public async Task global_filter_yalnizca_kendi_tenant_kullanicilarini_dondurur()
    {
        var baglanti = new SqliteConnection("DataSource=:memory:");
        await baglanti.OpenAsync();
        await using var _ = baglanti;

        await using (var kur = Baglam(baglanti, null))
        {
            await kur.Database.EnsureCreatedAsync();
            kur.Kullanicilar.AddRange(
                new User { TenantId = TenantA, KeycloakId = Guid.NewGuid(), KullaniciAdi = "admin" },
                new User { TenantId = TenantB, KeycloakId = Guid.NewGuid(), KullaniciAdi = "musteri" });
            await kur.SaveChangesAsync();
        }

        await using (var a = Baglam(baglanti, TenantA))
            Assert.Equal("admin", Assert.Single(await a.Kullanicilar.Select(k => k.KullaniciAdi).ToListAsync()));

        await using (var b = Baglam(baglanti, TenantB))
            Assert.Equal("musteri", Assert.Single(await b.Kullanicilar.Select(k => k.KullaniciAdi).ToListAsync()));
    }

    [Fact]
    public async Task jit_kullaniciyi_olusturur_ve_ikinci_cagride_cogaltmaz()
    {
        var baglanti = new SqliteConnection("DataSource=:memory:");
        await baglanti.OpenAsync();
        await using var _ = baglanti;
        await using var context = Baglam(baglanti, TenantA);
        await context.Database.EnsureCreatedAsync();

        var handler = new ProvisionUserHandler(context);
        var sub = Guid.NewGuid();
        var id1 = await handler.Handle(new ProvisionUserRequest(sub, "admin", "a@kds.local", "Ad Soyad", TenantA), default);
        var id2 = await handler.Handle(new ProvisionUserRequest(sub, "admin", "a@kds.local", "Ad Soyad", TenantA), default);

        Assert.Equal(id1, id2);
        Assert.Equal(1, await context.Kullanicilar.IgnoreQueryFilters().CountAsync());
    }
}
