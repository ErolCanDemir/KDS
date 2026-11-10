using Kds.Business.Metrics;
using Kds.Contracts.QueryModel;
using Kds.Data;
using Kds.Data.Entities;
using Kds.Execution;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S22 metrik çözücüsü: modeldeki metrik REFERANSI koşum anında katalogdan formüle çözülür.
/// Kritik davranışlar: (a) çözüm her koşumda — metrik güncellenince raporlar yeni tanımı kullanır
/// (tek doğruluk kaynağı vaadi); (b) <b>fail-closed</b> — silinmiş/pasif/yanlış-harita metriği
/// sessizce atlanmaz, sorgu açık hatayla durur.</summary>
public sealed class MetrikCozucuTests
{
    private static readonly Guid Kiraci = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Harita = Guid.Parse("22222222-0000-0000-0000-000000000001");

    private static async Task<(KdsDbContext Ctx, SqliteConnection C)> BaglamAsync()
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(Kiraci));
        await ctx.Database.EnsureCreatedAsync();
        ctx.Haritalar.Add(new DataMap { Id = Harita, TenantId = Kiraci, Ad = "H", BaglantiId = Guid.NewGuid() });
        await ctx.SaveChangesAsync();
        return (ctx, c);
    }

    private static Metric Metrik(string ad = "Toplam Ciro", string ifade = "SUM({Odemeler.Tutar})",
        Guid? harita = null, bool aktif = true) => new()
    {
        Id = Guid.NewGuid(), TenantId = Kiraci, Ad = ad, Ifade = ifade,
        VeriHaritasiId = harita ?? Harita, Bicim = "Para", Aktif = aktif,
    };

    private static QueryModel Model(params MetricField[] metrikler) =>
        new(Harita, Alanlar: [], Filtre: null, Siralama: [], SatirLimiti: null, MetrikAlanlar: metrikler);

    [Fact]
    public async Task referans_formule_cozulur_ve_katalog_adi_baslik_olur()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;
        var m = Metrik();
        ctx.Metrikler.Add(m);
        await ctx.SaveChangesAsync();

        var cozulen = await new MetrikCozucu(ctx).CozAsync(Model(new MetricField(m.Id)), default);

        var alan = Assert.Single(cozulen.HesaplananAlanlar!);
        Assert.Equal("SUM({Odemeler.Tutar})", alan.Ifade);
        Assert.Equal("Toplam Ciro", alan.TakmaAd);      // başlık = katalog adı
        Assert.Null(cozulen.MetrikAlanlar);              // referanslar tüketildi; üretici metrik bilmez
    }

    [Fact]
    public async Task guncellenen_metrik_SONRAKI_kosumda_yeni_formulu_kullanir()
    {
        // Tek doğruluk kaynağı vaadinin kendisi: model AYNI kalır, formül katalogdan değişir.
        var (ctx, c) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;
        var m = Metrik();
        ctx.Metrikler.Add(m);
        await ctx.SaveChangesAsync();
        var model = Model(new MetricField(m.Id));
        var cozucu = new MetrikCozucu(ctx);

        var once = await cozucu.CozAsync(model, default);
        m.Ifade = "SUM({Odemeler.Tutar}) - SUM({Iadeler.Tutar})";   // "iptaller düşülsün" kararı
        await ctx.SaveChangesAsync();
        var sonra = await cozucu.CozAsync(model, default);

        Assert.Equal("SUM({Odemeler.Tutar})", once.HesaplananAlanlar![0].Ifade);
        Assert.Equal("SUM({Odemeler.Tutar}) - SUM({Iadeler.Tutar})", sonra.HesaplananAlanlar![0].Ifade);
    }

    [Fact]
    public async Task silinmis_metrik_SESSIZCE_atlanmaz_acik_hata()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;

        var hata = await Assert.ThrowsAsync<QueryModelException>(() =>
            new MetrikCozucu(ctx).CozAsync(Model(new MetricField(Guid.NewGuid())), default));
        Assert.Contains("silinmiş", hata.Message);
    }

    [Fact]
    public async Task pasif_ve_baska_haritanin_metrigi_reddedilir()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;
        var baskaHarita = Guid.NewGuid();
        ctx.Haritalar.Add(new DataMap { Id = baskaHarita, TenantId = Kiraci, Ad = "H2", BaglantiId = Guid.NewGuid() });
        var pasif = Metrik("Pasif", aktif: false);
        var yabanci = Metrik("Yabancı", harita: baskaHarita);
        ctx.Metrikler.AddRange(pasif, yabanci);
        await ctx.SaveChangesAsync();
        var cozucu = new MetrikCozucu(ctx);

        var h1 = await Assert.ThrowsAsync<QueryModelException>(() =>
            cozucu.CozAsync(Model(new MetricField(pasif.Id)), default));
        Assert.Contains("pasife alınmış", h1.Message);
        Assert.Contains("Pasif", h1.Message);           // hangi metrik olduğu söylenir

        var h2 = await Assert.ThrowsAsync<QueryModelException>(() =>
            cozucu.CozAsync(Model(new MetricField(yabanci.Id)), default));
        Assert.Contains("başka bir veri haritasına", h2.Message);
    }

    [Fact]
    public async Task takma_ad_katalog_adini_ezebilir_ve_mevcut_hesaplar_korunur()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;
        var m = Metrik();
        ctx.Metrikler.Add(m);
        await ctx.SaveChangesAsync();

        var model = Model(new MetricField(m.Id, "Ciro (₺)")) with
        {
            HesaplananAlanlar = [new ComputedColumn("{A} - {B}", "Fark")],
        };
        var cozulen = await new MetrikCozucu(ctx).CozAsync(model, default);

        Assert.Equal(2, cozulen.HesaplananAlanlar!.Count);
        Assert.Equal("Fark", cozulen.HesaplananAlanlar[0].TakmaAd);       // Y1 alanı yerinde
        Assert.Equal("Ciro (₺)", cozulen.HesaplananAlanlar[1].TakmaAd);   // takma ad kataloğu ezdi
    }

    [Fact]
    public async Task metriksiz_model_oldugu_gibi_gecer()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;
        var model = Model() with { MetrikAlanlar = null };
        Assert.Same(model, await new MetrikCozucu(ctx).CozAsync(model, default));
    }
}
