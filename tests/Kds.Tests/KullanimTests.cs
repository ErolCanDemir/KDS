using Kds.Business.Settings;
using Kds.Business.Usage;
using Kds.Contracts.Usage;
using Kds.Data;
using Kds.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S41 — Yönetim → Kullanım. Sınanan asıl kararlar: (1) "yavaş" eşiği burada TANIMLANMAZ,
/// Sistem Ayarları'ndan okunur; (2) rapor kullanımı yalnız RAPOR üzerinden gelen koşumları sayar;
/// (3) hiç koşmamış raporlar ayrıca sorulur — sıfır koşum gruplamada GÖRÜNMEZ.
///
/// <para><b>Neden SQLite değil LocalDB:</b> sorgu <c>OlusturmaTarihi >= baslangic</c> ile dönem
/// süzüyor ve EF Core'un SQLite sağlayıcısı <c>DateTimeOffset</c> KARŞILAŞTIRMASINI çeviremiyor
/// ("could not be translated"). Üretimde hedef SQL Server olduğu için testi gerçek sağlayıcıda
/// koşmak hem tek geçerli yol hem de daha dürüst bir doğrulama. Her test kendi geçici veritabanını
/// yaratır ve siler.</para></summary>
public class KullanimTests : IDisposable
{
    private readonly string dbAdi = "KdsKullanimIT_" + Guid.NewGuid().ToString("N")[..12];
    private string Dize() =>
        $@"Server=(localdb)\MSSQLLocalDB;Database={dbAdi};Trusted_Connection=True;TrustServerCertificate=True";

