using Kds.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary><see cref="KdsDbContextExtensions.IslemdeCalistirAsync"/> (çok-adımlı atomiklik): gövde
/// transaction içinde koşar ve commit olur; hata fırlarsa dışarı çıkar + transaction kapanır (rollback).</summary>
public sealed class TransactionExtTests
{
    private static async Task<(SqliteConnection, KdsDbContext)> BaglamKurAsync()
    {
        var baglanti = new SqliteConnection("DataSource=:memory:");
        await baglanti.OpenAsync();
        var secenekler = new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(baglanti).Options;
        return (baglanti, new KdsDbContext(secenekler, new FixedTenantContext(null)));
    }

    [Fact]
    public async Task islem_govdesi_transaction_icinde_calisir_ve_tamamlanir()
    {
        var (baglanti, context) = await BaglamKurAsync();
        await using var _ = baglanti;
        await using var __ = context;
        var calisti = false;

        await context.IslemdeCalistirAsync(ic =>
        {
            calisti = context.Database.CurrentTransaction is not null;
            return Task.CompletedTask;
        });

        Assert.True(calisti);
        Assert.Null(context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task govde_hata_firlatirsa_ayni_hata_disari_cikar_ve_islem_kapanir()
    {
        var (baglanti, context) = await BaglamKurAsync();
        await using var _ = baglanti;
        await using var __ = context;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.IslemdeCalistirAsync(ic => throw new InvalidOperationException("patladi")));

        Assert.Null(context.Database.CurrentTransaction);
    }
}
