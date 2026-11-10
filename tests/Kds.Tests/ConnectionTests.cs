using Kds.Business.Connections;
using Kds.Execution;
using Kds.Contracts.Connections;
using Kds.Data;
using Kds.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S4 — Bağlantı yönetimi handler testleri (SQLite in-memory + sahte koruma/denetçi).
/// Parola şifreleme ve gerçek DB bağlantısı sahtelenir; test edilen şey handler mantığıdır.</summary>
public sealed class ConnectionTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static KdsDbContext Baglam(SqliteConnection baglanti, Guid? tenant) =>
        new(new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(baglanti).Options, new FixedTenantContext(tenant));

    // Tersinir sahte koruma: parolanın düz metin saklanmadığını + geri çözüldüğünü doğrular.
    private sealed class SahteKoruma : IPasswordProtector
    {
        public string Protect(string d) => string.IsNullOrEmpty(d) ? string.Empty : "enc:" + d;
        public string Unprotect(string s) => string.IsNullOrEmpty(s) ? string.Empty : s["enc:".Length..];
    }

    private sealed class SahteDenetci(bool basarili) : IConnectionProber
    {
        public Task<ProbeResult> ProbeAsync(string s, string v, string? k, string? sf, CancellationToken i) =>
            Task.FromResult(new ProbeResult(basarili, basarili ? 5 : (int?)null, basarili ? null : "hata"));
    }

    [Fact]
    public async Task ekle_parolayi_sifreli_saklar_ve_tenant_damgalar()
    {
        var baglanti = new SqliteConnection("DataSource=:memory:");
        await baglanti.OpenAsync();
        await using var _ = baglanti;
        await using var context = Baglam(baglanti, TenantA);
        await context.Database.EnsureCreatedAsync();

        var ozet = await new AddConnectionHandler(context, new SahteKoruma())
            .Handle(new AddConnectionRequest("Üretim", "sql-1", "SatisDB", "reader", "gizli", DataMode.Canli), default);

        var record = await context.Baglantilar.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("enc:gizli", record.SifreSifreli);   // düz metin saklanmadı
        Assert.Equal(TenantA, record.TenantId);           // tenant damgalandı
        Assert.Equal(DataMode.Canli, ozet.Mod);
        Assert.Null(ozet.SonTestBasarili);               // test edilmemiş
    }

    [Fact]
    public async Task liste_yalnizca_kendi_tenant_baglantilarini_dondurur()
    {
        var baglanti = new SqliteConnection("DataSource=:memory:");
        await baglanti.OpenAsync();
        await using var _ = baglanti;

        await using (var kur = Baglam(baglanti, TenantA))
        {
            await kur.Database.EnsureCreatedAsync();
            kur.Baglantilar.AddRange(
                new Connection { TenantId = TenantA, Ad = "A", Sunucu = "s", Veritabani = "d" },
                new Connection { TenantId = TenantB, Ad = "B", Sunucu = "s", Veritabani = "d" });
            await kur.SaveChangesAsync();
        }

        await using var a = Baglam(baglanti, TenantA);
        var liste = await new ListConnectionsHandler(a).Handle(new ListConnectionsRequest(), default);
        Assert.Equal("A", Assert.Single(liste).Ad);
    }

    [Fact]
    public async Task guncelle_bos_sifrede_mevcut_parolayi_korur_yeni_sifrede_degistirir()
    {
        var baglanti = new SqliteConnection("DataSource=:memory:");
        await baglanti.OpenAsync();
        await using var _ = baglanti;
        await using var context = Baglam(baglanti, TenantA);
        await context.Database.EnsureCreatedAsync();
        var koruma = new SahteKoruma();

        var ozet = await new AddConnectionHandler(context, koruma)
            .Handle(new AddConnectionRequest("Ad", "s", "d", "u", "ilk", DataMode.Canli), default);

        // Boş şifre → existing parola korunur, diğer alanlar güncellenir
        await new UpdateConnectionHandler(context, koruma)
            .Handle(new UpdateConnectionRequest(ozet.Id, "Ad2", "s", "d", "u", null, DataMode.IceAlim), default);
        var k1 = await context.Baglantilar.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("enc:ilk", k1.SifreSifreli);
        Assert.Equal("Ad2", k1.Ad);
        Assert.Equal(DataMode.IceAlim, k1.Mod);

        // Yeni şifre → değişir
        await new UpdateConnectionHandler(context, koruma)
            .Handle(new UpdateConnectionRequest(ozet.Id, "Ad2", "s", "d", "u", "newEntity", DataMode.IceAlim), default);
        var k2 = await context.Baglantilar.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("enc:newEntity", k2.SifreSifreli);
    }

    [Fact]
    public async Task test_by_id_sonucu_baglantiya_islenir()
    {
        var baglanti = new SqliteConnection("DataSource=:memory:");
        await baglanti.OpenAsync();
        await using var _ = baglanti;
        await using var context = Baglam(baglanti, TenantA);
        await context.Database.EnsureCreatedAsync();
        var koruma = new SahteKoruma();

        var ozet = await new AddConnectionHandler(context, koruma)
            .Handle(new AddConnectionRequest("Ad", "s", "d", "u", "p", DataMode.Canli), default);

        var result = await new TestConnectionByIdHandler(context, new SahteDenetci(true), koruma)
            .Handle(new TestConnectionByIdRequest(ozet.Id), default);

        Assert.True(result.Basarili);
        var record = await context.Baglantilar.IgnoreQueryFilters().SingleAsync();
        Assert.True(record.SonTestBasarili);   // sonuç bağlantıya işlendi
        Assert.Equal(5, record.SonTestMs);
        Assert.NotNull(record.SonTestTarihi);
    }

    [Fact]
    public async Task sil_bulunmayanda_false_bulunani_siler()
    {
        var baglanti = new SqliteConnection("DataSource=:memory:");
        await baglanti.OpenAsync();
        await using var _ = baglanti;
        await using var context = Baglam(baglanti, TenantA);
        await context.Database.EnsureCreatedAsync();

        Assert.False(await new DeleteConnectionHandler(context).Handle(new DeleteConnectionRequest(Guid.NewGuid()), default));

        var ozet = await new AddConnectionHandler(context, new SahteKoruma())
            .Handle(new AddConnectionRequest("Ad", "s", "d", null, null, DataMode.Canli), default);
        Assert.True(await new DeleteConnectionHandler(context).Handle(new DeleteConnectionRequest(ozet.Id), default));
        Assert.Equal(0, await context.Baglantilar.IgnoreQueryFilters().CountAsync());
    }
}
