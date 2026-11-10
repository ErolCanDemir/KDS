using Kds.Business.Alerts;
using Kds.Business.Identity;
using Kds.Business.Security;
using Kds.Business.Subscriptions;
using Kds.Contracts.Alerts;
using Kds.Contracts.Identity;
using Kds.Contracts.Queries;
using Kds.Contracts.QueryModel;
using Kds.Contracts.Reports;
using Kds.Contracts.Subscriptions;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>Zamanlanmış işlerin (abonelik · uyarı) satır-düzeyi güvenliği.
///
/// <para><b>Sorun:</b> bu işler arka planda, HTTP isteği olmadan koşar. RLS kimliğe dayanır; boş
/// kimlikle rol-kapsamlı kural (<c>Roller='musteri'</c>) eşleşmez ve <b>sessizce düşer</b> —
/// çözücünün fail-closed koruması bile devreye girmez. Sonuç: kısıtlı satırlar e-postayla çıkardı.</para>
///
/// <para><b>Çözüm (20 Ağu 2026):</b> iş kurulurken sahibin rol+iddia <b>anlık görüntüsü</b> işle
/// saklanır; koşumda geri yüklenip <c>RunQueryModelRequest.KosanKimlik</c> ile taşınır ve kurallar
/// O kimliğe uygulanır. Böylece zamanlanmış gönderim, rapor sahibinin yetkisiyle koşar — piyasadaki
/// karşılığı da budur. Önceki tümden yasak (kurallı veriye hiç dokunma) kaldırıldı; yerine iki dar
/// ret kaldı: <b>kimlik yoksa</b> ve <b>ham SQL + sahibi kurala tabiyse</b>.</para></summary>
public sealed class ZamanlanmisIsRlsKapisiTests
{
    private static readonly Guid Kiraci = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Harita = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid Tablo = Guid.Parse("33333333-0000-0000-0000-000000000001");

    /// <summary>Gerçek kimlik taşıyan bağlam — anlık görüntü ancak böyle üretilir.</summary>
    private sealed record SahteKb(
        Guid? KeycloakId, bool IsAdmin, bool CanDesign,
        IReadOnlyCollection<string> Roller, string? KullaniciAdi, string? EPosta,
        IReadOnlyDictionary<string, string> Iddialar) : IUserContext;

    private static readonly Guid BayiKc = Guid.Parse("44444444-0000-0000-0000-000000000001");

    /// <summary>Kimliği SABİT bayi — aynı kullanıcının işini yeniden kaydedebilmesi için
    /// (sahiplik kapısı KeycloakId üzerinden çözülür).</summary>
    private static IUserContext Musteri(string servisKodu) => new SahteKb(
        BayiKc, false, false, [Roles.Musteri], "bayi", "bayi@x.com",
        new Dictionary<string, string> { ["servis_kodu"] = servisKodu });

    private static IUserContext Yonetici() => new SahteKb(
        Guid.NewGuid(), true, true, [Roles.Admin], "admin", "admin@x.com", new Dictionary<string, string>());

