using Kds.Business.Connections;
using Kds.Business.Ingest;
using Kds.Business.Queries;
using Kds.Contracts.Connections;
using Kds.Data;
using Kds.Data.Entities;
using Kds.Execution;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S12 Faz 3 — mod yönlendirmesi: aynı sorgu, bağlantı Canlı iken KAYNAĞA, İçe Alım iken
/// tenant DEPOSUNA (KdsDepo_&lt;tenant&gt;) gider. Sahte çalıştırıcı gerçek bağlantı dizesini yakalar;
/// üretilen SQL/bekçi/audit değişmez — yalnız hedef değişir (başarı ölçütü #3).</summary>
public sealed class IceAlimRaporYonlendirmeTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private sealed class YakalayanCalistirici : IQueryExecutor
    {
        public string? Dize { get; private set; }
        public Task<QueryResult> ExecuteAsync(QueryRequest request, CancellationToken cancellationToken)
        {
            Dize = request.BaglantiDizesi;
            return Task.FromResult(new QueryResult([], [], false, TimeSpan.Zero));
        }
    }

    private sealed class KimlikKoruyucu : IPasswordProtector
    {
        public string Protect(string duzMetin) => duzMetin;
        public string Unprotect(string sifreli) => sifreli;
    }

    private static async Task<(KdsDbContext ctx, SqliteConnection c)> BaglamAsync()
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(TenantA));
        await ctx.Database.EnsureCreatedAsync();
        return (ctx, c);
    }

    private static async Task<Guid> BaglantiTohumla(KdsDbContext ctx, DataMode mod)
    {
        var b = new Connection { Ad = "K", Sunucu = "SRC-SERVER", Veritabani = "CanliDB", Mod = mod };
        ctx.Baglantilar.Add(b);
        await ctx.SaveChangesAsync();
        return b.Id;
    }

    private static async Task<string?> DizeYakala(KdsDbContext ctx, Guid baglantiId)
    {
        var calistirici = new YakalayanCalistirici();
        var ayar = new IceAlimAyarlari { DepoSunucuBaglantisi = "Server=DEPO-SERVER;Trusted_Connection=True;TrustServerCertificate=True" };
        var sistemAyarlari = new Kds.Business.Settings.SistemAyarlari(
            ctx,
            new Microsoft.Extensions.Caching.Memory.MemoryCache(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            new FixedTenantContext(TenantA));
        var handler = new RunQueryHandler(calistirici, new QueryConnectionResolver(
            ctx, new KimlikKoruyucu(), new DepoCozucu(ayar, sistemAyarlari), new FixedTenantContext(TenantA),
            new Kds.Business.DataMaps.ConnectionStringResolver(ctx, new KimlikKoruyucu())),
            sistemAyarlari);
        await handler.Handle(new Kds.Contracts.Queries.RunQueryRequest(baglantiId, "SELECT 1"), default);
        return calistirici.Dize;
    }

    [Fact]
    public async Task icealim_modu_tenant_deposuna_yonlenir()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _ = c;
        await using var __ = ctx;

        var id = await BaglantiTohumla(ctx, DataMode.IceAlim);
        var dize = await DizeYakala(ctx, id);

        Assert.NotNull(dize);
        Assert.Contains("KdsDepo_" + TenantA.ToString("N"), dize);   // depo DB
        Assert.Contains("DEPO-SERVER", dize);                        // depo SUNUCUSU
        Assert.DoesNotContain("CanliDB", dize);                      // canlı kaynağa GİTMEZ
    }

    [Fact]
    public async Task canli_modu_kaynaga_gider()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _ = c;
        await using var __ = ctx;

        var id = await BaglantiTohumla(ctx, DataMode.Canli);
        var dize = await DizeYakala(ctx, id);

        Assert.NotNull(dize);
        Assert.Contains("CanliDB", dize);        // canlı kaynağa gider
        Assert.Contains("SRC-SERVER", dize);
        Assert.DoesNotContain("KdsDepo_", dize); // depoya YÖNLENMEZ
    }

    [Fact]
    public async Task icealim_depo_hedefi_seciliyse_hedef_baglantiya_yonlenir()
    {
        // M22b: bağlantıda depo hedefi seçiliyse okuma varsayılan tenant deposuna değil
        // hedef bağlantının sunucu/veritabanına gider.
        var (ctx, c) = await BaglamAsync();
        await using var _ = c;
        await using var __ = ctx;

        var hedef = new Connection { Ad = "H", Sunucu = "HEDEF-SERVER", Veritabani = "DepoHedefDB", Mod = DataMode.Canli };
        ctx.Baglantilar.Add(hedef);
        await ctx.SaveChangesAsync();
        var kaynak = new Connection { Ad = "K", Sunucu = "SRC-SERVER", Veritabani = "CanliDB", Mod = DataMode.IceAlim, DepoBaglantiId = hedef.Id };
        ctx.Baglantilar.Add(kaynak);
        await ctx.SaveChangesAsync();

        var dize = await DizeYakala(ctx, kaynak.Id);

        Assert.NotNull(dize);
        Assert.Contains("HEDEF-SERVER", dize);
        Assert.Contains("DepoHedefDB", dize);
        Assert.DoesNotContain("KdsDepo_", dize); // varsayılan depoya GİTMEZ
        Assert.DoesNotContain("CanliDB", dize);  // kaynağa da GİTMEZ
    }
}
