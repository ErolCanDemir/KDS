using Kds.Contracts.Logging;
using Kds.Data;
using Kds.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>Olay günlüğü — DataChange interceptor: SaveChanges bir Insert/Update için DataChangeEvent
/// üretir mi, ActionId taşır mı, şifre alanı maskelenir mi.</summary>
public sealed class DataChangeInterceptorTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed class YakalayanGunluk : IEventLog
    {
        public List<ILogEvent> Olaylar { get; } = [];
        public void Write(ILogEvent olay) => Olaylar.Add(olay);
    }

    private sealed record SahteEylem(Guid ActionId, Guid? TenantId, Guid? UserId, string? UserName, string? Ip)
        : IActionContext;

    [Fact]
    public async Task insert_datachange_olayi_uretir_actionid_ve_sifre_maskesi_ile()
    {
        var actionId = Guid.NewGuid();
        var gunluk = new YakalayanGunluk();
        var eylem = new SahteEylem(actionId, TenantA, Guid.NewGuid(), "admin", "127.0.0.1");

        var baglanti = new SqliteConnection("DataSource=:memory:");
        await baglanti.OpenAsync();
        await using var _ = baglanti;
        var secenekler = new DbContextOptionsBuilder<KdsDbContext>()
            .UseSqlite(baglanti)
            .AddInterceptors(new DataChangeInterceptor(gunluk, eylem))
            .Options;
        await using var context = new KdsDbContext(secenekler, new FixedTenantContext(TenantA));
        await context.Database.EnsureCreatedAsync();

        context.Baglantilar.Add(new Connection
        {
            TenantId = TenantA, Ad = "Üretim", Sunucu = "s", Veritabani = "d", SifreSifreli = "gizli-parola",
        });
        await context.SaveChangesAsync();

        var olay = Assert.IsType<DataChangeEvent>(Assert.Single(gunluk.Olaylar));
        Assert.Equal(actionId, olay.ActionId);               // ait olduğu aksiyona bağlı
        Assert.Equal("Connection", olay.EntityName);
        Assert.Equal("Insert", olay.Operation);
        Assert.Equal(TenantA, olay.TenantId);
        Assert.NotNull(olay.After);
        Assert.Contains("Üretim", olay.After);
        Assert.DoesNotContain("gizli-parola", olay.After);   // şifre loga sızmaz
        Assert.Contains("***", olay.After);                  // maskelendi
    }
}