    /// <summary>Üretilen kuralları YAKALAYAN sahte: RLS'in gerçekten uygulanıp uygulanmadığını
    /// görmek için (sonuç kümesi taklit; önemli olan kuralların geçmesi).</summary>
    private sealed class KuralYakalayanGonderici : ISender
    {
        public RunQueryModelRequest? SonModelIstegi;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is RunQueryModelRequest m) SonModelIstegi = m;
            return Task.FromResult((TResponse)(object)new QueryRunResult(
                [new QueryColumnDto("Deger", "int")], [new object?[] { 1 }], false, 1));
        }

        public Task Send<TRequest>(TRequest r, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public Task<object?> Send(object r, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> r, CancellationToken ct = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object r, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class SessizEposta : IEpostaGonderici
    {
        public Task<bool> GonderAsync(EpostaMesaji mesaj, CancellationToken ct) => Task.FromResult(true);
    }

    private static async Task<(KdsDbContext Ctx, SqliteConnection C, Guid ModelRapor, Guid HamRapor)> KurAsync()
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(Kiraci));
        await ctx.Database.EnsureCreatedAsync();

        ctx.Kullanicilar.Add(new User { Id = Guid.NewGuid(), TenantId = Kiraci, KeycloakId = BayiKc, KullaniciAdi = "bayi" });
        ctx.Haritalar.Add(new DataMap
        {
            Id = Harita, TenantId = Kiraci, Ad = "Harita", BaglantiId = Guid.NewGuid(),
            Tablolar = [new DataMapTable { Id = Tablo, VeriHaritasiId = Harita, Sema = "dbo", HamAd = "T", DostcaAd = "T" }],
        });

        var model = new Report
        {
            Id = Guid.NewGuid(), TenantId = Kiraci, Ad = "Model Rapor", Paylasim = ReportSharing.Ekip,
            Gorsellestirme = VisualizationType.Tablo,
            ModelJson = $$"""{"haritaId":"{{Harita}}","alanlar":[]}""",
        };
        var ham = new Report
        {
            Id = Guid.NewGuid(), TenantId = Kiraci, Ad = "Ham SQL Rapor", Paylasim = ReportSharing.Ekip,
            Gorsellestirme = VisualizationType.Tablo, HamSql = "SELECT 1", BaglantiId = Guid.NewGuid(),
        };
        ctx.Raporlar.AddRange(model, ham);
        await ctx.SaveChangesAsync();
        return (ctx, c, model.Id, ham.Id);
    }

    private static async Task KuralEkleAsync(KdsDbContext ctx)
    {
        ctx.SatirKurallari.Add(new RowRule
        {
            Id = Guid.NewGuid(), TenantId = Kiraci, VeriHaritasiId = Harita, HaritaTabloId = Tablo,
            Ad = "Bayi kapsamı", Kolon = "ServisId", Op = KarsilastirmaOp.Esit,
            DegerKaynagi = KuralDegerKaynagi.TokenIddiasi, Deger = "servis_kodu",
            Roller = Roles.Musteri, Aktif = true,
        });
        await ctx.SaveChangesAsync();
    }

    private static SaveAlertRequest UyariIstegi(Guid raporId) =>
        new(null, "Eşik", raporId, null, OzetTuru.SatirSayisi, UyariOp.Buyuk, 0, ["a@x.com"], "0 * * * *", true, false);

    [Fact]
    public async Task zamanlanmis_kosum_SAHIBIN_kimligiyle_kosar_kural_uygulanir()
    {
        var (ctx, c, modelRapor, _) = await KurAsync();
        await using var _1 = c; await using var _2 = ctx;
        var cozucu = new SatirKuraliCozucu(ctx);
        await KuralEkleAsync(ctx);

        // Kurala TABİ bir müşteri uyarı kurar — kimliği (rol + servis_kodu iddiası) işle saklanır.
        var sahip = Musteri("1632");
        var ozet = await new SaveAlertHandler(ctx, sahip, cozucu).Handle(UyariIstegi(modelRapor), default);

        // Arka plan koşumu: istek bağlamı YOK. Kimlik anlık görüntüsünden geri yüklenmeli.
        var gonderici = new KuralYakalayanGonderici();
        await new RunAlertHandler(ctx, gonderici, new SessizEposta(), cozucu)
            .Handle(new RunAlertRequest(ozet.Id), default);

        // KANIT: model koşumuna sahibin kimliği taşındı ve kural O kimliğe çözüldü.
        Assert.NotNull(gonderici.SonModelIstegi);
        var kimlik = gonderici.SonModelIstegi!.KosanKimlik;
        Assert.NotNull(kimlik);
        Assert.Contains(Roles.Musteri, kimlik!.Roller);
        Assert.Equal("1632", kimlik.Iddialar["servis_kodu"]);

        // Ve çözücü bu kimlikle gerçekten filtre üretiyor (boş kimlikle üretmezdi).
        var kurallar = await cozucu.CozAsync(Harita, kimlik, default);
        var kural = Assert.Single(kurallar);
        Assert.Equal("ServisId", kural.Kolon);
        Assert.Equal(["1632"], kural.Degerler);
    }

    [Fact]
    public async Task kimligi_OLMAYAN_eski_kayit_kurall_veride_KOSMAZ()
    {
        var (ctx, c, modelRapor, _) = await KurAsync();
        await using var _1 = c; await using var _2 = ctx;
        var cozucu = new SatirKuraliCozucu(ctx);

        var ozet = await new SaveAlertHandler(ctx, Yonetici(), cozucu).Handle(UyariIstegi(modelRapor), default);
        // Kimlik saklanmadan önce kurulmuş bir kaydı taklit et.
        var satir = await ctx.Uyarilar.FirstAsync(u => u.Id == ozet.Id);
        satir.SahipKimlikJson = null;
        await ctx.SaveChangesAsync();
        await KuralEkleAsync(ctx);

        // Kimlik yoksa kimin adına koşulacağı bilinemez → fail-closed. Mesaj çözümü söyler.
        var hata = await Assert.ThrowsAsync<SatirKuraliHatasi>(() =>
            new RunAlertHandler(ctx, new KuralYakalayanGonderici(), new SessizEposta(), cozucu)
                .Handle(new RunAlertRequest(ozet.Id), default));
        Assert.Contains("bir kez kaydedin", hata.Message);
    }

    [Fact]
    public async Task ham_sql_sahibi_kurala_tabiyse_KOSMAZ()
    {
        var (ctx, c, _, hamRapor) = await KurAsync();
        await using var _1 = c; await using var _2 = ctx;
        var cozucu = new SatirKuraliCozucu(ctx);

        var abonelik = await new SaveSubscriptionHandler(ctx, Musteri("1632"), cozucu).Handle(
            new SaveSubscriptionRequest(null, "Ham", hamRapor, "Csv", ["a@x.com"], "0 8 * * 1", true), default);
        await KuralEkleAsync(ctx);

        // Ham SQL modelden geçmez → kural ENJEKTE EDİLEMEZ; kimlik bilinse bile uygulanacak yer yok.
        // (Ekran yolundaki RunReportHandler de aynı ayrımı yapıyor.)
        var hata = await Assert.ThrowsAsync<SatirKuraliHatasi>(() =>
            new RunSubscriptionHandler(ctx, new KuralYakalayanGonderici(), new Kds.Export.ResultExporter(),
                new SessizEposta(), cozucu).Handle(new RunSubscriptionRequest(abonelik.Id), default));
        Assert.Contains("elle yazılmış SQL", hata.Message);
    }

    [Fact]
    public async Task ham_sql_sahibi_kurala_tabi_DEGILSE_kosar()
    {
        var (ctx, c, _, hamRapor) = await KurAsync();
        await using var _1 = c; await using var _2 = ctx;
        var cozucu = new SatirKuraliCozucu(ctx);

        // Admin kurala tabi değildir (muaf) → ham SQL koşumu onun için sorun değil; kapı fazla
        // geniş olsaydı burada patlar ve özellik gereksiz yere kilitlenirdi.
        var abonelik = await new SaveSubscriptionHandler(ctx, Yonetici(), cozucu).Handle(
            new SaveSubscriptionRequest(null, "Ham", hamRapor, "Csv", ["a@x.com"], "0 8 * * 1", true), default);
        await KuralEkleAsync(ctx);

        var sonuc = await new RunSubscriptionHandler(ctx, new KuralYakalayanGonderici(),
            new Kds.Export.ResultExporter(), new SessizEposta(), cozucu)
            .Handle(new RunSubscriptionRequest(abonelik.Id), default);
        Assert.Equal(1, sonuc.AliciSayisi);
    }

    [Fact]
    public async Task kuralsiz_haritadaki_rapor_ETKILENMEZ()
    {
        var (ctx, c, modelRapor, _) = await KurAsync();
        await using var _1 = c; await using var _2 = ctx;
        var cozucu = new SatirKuraliCozucu(ctx);

        // Kural BAŞKA haritada → bu işin verisi kurallı değil; hiçbir kapı devreye girmemeli.
        var baskaHarita = Guid.NewGuid();
        var baskaTablo = Guid.NewGuid();
        ctx.Haritalar.Add(new DataMap
        {
            Id = baskaHarita, TenantId = Kiraci, Ad = "Başka", BaglantiId = Guid.NewGuid(),
            Tablolar = [new DataMapTable { Id = baskaTablo, VeriHaritasiId = baskaHarita, Sema = "dbo", HamAd = "X", DostcaAd = "X" }],
        });
        ctx.SatirKurallari.Add(new RowRule
        {
            Id = Guid.NewGuid(), TenantId = Kiraci, VeriHaritasiId = baskaHarita, HaritaTabloId = baskaTablo,
            Ad = "İlgisiz", Kolon = "X", Op = KarsilastirmaOp.Esit,
            DegerKaynagi = KuralDegerKaynagi.SabitDeger, Deger = "1", Aktif = true,
        });
        await ctx.SaveChangesAsync();

        var ozet = await new SaveAlertHandler(ctx, Musteri("1632"), cozucu).Handle(UyariIstegi(modelRapor), default);
        var sonuc = await new RunAlertHandler(ctx, new KuralYakalayanGonderici(), new SessizEposta(), cozucu)
            .Handle(new RunAlertRequest(ozet.Id), default);

        Assert.True(sonuc.Tetiklendi);
    }

    [Fact]
    public async Task kimlik_goruntusu_HER_kayitta_tazelenir()
    {
        var (ctx, c, modelRapor, _) = await KurAsync();
        await using var _1 = c; await using var _2 = ctx;
        var cozucu = new SatirKuraliCozucu(ctx);

        var ozet = await new SaveAlertHandler(ctx, Musteri("1632"), cozucu).Handle(UyariIstegi(modelRapor), default);
        var ilk = KimlikAnlikGoruntusu.Oku((await ctx.Uyarilar.AsNoTracking().FirstAsync(u => u.Id == ozet.Id)).SahipKimlikJson);
        Assert.Equal("1632", ilk!.Iddialar["servis_kodu"]);

        // Kullanıcının kapsamı değişti ve işi yeniden kaydetti → görüntü tazelenmeli, yoksa iş
        // sonsuza dek eski yetkiyle koşardı.
        await new SaveAlertHandler(ctx, Musteri("9999"), cozucu).Handle(
            new SaveAlertRequest(ozet.Id, "Eşik", modelRapor, null, OzetTuru.SatirSayisi, UyariOp.Buyuk,
                0, ["a@x.com"], "0 * * * *", true, false), default);

        var yeni = KimlikAnlikGoruntusu.Oku((await ctx.Uyarilar.AsNoTracking().FirstAsync(u => u.Id == ozet.Id)).SahipKimlikJson);
        Assert.Equal("9999", yeni!.Iddialar["servis_kodu"]);
    }
}
