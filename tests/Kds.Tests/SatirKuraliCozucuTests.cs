using Kds.Business.Security;
using Kds.Contracts.Identity;
using Kds.Contracts.QueryModel;
using Kds.Data;
using Kds.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>RLS çözücüsü: hangi kural KİME uygulanır ve değer nereden gelir. Kritik davranış
/// <b>fail-closed</b>: kural uygulanacakken değeri çözülemiyorsa sorgu çalıştırılmaz — filtresiz
/// koşmak (kullanıcının her şeyi görmesi) sessiz bir veri sızıntısı olurdu.</summary>
public sealed class SatirKuraliCozucuTests
{
    private static readonly Guid Kiraci = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Harita = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid Tablo = Guid.Parse("33333333-0000-0000-0000-000000000001");

    private static async Task<(KdsDbContext Ctx, SqliteConnection Baglanti)> BaglamAsync()
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(Kiraci));
        await ctx.Database.EnsureCreatedAsync();
        // Kural bir HARİTA TABLOSUNA bağlıdır (FK) — önce onlar var olmalı.
        ctx.Haritalar.Add(new DataMap
        {
            Id = Harita, TenantId = Kiraci, Ad = "Harita", BaglantiId = Guid.NewGuid(),
            Tablolar = [new DataMapTable { Id = Tablo, VeriHaritasiId = Harita, Sema = "talep", HamAd = "Talepler", DostcaAd = "Talepler" }],
        });
        await ctx.SaveChangesAsync();
        return (ctx, c);
    }

    private static RowRule Kural(string ad, KuralDegerKaynagi kaynak, string? deger, string? roller = null) => new()
    {
        Id = Guid.NewGuid(), TenantId = Kiraci, VeriHaritasiId = Harita, HaritaTabloId = Tablo,
        Ad = ad, Kolon = "ServisId", Op = KarsilastirmaOp.Esit,
        DegerKaynagi = kaynak, Deger = deger, Roller = roller, Aktif = true,
    };

    private static CalistiranKullanici Kullanici(
        string rol = Roles.Musteri, string? ad = "bayi1", string? eposta = "bayi1@x.com",
        Dictionary<string, string>? iddialar = null) =>
        new([rol], ad, eposta, iddialar ?? []);

    [Fact]
    public async Task token_iddiasindan_deger_cozulur()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _ = c; await using var __ = ctx;
        ctx.SatirKurallari.Add(Kural("Bayi kapsamı", KuralDegerKaynagi.TokenIddiasi, "servis_kodu"));
        await ctx.SaveChangesAsync();

        var kurallar = await new SatirKuraliCozucu(ctx).CozAsync(
            Harita, Kullanici(iddialar: new() { ["servis_kodu"] = "42" }), default);

        var k = Assert.Single(kurallar);
        Assert.Equal(Tablo, k.TabloId);
        Assert.Equal("ServisId", k.Kolon);
        Assert.Equal(["42"], k.Degerler);
        Assert.Equal("Bayi kapsamı", k.KuralAdi);
    }

    [Fact]
    public async Task iddia_yoksa_sorgu_calistirilmaz_fail_closed()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _ = c; await using var __ = ctx;
        ctx.SatirKurallari.Add(Kural("Bayi kapsamı", KuralDegerKaynagi.TokenIddiasi, "servis_kodu"));
        await ctx.SaveChangesAsync();

        // Kullanıcının kimliğinde beklenen iddia YOK → filtresiz koşmak yerine AÇIKÇA reddet.
        var hata = await Assert.ThrowsAsync<SatirKuraliHatasi>(() =>
            new SatirKuraliCozucu(ctx).CozAsync(Harita, Kullanici(), default));
        Assert.Contains("servis_kodu", hata.Message);
        Assert.Contains("ÇALIŞTIRILMADI", hata.Message);
    }

    [Fact]
    public async Task admin_kurallardan_muaftir()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _ = c; await using var __ = ctx;
        ctx.SatirKurallari.Add(Kural("Bayi kapsamı", KuralDegerKaynagi.TokenIddiasi, "servis_kodu"));
        await ctx.SaveChangesAsync();
        var cozucu = new SatirKuraliCozucu(ctx);

        // Admin: kural uygulanmaz VE fail-closed tetiklenmez (iddiası olmasa bile).
        Assert.Empty(await cozucu.CozAsync(Harita, Kullanici(Roles.Admin), default));
        Assert.False(await cozucu.KuralaTabiMiAsync(Kullanici(Roles.Admin), default));
        Assert.True(await cozucu.KuralaTabiMiAsync(Kullanici(), default));
    }

    [Fact]
    public async Task kural_yalniz_hedef_rollere_uygulanir()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _ = c; await using var __ = ctx;
        ctx.SatirKurallari.Add(Kural("Yalnız müşteri", KuralDegerKaynagi.SabitDeger, "7", roller: Roles.Musteri));
        await ctx.SaveChangesAsync();
        var cozucu = new SatirKuraliCozucu(ctx);

        Assert.Single(await cozucu.CozAsync(Harita, Kullanici(Roles.Musteri), default));
        Assert.Empty(await cozucu.CozAsync(Harita, Kullanici(Roles.Gelistirici), default));
    }

    [Fact]
    public async Task cok_degerli_iddia_ve_kullanici_adi_desteklenir()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _ = c; await using var __ = ctx;
        ctx.SatirKurallari.Add(Kural("Çoklu servis", KuralDegerKaynagi.TokenIddiasi, "servis_kodu"));
        ctx.SatirKurallari.Add(Kural("Kendi kayıtları", KuralDegerKaynagi.KullaniciAdi, null));
        await ctx.SaveChangesAsync();

        var kurallar = await new SatirKuraliCozucu(ctx).CozAsync(
            Harita, Kullanici(iddialar: new() { ["servis_kodu"] = "12, 34" }), default);

        Assert.Equal(2, kurallar.Count);
        Assert.Contains(kurallar, k => k.Degerler.SequenceEqual<object?>(["12", "34"]));
        Assert.Contains(kurallar, k => k.Degerler.SequenceEqual<object?>(["bayi1"]));
    }

    [Fact]
    public async Task pasif_kural_ve_baska_haritanin_kurali_uygulanmaz()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _ = c; await using var __ = ctx;
        var pasif = Kural("Pasif", KuralDegerKaynagi.SabitDeger, "1");
        pasif.Aktif = false;
        // İkinci (gerçek) harita: kuralı oraya bağlı olduğu için BU haritanın sorgusunu etkilememeli.
        var ikinciHarita = Guid.NewGuid();
        var ikinciTablo = Guid.NewGuid();
        ctx.Haritalar.Add(new DataMap
        {
            Id = ikinciHarita, TenantId = Kiraci, Ad = "Harita2", BaglantiId = Guid.NewGuid(),
            Tablolar = [new DataMapTable { Id = ikinciTablo, VeriHaritasiId = ikinciHarita, Sema = "dbo", HamAd = "X", DostcaAd = "X" }],
        });
        var baskaHarita = Kural("Başka harita", KuralDegerKaynagi.SabitDeger, "1");
        baskaHarita.VeriHaritasiId = ikinciHarita;
        baskaHarita.HaritaTabloId = ikinciTablo;
        ctx.SatirKurallari.AddRange(pasif, baskaHarita);
        await ctx.SaveChangesAsync();

        Assert.Empty(await new SatirKuraliCozucu(ctx).CozAsync(Harita, Kullanici(), default));
    }
}

/// <summary>Kuralsız çözücü — RLS'i ilgilendirmeyen testlerde kullanılır (varsayılan: kural yok).</summary>
public sealed class KuralsizCozucu : Kds.Business.Security.ISatirKuraliCozucu
{
    public Task<IReadOnlyList<RowFilter>> CozAsync(Guid veriHaritasiId, CalistiranKullanici kullanici, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<RowFilter>>([]);

    public Task<bool> KuralaTabiMiAsync(CalistiranKullanici kullanici, CancellationToken ct) => Task.FromResult(false);

    public Task<bool> VeriKuralliMiAsync(Guid? veriHaritasiId, CancellationToken ct) => Task.FromResult(false);
}

/// <summary>Kimlik taşımayan sahte kullanıcı bağlamı (RLS'siz testler için).</summary>
public sealed class SahteKullanici : Kds.Business.Identity.IUserContext
{
    public Guid? KeycloakId => null;
    public bool IsAdmin => false;
    public bool CanDesign => true;
}
