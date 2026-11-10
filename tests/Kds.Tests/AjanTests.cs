using Kds.Business.Agents;
using Kds.Business.Connections;
using Kds.Business.DataMaps;
using Kds.Business.Embedding;
using Kds.Business.Identity;
using Kds.Business.Ingest;
using Kds.Business.Settings;
using Kds.Contracts.Agents;
using Kds.Contracts.Ingest;
using Kds.Data;
using Kds.Data.Entities;
using Kds.Execution;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S45 KDS Ajanı — handler katmanı (SQLite): anahtar yaşam döngüsü (üret · iptal · ret),
/// iş dağıtımı (yalnız KENDİ bağlantısının vadesi gelen işleri; çifte dağıtım yok), iş kapatma,
/// elle tetiğin ajana devri. Depo yazımı LocalDB'de ayrı sınıfta (<see cref="AjanDepoTests"/>).</summary>
public sealed class AjanTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>İmza yerine "K:{guid}" — kripto Api katmanının işi, burada yaşam döngüsü sınanır.</summary>
    private sealed class SahteKoruyucu : IAjanAnahtarKoruyucu
    {
        public string Uret(Guid ajanId) => $"K:{ajanId:N}";
        public Guid? Coz(string anahtar) =>
            anahtar.StartsWith("K:") && Guid.TryParse(anahtar[2..], out var id) ? id : null;
    }

    private sealed class SahteKapsam : IKiraciKapsami
    {
        private sealed class Bos : IDisposable { public void Dispose() { } }
        public IDisposable Baglan(Guid kiraciId) => new Bos();
    }

    private sealed class SahteParola : IPasswordProtector
    {
        public string Protect(string duzMetin) => duzMetin;
        public string Unprotect(string sifreli) => sifreli;
    }

    private sealed class SahteAyarlar : ISistemAyarlari
    {
        public string Metin(string anahtar) => "";
        public int Sayi(string anahtar) => 0;
        public bool EvetHayir(string anahtar) => false;
    }

    /// <summary>Depoya gerçekten yazmaz; hangi çağrının geldiğini kaydeder (LocalDB'siz sınama).</summary>
    private sealed class SahteDepoYazici : IAjanDepoYazici
    {
        public readonly List<string> Cagrilar = [];
        public string BaslaModu = "Artimli";
        public Task DepoyuHazirlaAsync(string sunucuDizesi, string dbAdi, CancellationToken ct)
        { Cagrilar.Add($"hazirla:{dbAdi}"); return Task.CompletedTask; }
        public Task<string> TabloBaslaAsync(string depoDizesi, string sema, string tablo,
            IReadOnlyList<DepoKolon> kolonlar, string mod, CancellationToken ct)
        { Cagrilar.Add($"basla:{sema}.{tablo}:{mod}"); return Task.FromResult(BaslaModu); }
        public Task SatirlarYazAsync(string depoDizesi, string sema, string hedefTablo,
            IReadOnlyList<DepoKolon> kolonlar, IReadOnlyList<object?[]> satirlar, CancellationToken ct)
        { Cagrilar.Add($"satir:{hedefTablo}:{satirlar.Count}"); return Task.CompletedTask; }
        public Task<long> TabloBitirAsync(string depoDizesi, string sema, string tablo, string mod,
            IReadOnlyList<DepoKolon> kolonlar, IReadOnlyList<string> pk, CancellationToken ct)
        { Cagrilar.Add($"bitir:{sema}.{tablo}:{mod}"); return Task.FromResult(42L); }
    }

    private sealed record SahteKb(Guid? KeycloakId, bool IsAdmin, bool CanDesign) : IUserContext;

    private sealed class Ortam : IAsyncDisposable
    {
        public required SqliteConnection Connection { get; init; }
        public required KdsDbContext Baglam { get; init; }
        public readonly SahteKoruyucu Koruyucu = new();
        public readonly SahteKapsam Kapsam = new();
        public readonly SahteDepoYazici Depo = new();
        public DepoCozucu DepoCozucu => new(
            new IceAlimAyarlari { DepoSunucuBaglantisi = "Server=(test);Trusted_Connection=True" },
            new SahteAyarlar());
        public ConnectionStringResolver DizeCozucu => new(Baglam, new SahteParola());
        public async ValueTask DisposeAsync() { await Baglam.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    private static async Task<Ortam> KurAsync()
    {
        var baglanti = new SqliteConnection("DataSource=:memory:");
        await baglanti.OpenAsync();
        var context = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(baglanti).Options,
            new FixedTenantContext(TenantA));
        await context.Database.EnsureCreatedAsync();
        return new Ortam { Connection = baglanti, Baglam = context };
    }

    private static async Task<(Agent Ajan, string Anahtar)> AjanEkleAsync(Ortam o, string ad = "Fabrika")
    {
        var sonuc = await new CreateAgentHandler(o.Baglam, o.Koruyucu, new SahteKb(null, true, true))
            .Handle(new CreateAgentRequest(ad), default);
        return (await o.Baglam.Ajanlar.FirstAsync(a => a.Id == sonuc.Id), sonuc.Anahtar);
    }

    private static async Task<Connection> AjanliBaglantiAsync(Ortam o, Guid ajanId)
    {
        var b = new Connection { Ad = "Kaynak", Sunucu = "s", Veritabani = "db", Mod = Contracts.Connections.DataMode.IceAlim, AjanId = ajanId };
        o.Baglam.Baglantilar.Add(b);
        await o.Baglam.SaveChangesAsync();
        return b;
    }

    private static async Task<IngestJob> IsEkleAsync(Ortam o, Guid baglantiId,
        DateTimeOffset? tetik = null, string? cron = null)
    {
        var j = new IngestJob
        {
            Ad = "İş", BaglantiId = baglantiId, Aktif = true, Zamanlama = cron,
            TablolarJson = """[{"Sema":"dbo","Tablo":"T","Kolonlar":null,"ArtimliKolon":"Id"}]""",
            ArtimliDurumJson = """{"dbo.T":"7"}""",
            AjanTetikTarihi = tetik,
        };
        o.Baglam.IceAlimIsleri.Add(j);
        await o.Baglam.SaveChangesAsync();
        return j;
    }

    [Fact]
    public async Task anahtar_yasam_dongusu_uret_dogrula_iptal_ret()
    {
        await using var o = await KurAsync();
        var (ajan, anahtar) = await AjanEkleAsync(o);

        var dogrulanan = await AjanKapisi.DogrulaAsync(o.Baglam, o.Koruyucu, anahtar, default);
        Assert.Equal(ajan.Id, dogrulanan.Id);

        // Bozuk anahtar ve iptal sonrası: ikisi de AYNI ayrımsız retle düşer.
        await Assert.ThrowsAsync<AjanReddi>(() => AjanKapisi.DogrulaAsync(o.Baglam, o.Koruyucu, "bozuk", default));
        Assert.True(await new RevokeAgentHandler(o.Baglam).Handle(new RevokeAgentRequest(ajan.Id), default));
        await Assert.ThrowsAsync<AjanReddi>(() => AjanKapisi.DogrulaAsync(o.Baglam, o.Koruyucu, anahtar, default));

        var liste = await new ListAgentsHandler(o.Baglam).Handle(new ListAgentsRequest(), default);
        Assert.Equal("iptal", Assert.Single(liste).Durum);
    }

    [Fact]
    public async Task isler_yalniz_kendi_baglantisinin_vadesi_gelenlerini_verir_ve_cifte_dagitmaz()
    {
        await using var o = await KurAsync();
        var (ajan, anahtar) = await AjanEkleAsync(o);
        var (digerAjan, _) = await AjanEkleAsync(o, "Diğer");

        var benimki = await AjanliBaglantiAsync(o, ajan.Id);
        var baskasininki = await AjanliBaglantiAsync(o, digerAjan.Id);

        var tetikli = await IsEkleAsync(o, benimki.Id, tetik: DateTimeOffset.UtcNow);
        await IsEkleAsync(o, benimki.Id);                                     // tetiksiz + cron'suz → verilmez
        await IsEkleAsync(o, baskasininki.Id, tetik: DateTimeOffset.UtcNow);  // başka ajanın işi → verilmez

        var handler = new AjanIsleriHandler(o.Baglam, o.Koruyucu, o.Kapsam);
        var sonuc = await handler.Handle(new AjanIsleriRequest(anahtar), default);

        var verilen = Assert.Single(sonuc.Isler);
        Assert.Equal(tetikli.Id, verilen.IsId);
        Assert.Equal("Id", verilen.Tablolar[0].ArtimliKolon);
        Assert.Equal("7", verilen.Tablolar[0].SuSeviyesi);   // S44 seviyesi ajana taşınır

        // Dağıtım işi "Kosuyor" işaretler, tetiği düşürür → İKİNCİ yoklama BOŞ (çifte koşum yok).
        Assert.Equal("Kosuyor", (await o.Baglam.IceAlimIsleri.FirstAsync(x => x.Id == tetikli.Id)).SonDurum);
        Assert.Empty((await handler.Handle(new AjanIsleriRequest(anahtar), default)).Isler);

        // Yoklama "son görülme"yi de günceller (ekran çevrimiçi çipi bundan).
        Assert.NotNull((await o.Baglam.Ajanlar.FirstAsync(a => a.Id == ajan.Id)).SonGorulme);
    }

    [Fact]
    public async Task is_bitir_durum_yazar_yabanci_ajanin_isine_yazamaz()
    {
        await using var o = await KurAsync();
        var (ajan, anahtar) = await AjanEkleAsync(o);
        var (_, yabanciAnahtar) = await AjanEkleAsync(o, "Yabancı");
        var b = await AjanliBaglantiAsync(o, ajan.Id);
        var job = await IsEkleAsync(o, b.Id, tetik: DateTimeOffset.UtcNow);

        // Aynı kiracının BAŞKA ajanı bile bu işe yazamaz (iş-ajan eşleşme kapısı).
        await Assert.ThrowsAsync<AjanReddi>(() => new AjanIsBitirHandler(o.Baglam, o.Koruyucu, o.Kapsam)
            .Handle(new AjanIsBitirRequest(yabanciAnahtar, job.Id, "Basarili", null, 5, 1, 1), default));

        Assert.True(await new AjanIsBitirHandler(o.Baglam, o.Koruyucu, o.Kapsam)
            .Handle(new AjanIsBitirRequest(anahtar, job.Id, "Basarili", null, 5, 1, 1), default));
        var guncel = await o.Baglam.IceAlimIsleri.FirstAsync(x => x.Id == job.Id);
        Assert.Equal("Basarili", guncel.SonDurum);
        Assert.Equal(5, guncel.SonSatirSayisi);
        Assert.Null(guncel.AjanTetikTarihi);

        // Hata bildirimi satır sayısını TAŞIMAZ (yarım koşumun sayısı yanıltıcı olurdu).
        await new AjanIsBitirHandler(o.Baglam, o.Koruyucu, o.Kapsam)
            .Handle(new AjanIsBitirRequest(anahtar, job.Id, "Hata", "kaynak kapalı", 3, 1, 0), default);
        guncel = await o.Baglam.IceAlimIsleri.FirstAsync(x => x.Id == job.Id);
        Assert.Equal("Hata", guncel.SonDurum);
        Assert.Equal("kaynak kapalı", guncel.SonHata);
        Assert.Null(guncel.SonSatirSayisi);
    }

    [Fact]
    public async Task tablo_bitir_su_seviyesini_tablo_basina_yazar()
    {
        await using var o = await KurAsync();
        var (ajan, anahtar) = await AjanEkleAsync(o);
        var b = await AjanliBaglantiAsync(o, ajan.Id);
        var job = await IsEkleAsync(o, b.Id);

        var sonuc = await new AjanTabloBitirHandler(o.Baglam, o.Koruyucu, o.Kapsam,
                o.DizeCozucu, o.DepoCozucu, o.Depo)
            .Handle(new AjanTabloBitirRequest(anahtar, job.Id, "dbo", "T", "Artimli",
                [new AjanKolonDto("Id", "int", null, null, null, false)], ["Id"], "99"), default);

        Assert.Equal(42, sonuc.SatirSayisi);
        Assert.Contains("bitir:dbo.T:Artimli", o.Depo.Cagrilar);
        Assert.Contains("\"dbo.T\":\"99\"",
            (await o.Baglam.IceAlimIsleri.FirstAsync(x => x.Id == job.Id)).ArtimliDurumJson);
    }

    [Fact]
    public async Task simdi_calistir_ajanli_kaynakta_kosmnaz_tetik_damgalar_ve_devreder()
    {
        await using var o = await KurAsync();
        var (ajan, _) = await AjanEkleAsync(o);
        var b = await AjanliBaglantiAsync(o, ajan.Id);
        var job = await IsEkleAsync(o, b.Id);

        // Koşucuya hiç inilmemeli — inseydi sunucu müşteri ağındaki kaynağa bağlanmayı denerdi.
        var kosucu = new PatlayanKosucu();
        var sonuc = await new RunIngestJobHandler(o.Baglam, o.DizeCozucu, kosucu,
                new FixedTenantContext(TenantA), o.DepoCozucu)
            .Handle(new RunIngestJobRequest(job.Id), default);

        Assert.True(sonuc.AjanaDevredildi);
        Assert.NotNull((await o.Baglam.IceAlimIsleri.FirstAsync(x => x.Id == job.Id)).AjanTetikTarihi);
    }

    private sealed class PatlayanKosucu : IIngestRunner
    {
        public Task<IngestOutcome> RunAsync(string k, string d, string db,
            IReadOnlyList<IngestTableSpec> t, IReadOnlyDictionary<string, string> s, CancellationToken ct)
            => throw new InvalidOperationException("Ajanlı işte sunucu koşucusu çağrılmamalı.");
        public Task<IngestOutcome> RunHedefeAsync(string k, string d,
            IReadOnlyList<IngestTableSpec> t, IReadOnlyDictionary<string, string> s, CancellationToken ct)
            => throw new InvalidOperationException("Ajanlı işte sunucu koşucusu çağrılmamalı.");
    }
}
