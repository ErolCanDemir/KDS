using Kds.Business.Identity;
using Kds.Business.Queries;
using Kds.Execution;
using Kds.Contracts.Queries;
using Kds.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kds.Tests;

/// <summary>S5 çalıştırma çekirdeği: çalıştırıcının tek-kapı garantisi (bekçi), denetim
/// davranışı (başarılı/başarısız audit) ve LocalDB entegrasyonu (gerçek SELECT + şema keşfi).</summary>
public sealed class QueryExecutionTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    // LocalDB entegrasyon testleri bu makinenin dev DB'sine (LocalDB / KdsMetadata) bağlanır.
    private const string LocalDb =
        "Server=(localdb)\\MSSQLLocalDB;Database=KdsMetadata;Trusted_Connection=True;TrustServerCertificate=True";

    [Fact]
    public async Task calistirici_bekciyi_gecemeyen_sqli_db_acmadan_reddeder()
    {
        var calistirici = new SqlQueryExecutor(new ScriptDomQueryGuard());
        // Bağlantı dizesi erişilemez; yine de bekçi ÖNCE reddettiği için SqlException değil
        // QuerySecurityException atılır (icra hiç başlamaz).
        var request = new QueryRequest("Server=erisilemez.gecersiz;", "DROP TABLE T",
            new Dictionary<string, object?>(), 100, TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<QuerySecurityException>(() => calistirici.ExecuteAsync(request, default));
    }

    /// <summary>Davranışın izole-scope gereksinimini testte karşılayan sahte fabrika — hep AYNI
    /// test DbContext'ini döndürür (SQLite in-memory tek bağlantı; dispose test ortamına ait).</summary>
    private sealed class TekBaglamKapsami(KdsDbContext context) : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        public IServiceScope CreateScope() => this;
        public IServiceProvider ServiceProvider => this;
        public object? GetService(Type t) =>
            t == typeof(KdsDbContext) ? context
            : t == typeof(IUserContext) ? new BosKullanici()
            : null;
        public void Dispose() { }
    }

    /// <summary>Kullanıcı bağlamı yok (KeycloakId null) → denetim "kim"i boş bırakır; bu testlerin
    /// odağı değil (audit yazımı + tenant damgası).</summary>
    private sealed class BosKullanici : IUserContext
    {
        public Guid? KeycloakId => null;
        public bool IsAdmin => false;
        public bool CanDesign => false;
    }

    [Fact]
    public async Task denetim_davranisi_basarili_icrayi_yazar()
    {
        await using var o = await SqliteOrtam();
        var davranis = new QueryAuditBehavior(new TekBaglamKapsami(o.Baglam));
        var request = new RunQueryRequest(Guid.NewGuid(), "SELECT 1");
        var beklenen = new QueryRunResult([new QueryColumnDto("c", "int")], [new object?[] { 1 }], false, 7);

        var result = await davranis.Handle(request, _ => Task.FromResult(beklenen), default);

        Assert.Same(beklenen, result);
        var record = await o.Baglam.DenetimKayitlari.IgnoreQueryFilters().SingleAsync();
        Assert.True(record.Basarili);
        Assert.Equal(1, record.SatirSayisi);
        Assert.Equal(7, record.SureMs);
        Assert.Equal(TenantA, record.TenantId);   // SaveChanges damgaladı
        Assert.Equal("SELECT 1", record.Sql);
    }

    [Fact]
    public async Task denetim_davranisi_engellenmis_denemeyi_yazar_ve_hatayi_yukseltir()
    {
        await using var o = await SqliteOrtam();
        var davranis = new QueryAuditBehavior(new TekBaglamKapsami(o.Baglam));
        var request = new RunQueryRequest(Guid.NewGuid(), "DROP TABLE T");

        await Assert.ThrowsAsync<QuerySecurityException>(() =>
            davranis.Handle(request, _ => throw new QuerySecurityException("engellendi"), default));

        var record = await o.Baglam.DenetimKayitlari.IgnoreQueryFilters().SingleAsync();
        Assert.False(record.Basarili);
        Assert.Equal("engellendi", record.Hata);
    }

    [Fact]
    public async Task denetim_parametre_degerlerini_de_yazar()
    {
        // Kod incelemesi O5: SQL şablonu tek başına "ne arandı"yı göstermez — değerler JSON'lanır.
        await using var o = await SqliteOrtam();
        var davranis = new QueryAuditBehavior(new TekBaglamKapsami(o.Baglam));
        var request = new RunQueryRequest(Guid.NewGuid(), "SELECT 1 WHERE @p0 = @p0",
            Parametreler: new Dictionary<string, object?> { ["@p0"] = "Ankara", ["@p1"] = 5000m });
        var beklenen = new QueryRunResult([], [], false, 1);

        await davranis.Handle(request, _ => Task.FromResult(beklenen), default);

        var record = await o.Baglam.DenetimKayitlari.IgnoreQueryFilters().SingleAsync();
        Assert.NotNull(record.ParametrelerJson);
        Assert.Contains("Ankara", record.ParametrelerJson);
        Assert.Contains("@p1", record.ParametrelerJson);
    }

    [Fact]
    public async Task calistirici_localdb_de_gercek_select_kosar_ve_satir_sinirini_uygular()
    {
        var calistirici = new SqlQueryExecutor(new ScriptDomQueryGuard());
        var sql = "SELECT v FROM (VALUES (1),(2),(3),(4),(5)) AS t(v)";
        var result = await calistirici.ExecuteAsync(
            new QueryRequest(LocalDb, sql, new Dictionary<string, object?>(), 3, TimeSpan.FromSeconds(10)), default);

        Assert.Equal(3, result.Satirlar.Count);
        Assert.True(result.SatirSiniriUygulandi);
        Assert.Equal("v", Assert.Single(result.Kolonlar).Ad);
    }

    [Fact]
    public async Task sema_kesfi_localdb_tablo_ve_kolonlari_bulur()
    {
        var kesif = new SqlSchemaDiscovery();

        var tablolar = await kesif.SearchTablesAsync(LocalDb, "Baglantilar", null, 20, default);
        Assert.Contains(tablolar, t => t.Ad == "Baglantilar");

        var kolonlar = await kesif.GetColumnsAsync(LocalDb, "dbo", "Baglantilar", default);
        Assert.Contains(kolonlar, k => k.Ad == "Id" && k.BirincilAnahtarMi);
    }

    private static async Task<Ortam> SqliteOrtam()
    {
        var baglanti = new SqliteConnection("DataSource=:memory:");
        await baglanti.OpenAsync();
        var context = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(baglanti).Options,
            new FixedTenantContext(TenantA));
        await context.Database.EnsureCreatedAsync();
        return new Ortam(baglanti, context);
    }

    private sealed class Ortam(SqliteConnection baglanti, KdsDbContext context) : IAsyncDisposable
    {
        public KdsDbContext Baglam { get; } = context;

        public async ValueTask DisposeAsync()
        {
            await Baglam.DisposeAsync();
            await baglanti.DisposeAsync();
        }
    }
}