    public void Dispose()
    {
        using var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlServer(Dize()).Options,
            new FixedTenantContext(Kiraci));
        ctx.Database.EnsureDeleted();
        GC.SuppressFinalize(this);
    }

    private static readonly Guid Kiraci = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid RaporA = Guid.Parse("aaaa1111-0000-0000-0000-000000000001");
    private static readonly Guid RaporB = Guid.Parse("aaaa1111-0000-0000-0000-000000000002");
    private static readonly Guid OluRaporId = Guid.Parse("aaaa1111-0000-0000-0000-000000000003");

    /// <summary>Yalnız "yavaş sorgu" eşiğini veren sahte ayar kaynağı — gerçek ayar zinciri
    /// (kiracı → kurulum → varsayılan) burada sınanmıyor, o kendi testlerinde.</summary>
    private sealed class SahteAyarlar(int yavasEsikMs) : ISistemAyarlari
    {
        public string Metin(string anahtar) => "";
        public bool EvetHayir(string anahtar) => false;
        public int Sayi(string anahtar) =>
            anahtar == AyarTanimlari.TaniYavasEsikMs ? yavasEsikMs : 0;
    }

    private async Task<KdsDbContext> BaglamAsync(Guid? kiraci = null)
    {
        var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlServer(Dize()).Options,
            new FixedTenantContext(kiraci ?? Kiraci));
        await ctx.Database.EnsureCreatedAsync();

        ctx.Raporlar.AddRange(
            new Report { Id = RaporA, TenantId = Kiraci, Ad = "Günlük Ciro" },
            new Report { Id = RaporB, TenantId = Kiraci, Ad = "Servis Kırılımı" },
            new Report { Id = OluRaporId, TenantId = Kiraci, Ad = "Kimsenin Açmadığı Rapor" });

        var simdi = DateTimeOffset.UtcNow;
        ctx.DenetimKayitlari.AddRange(
            // RaporA: 3 koşum, biri YAVAŞ (12 sn) ve biri başarısız
            new AuditRecord { TenantId = Kiraci, RaporId = RaporA, Sql = "SELECT 1", Basarili = true, SureMs = 100, OlusturmaTarihi = simdi.AddDays(-1) },
            new AuditRecord { TenantId = Kiraci, RaporId = RaporA, Sql = "SELECT 2", Basarili = true, SureMs = 12_000, OlusturmaTarihi = simdi.AddDays(-2) },
            new AuditRecord { TenantId = Kiraci, RaporId = RaporA, Sql = "SELECT 3", Basarili = false, SureMs = 50, OlusturmaTarihi = simdi.AddDays(-3) },
            // RaporB: tek koşum
            new AuditRecord { TenantId = Kiraci, RaporId = RaporB, Sql = "SELECT 4", Basarili = true, SureMs = 200, OlusturmaTarihi = simdi.AddDays(-1) },
            // Rapora BAĞLI OLMAYAN koşum (tasarımcı/SQL editörü) — rapor kullanımına sayılmamalı
            new AuditRecord { TenantId = Kiraci, RaporId = null, Sql = "SELECT 5", Basarili = true, SureMs = 300, OlusturmaTarihi = simdi.AddDays(-1) },
            // DÖNEM DIŞI (60 gün önce) — hiçbir sayıma girmemeli
            new AuditRecord { TenantId = Kiraci, RaporId = RaporB, Sql = "SELECT 6", Basarili = true, SureMs = 90_000, OlusturmaTarihi = simdi.AddDays(-60) });

        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task ozet_donem_icini_sayar_ve_esigi_AYARLARDAN_okur()
    {
        await using var ctx = await BaglamAsync();

        var s = await new GetUsageHandler(ctx, new SahteAyarlar(10_000))
            .Handle(new GetUsageRequest(30), CancellationToken.None);

        Assert.Equal(5, s.Ozet.ToplamKosum);          // 60 gün önceki kayıt HARİÇ
        Assert.Equal(1, s.Ozet.BasarisizKosum);
        Assert.Equal(1, s.Ozet.YavasKosum);           // yalnız 12 sn'lik
        Assert.Equal(10_000, s.Ozet.YavasEsikMs);     // eşik burada tanımlanmadı, ayardan geldi
        Assert.Equal(12_000, s.Ozet.EnUzunSureMs);
    }

    [Fact]
    public async Task esik_degisince_yavas_sayisi_DEGISIR()
    {
        // Eşiğin gerçekten kullanıldığını kanıtlar: aynı veri, farklı ayar → farklı sonuç.
        await using var ctx = await BaglamAsync();

        var s = await new GetUsageHandler(ctx, new SahteAyarlar(150))
            .Handle(new GetUsageRequest(30), CancellationToken.None);

        Assert.Equal(3, s.Ozet.YavasKosum);           // 12000, 200, 300 ms
        Assert.Equal(3, s.YavasKosumlar.Count);
        Assert.Equal(12_000, s.YavasKosumlar[0].SureMs);   // en yavaş önce
        Assert.Equal("Günlük Ciro", s.YavasKosumlar[0].RaporAd);
    }

    [Fact]
    public async Task rapor_kullanimi_yalniz_RAPOR_kosumlarini_sayar()
    {
        await using var ctx = await BaglamAsync();

        var s = await new GetUsageHandler(ctx, new SahteAyarlar(10_000))
            .Handle(new GetUsageRequest(30), CancellationToken.None);

        var a = Assert.Single(s.RaporKullanimlari, r => r.RaporId == RaporA);
        Assert.Equal(3, a.KosumSayisi);
        Assert.Equal(1, a.BasarisizSayisi);
        Assert.Equal("Günlük Ciro", a.Ad);
        // RaporId'siz koşum (tasarımcı/SQL editörü) hiçbir rapora yazılmadı.
        Assert.Equal(2, s.RaporKullanimlari.Count);
        Assert.Equal(RaporA, s.RaporKullanimlari[0].RaporId);   // en çok koşan önce
    }

    [Fact]
    public async Task SILINMIS_raporun_kosumlari_listede_gorunmez()
    {
        /* Canlı ekranda liste "(silinmiş rapor)" satırlarıyla dolmuş ve asıl raporları gizlemişti
         * (26 Ağu 2026). Silinmiş bir raporun kullanım sayısı kullanıcıya hiçbir şey kazandırmaz —
         * açılamaz, düzeltilemez. ÖZET sayaçları yine tüm koşumları sayar; rakam kaybolmaz. */
        await using var ctx = await BaglamAsync();
        ctx.Raporlar.Remove(await ctx.Raporlar.FirstAsync(r => r.Id == RaporA));
        await ctx.SaveChangesAsync();

        var s = await new GetUsageHandler(ctx, new SahteAyarlar(10_000))
            .Handle(new GetUsageRequest(30), CancellationToken.None);

        Assert.DoesNotContain(s.RaporKullanimlari, r => r.RaporId == RaporA);
        Assert.Equal(5, s.Ozet.ToplamKosum);   // özet DEĞİŞMEZ: koşumlar gerçekten oldu
    }

    [Fact]
    public async Task hic_kosmayan_rapor_OLU_listesinde_cikar()
    {
        /* Sıfır koşum, gruplamanın sonucunda HİÇ görünmez — "az kullanılan içerik" sorusunun asıl
         * muhatabı ise tam olarak bunlar. Bu yüzden ayrıca sorulur. */
        await using var ctx = await BaglamAsync();

        var s = await new GetUsageHandler(ctx, new SahteAyarlar(10_000))
            .Handle(new GetUsageRequest(30), CancellationToken.None);

        var olu = Assert.Single(s.OluRaporlar);
        Assert.Equal(OluRaporId, olu.Id);
        Assert.Equal("Kimsenin Açmadığı Rapor", olu.Ad);
    }

    [Fact]
    public async Task dar_donemde_kosmayan_rapor_da_OLU_sayilir()
    {
        // Dönem 1 güne inince 2-3 gün önce koşan RaporA da "bu dönemde ölü" olur: ölülük
        // MUTLAK değil, seçilen döneme GÖREdir. (Son koşum tarihi listede zaten görünür.)
        await using var ctx = await BaglamAsync();

        var s = await new GetUsageHandler(ctx, new SahteAyarlar(10_000))
            .Handle(new GetUsageRequest(1), CancellationToken.None);

        Assert.Equal(1, s.GunSayisi);
        Assert.Contains(s.OluRaporlar, r => r.Id == OluRaporId);
        Assert.All(s.RaporKullanimlari, r => Assert.Equal(1, r.KosumSayisi));   // yalnız dünkü koşumlar
    }

    [Fact]
    public async Task gun_sayisi_sinirlar_icine_kirpilir()
    {
        await using var ctx = await BaglamAsync();
        var h = new GetUsageHandler(ctx, new SahteAyarlar(10_000));

        Assert.Equal(365, (await h.Handle(new GetUsageRequest(9999), CancellationToken.None)).GunSayisi);
        Assert.Equal(1, (await h.Handle(new GetUsageRequest(0), CancellationToken.None)).GunSayisi);
        Assert.Equal(1, (await h.Handle(new GetUsageRequest(-5), CancellationToken.None)).GunSayisi);
    }

    [Fact]
    public async Task kayit_yoksa_ozet_SIFIRLARLA_doner()
    {
        /* Boş kiracıda ekran çökmemeli: ortalama/en uzun hesabı boş kümede patlar. Veri NORMAL
         * kiracıyla yazılır, sorgu BAŞKA kiracının bağlamından koşar — böylece hem boş küme hem
         * de kiracı izolasyonu aynı anda sınanır. (Veriyi yeni kiracıyla yazsaydık DbContext
         * eklemede TenantId'yi ona atar ve küme boş kalmazdı.) */
        await using var ctx = await BaglamAsync();
        await using var yabanci = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlServer(Dize()).Options,
            new FixedTenantContext(Guid.NewGuid()));

        var s = await new GetUsageHandler(yabanci, new SahteAyarlar(10_000))
            .Handle(new GetUsageRequest(30), CancellationToken.None);

        Assert.Equal(0, s.Ozet.ToplamKosum);
        Assert.Equal(0, s.Ozet.OrtalamaSureMs);
        Assert.Empty(s.YavasKosumlar);
        Assert.Empty(s.RaporKullanimlari);
    }
}
