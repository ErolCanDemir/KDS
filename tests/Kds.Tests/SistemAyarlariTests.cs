using Kds.Business.Settings;
using Kds.Data;
using Kds.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Kds.Tests;

/// <summary>S16/A3 — Sistem Ayarları: değer sırası (kiracıya özel → kurulum geneli → koddaki
/// varsayılan) ve yazım kuralı (varsayılana eşitlenen ayar SATIRI SİLİNİR — "varsayılan" durumu
/// satırın yokluğuyla temsil edilir).</summary>
public sealed class SistemAyarlariTests
{
    private static readonly Guid KiraciA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    /// <summary>Sahte koruyucu: gerçek Data Protection yerine görünür bir dönüşüm — böylece
    /// "diskte ham parola durmuyor" iddiası testte GÖRÜLEBİLİR (kimlik dönüşümü bunu kanıtlamazdı).</summary>
    private sealed class SahteKoruyucu : Kds.Business.Connections.IPasswordProtector
    {
        // Base64: düz metin çıktının İÇİNDE GEÇMEZ → "diskte ham parola yok" iddiası sınanabilir.
        public string Protect(string duzMetin) =>
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(duzMetin));
        public string Unprotect(string sifreli) =>
            System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(sifreli));
    }

    private static async Task<(KdsDbContext Ctx, SqliteConnection Baglanti)> BaglamAsync()
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(KiraciA));
        await ctx.Database.EnsureCreatedAsync();
        return (ctx, c);
    }

    private static SistemAyarlari Ayarlar(KdsDbContext ctx) =>
        new(ctx, new MemoryCache(new MemoryCacheOptions()), new FixedTenantContext(KiraciA));

    [Fact]
    public async Task kayit_yoksa_koddaki_varsayilan_gecerlidir()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _ = c;
        await using var __ = ctx;

        Assert.Equal(60, Ayarlar(ctx).Sayi(AyarTanimlari.SorguZamanAsimiSn));
        Assert.Equal(10000, Ayarlar(ctx).Sayi(AyarTanimlari.SorguEnCokSatir));
    }

    [Fact]
    public async Task kiraci_ayari_kurulum_genelini_ezer()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _ = c;
        await using var __ = ctx;

        ctx.SistemAyarlari.AddRange(
            new SystemSetting { Anahtar = AyarTanimlari.SorguZamanAsimiSn, Deger = "90", TenantId = null },
            new SystemSetting { Anahtar = AyarTanimlari.SorguZamanAsimiSn, Deger = "120", TenantId = KiraciA });
        await ctx.SaveChangesAsync();

        Assert.Equal(120, Ayarlar(ctx).Sayi(AyarTanimlari.SorguZamanAsimiSn));
    }

    [Fact]
    public async Task kiraci_ayari_yoksa_kurulum_geneli_gecerlidir()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _ = c;
        await using var __ = ctx;

        ctx.SistemAyarlari.Add(new SystemSetting
        {
            Anahtar = AyarTanimlari.TaniTaramaEsigi, Deger = "250000", TenantId = null,
        });
        await ctx.SaveChangesAsync();

        Assert.Equal(250000, Ayarlar(ctx).Sayi(AyarTanimlari.TaniTaramaEsigi));
    }

    [Fact]
    public async Task sir_ayari_sifreli_saklanir_listede_maskelenir_okumada_cozulur()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _ = c;
        await using var __ = ctx;
        var onbellek = new MemoryCache(new MemoryCacheOptions());
        var koruma = new SahteKoruyucu();
        var yazar = new SaveSettingsHandler(ctx, onbellek, koruma);

        await yazar.Handle(new SaveSettingsRequest(new Dictionary<string, string>
        {
            [AyarTanimlari.EpostaParola] = "gizli123",
        }), default);

        // Diskte ham parola DURMAZ (koruyucudan geçmiştir).
        var satir = await ctx.SistemAyarlari.SingleAsync();
        Assert.True(satir.SirMi);
        Assert.DoesNotContain("gizli123", satir.Deger, StringComparison.Ordinal);
        Assert.Equal(koruma.Protect("gizli123"), satir.Deger);

        // Okuma çözülmüş değeri verir…
        var okuyucu = new SistemAyarlari(ctx, new MemoryCache(new MemoryCacheOptions()),
            new FixedTenantContext(KiraciA), koruma);
        Assert.Equal("gizli123", okuyucu.Metin(AyarTanimlari.EpostaParola));

        // …ama ekrana giden listede değer BOŞ, yalnız "dolu" bilgisi vardır.
        var liste = await new ListSettingsHandler(ctx, new FixedTenantContext(KiraciA))
            .Handle(new ListSettingsRequest(), default);
        var parola = liste.Single(a => a.Anahtar == AyarTanimlari.EpostaParola);
        Assert.Equal(string.Empty, parola.Deger);
        Assert.True(parola.Dolu);
    }

    [Fact]
    public async Task depo_ayari_kurulum_degerini_ezer_db_adi_desenden_uretilir()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _ = c;
        await using var __ = ctx;
        var kurulum = new Kds.Business.Ingest.IceAlimAyarlari { DepoSunucuBaglantisi = "Server=KURULUM;" };

        // Ayar boşken kurulumdaki (env/kurulum.json) değer geçerli.
        var cozucu = new Kds.Business.Ingest.DepoCozucu(kurulum, Ayarlar(ctx));
        Assert.Equal("Server=KURULUM;", cozucu.SunucuDizesi());
        Assert.Equal("KdsDepo_" + KiraciA.ToString("N"), cozucu.DbAdi(KiraciA));

        ctx.SistemAyarlari.AddRange(
            new SystemSetting { Anahtar = AyarTanimlari.DepoSunucuBaglantisi, Deger = "Server=AYAR;", SirMi = true },
            new SystemSetting { Anahtar = AyarTanimlari.DepoDbAdiDeseni, Deger = "Musteri_{kiraci}_Depo" });
        await ctx.SaveChangesAsync();

        var yeni = new Kds.Business.Ingest.DepoCozucu(kurulum, Ayarlar(ctx));
        Assert.Equal("Server=AYAR;", yeni.SunucuDizesi());
        Assert.Equal($"Musteri_{KiraciA:N}_Depo", yeni.DbAdi(KiraciA));
    }

    [Fact]
    public async Task varsayilana_esitlenen_ayar_satiri_silinir_gecersiz_deger_reddedilir()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _ = c;
        await using var __ = ctx;
        var onbellek = new MemoryCache(new MemoryCacheOptions());
        var yazar = new SaveSettingsHandler(ctx, onbellek);

        await yazar.Handle(new SaveSettingsRequest(
            new Dictionary<string, string> { [AyarTanimlari.SorguZamanAsimiSn] = "90" }), default);
        Assert.Equal("90", (await ctx.SistemAyarlari.SingleAsync()).Deger);

        // Varsayılana geri dönüş → satır silinir (ayar tablosu şişmez).
        await yazar.Handle(new SaveSettingsRequest(
            new Dictionary<string, string> { [AyarTanimlari.SorguZamanAsimiSn] = "60" }), default);
        Assert.Empty(await ctx.SistemAyarlari.ToListAsync());

        // Sayı beklenen ayara metin/negatif kabul edilmez.
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => yazar.Handle(
            new SaveSettingsRequest(new Dictionary<string, string> { [AyarTanimlari.SorguZamanAsimiSn] = "-5" }), default));
    }
}
