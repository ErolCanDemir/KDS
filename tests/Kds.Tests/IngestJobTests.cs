using Kds.Business.Ingest;
using Kds.Contracts.Ingest;
using Kds.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S12 İçe alım işi CRUD (SQLite): kaydet/getir tabloları+zamanlamayı korur; sil.
/// (Gerçek çalıştırma/SqlBulkCopy canlı MSSQL ister → E2E/elle kanıtta.)</summary>
public sealed class IngestJobTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static KdsDbContext Baglam(SqliteConnection c) =>
        new(new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(TenantA));

    [Fact]
    public async Task kaydet_getir_tablolari_ve_zamanlamayi_korur()
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var _ = c;
        await using var ctx = Baglam(c);
        await ctx.Database.EnsureCreatedAsync();

        var ozet = await new SaveIngestJobHandler(ctx).Handle(new SaveIngestJobRequest(
            null, "QMS Gecelik", Guid.NewGuid(),
            [new IngestTableDto("dbo", "Kisiler", null), new IngestTableDto("dbo", "Talepler", ["Id", "Durum"])],
            "0 3 * * *", true), default);

        Assert.Equal(2, ozet.TabloSayisi);
        Assert.True(ozet.Aktif);

        var detay = await new GetIngestJobHandler(ctx).Handle(new GetIngestJobRequest(ozet.Id), default);
        Assert.NotNull(detay);
        Assert.Equal("0 3 * * *", detay!.Zamanlama);
        Assert.Null(detay.Tablolar[0].Kolonlar);                            // tüm kolonlar (bulk)
        Assert.Equal(new[] { "Id", "Durum" }, detay.Tablolar[1].Kolonlar);  // seçili kolonlar (curated)
    }

    // ---- S44: artımlı yükleme ----

    [Fact]
    public async Task artimli_kolon_kayit_round_tripte_korunur()
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var _ = c;
        await using var ctx = Baglam(c);
        await ctx.Database.EnsureCreatedAsync();

        var ozet = await new SaveIngestJobHandler(ctx).Handle(new SaveIngestJobRequest(
            null, "Artımlı", Guid.NewGuid(),
            [new IngestTableDto("dbo", "Olaylar", null, "Rv"), new IngestTableDto("dbo", "Sabit", null)],
            null, true), default);

        Assert.Equal(1, ozet.ArtimliTabloSayisi);   // özet karttaki rozet için sayar

        var detay = await new GetIngestJobHandler(ctx).Handle(new GetIngestJobRequest(ozet.Id), default);
        Assert.Equal("Rv", detay!.Tablolar[0].ArtimliKolon);
        Assert.Null(detay.Tablolar[1].ArtimliKolon);
    }

    [Fact]
    public async Task artimli_kolon_degisince_su_seviyesi_duser_ayni_kalinca_korunur()
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var _ = c;
        await using var ctx = Baglam(c);
        await ctx.Database.EnsureCreatedAsync();

        var ozet = await new SaveIngestJobHandler(ctx).Handle(new SaveIngestJobRequest(
            null, "X", Guid.NewGuid(),
            [new IngestTableDto("dbo", "A", null, "Rv"), new IngestTableDto("dbo", "B", null, "Id")],
            null, true), default);

        // Koşucunun yazacağı su seviyelerini taklit et.
        var job = await ctx.IceAlimIsleri.FirstAsync(x => x.Id == ozet.Id);
        job.ArtimliDurumJson = """{"dbo.A":"0x01","dbo.B":"42"}""";
        await ctx.SaveChangesAsync();

        // A'nın artımlı kolonu DEĞİŞTİ (Rv→Tarih) → seviyesi düşmeli (eski kolonun değeriyle yeni
        // kolonu karşılaştırmak satır atlatırdı); B aynı kaldı → seviyesi korunmalı.
        await new SaveIngestJobHandler(ctx).Handle(new SaveIngestJobRequest(
            ozet.Id, "X", job.BaglantiId,
            [new IngestTableDto("dbo", "A", null, "Tarih"), new IngestTableDto("dbo", "B", null, "Id")],
            null, true), default);

        var seviyeler = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            (await ctx.IceAlimIsleri.FirstAsync(x => x.Id == ozet.Id)).ArtimliDurumJson!)!;
        Assert.False(seviyeler.ContainsKey("dbo.A"));
        Assert.Equal("42", seviyeler["dbo.B"]);
    }

    [Fact]
    public void artimli_kolon_secili_kolonlarin_disindaysa_dogrulama_reddeder()
    {
        var dogrulayici = new SaveIngestJobValidation();

        var kotu = dogrulayici.Validate(new SaveIngestJobRequest(null, "X", Guid.NewGuid(),
            [new IngestTableDto("dbo", "T", ["Id", "Ad"], "Tarih")], null, true));
        Assert.Contains(kotu.Errors, e => e.ErrorMessage.Contains("Artımlı kolon"));

        // Kolon seçimi yoksa (tüm kolonlar) artımlı kolon serbesttir; seçilenler içindeyse de geçer.
        Assert.True(dogrulayici.Validate(new SaveIngestJobRequest(null, "X", Guid.NewGuid(),
            [new IngestTableDto("dbo", "T", null, "Tarih")], null, true)).IsValid);
        Assert.True(dogrulayici.Validate(new SaveIngestJobRequest(null, "X", Guid.NewGuid(),
            [new IngestTableDto("dbo", "T", ["Id", "Tarih"], "Tarih")], null, true)).IsValid);
    }

    [Fact]
    public async Task sil_bulunmayanda_false_bulunani_siler()
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var _ = c;
        await using var ctx = Baglam(c);
        await ctx.Database.EnsureCreatedAsync();

        Assert.False(await new DeleteIngestJobHandler(ctx).Handle(new DeleteIngestJobRequest(Guid.NewGuid()), default));

        var ozet = await new SaveIngestJobHandler(ctx).Handle(new SaveIngestJobRequest(
            null, "X", Guid.NewGuid(), [new IngestTableDto("dbo", "T", null)], null, true), default);
        Assert.True(await new DeleteIngestJobHandler(ctx).Handle(new DeleteIngestJobRequest(ozet.Id), default));
        Assert.Equal(0, await ctx.IceAlimIsleri.IgnoreQueryFilters().CountAsync());
    }
}
