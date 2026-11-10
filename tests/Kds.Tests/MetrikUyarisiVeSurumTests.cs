using Kds.Business.Alerts;
using Kds.Business.DataMaps;
using Kds.Business.Identity;
using Kds.Business.Metrics;
using Kds.Business.Subscriptions;
using Kds.Contracts.Alerts;
using Kds.Contracts.DataMaps;
using Kds.Contracts.Metrics;
using Kds.Contracts.Queries;
using Kds.Contracts.QueryModel;
using Kds.Data;
using Kds.Data.Entities;
using Kds.Execution;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S30: metriğe bağlı uyarı + metrik sürüm geçmişi + içe/dışa aktarma.
///
/// <para>Metrik uyarısının kritik sözleşmesi: kaynak TAM BİRİ (rapor XOR metrik) ve koşum
/// metriği <c>MeasureMetric</c> ile aynı kırılımsız modelle ölçer — kolon/özet kavramı yoktur.
/// Sürümlemenin kritiği: satır = ESKİ hâl; tanım her değişimde yazılır, durum (Aktif) yazılmaz.</para></summary>
public sealed class MetrikUyarisiVeSurumTests
{
    private static readonly Guid Kiraci = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid HaritaId = Guid.Parse("22222222-0000-0000-0000-000000000001");

    private sealed record SahteKb(Guid? KeycloakId, bool IsAdmin, bool CanDesign) : IUserContext;

    private static async Task<(KdsDbContext Ctx, SqliteConnection C)> BaglamAsync()
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(Kiraci));
        await ctx.Database.EnsureCreatedAsync();
        ctx.Haritalar.Add(new DataMap { Id = HaritaId, TenantId = Kiraci, Ad = "Satış Haritası", BaglantiId = Guid.NewGuid() });
        await ctx.SaveChangesAsync();
        return (ctx, c);
    }

    private static Metric Metrik(string ad = "Toplam Ciro") => new()
    {
        Id = Guid.NewGuid(), TenantId = Kiraci, Ad = ad, Ifade = "SUM({Odemeler.Tutar})",
        VeriHaritasiId = HaritaId, Bicim = "Para", Aktif = true,
    };

    /// <summary>Metrik ölçümünü koşmuş gibi yapar: kolon adı = metrik adı, tek satır 42.</summary>
    private sealed class MetrikSonucGonderici(string kolonAd) : ISender
    {
        public QueryModel? Kosulan;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is RunQueryModelRequest k)
            {
                Kosulan = k.Model;
                return Task.FromResult((TResponse)(object)new QueryRunResult(
                    [new QueryColumnDto(kolonAd, "decimal")], [new object?[] { 42m }],
                    SatirSiniriUygulandi: false, SureMs: 1));
            }

            throw new NotSupportedException(request.GetType().Name);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class SahteEposta : IEpostaGonderici
    {
        public Task<bool> GonderAsync(EpostaMesaji mesaj, CancellationToken ct) => Task.FromResult(true);
    }

    // ---------- kaynak seçimi (XOR) ----------

    [Fact]
    public void uyari_kaynagi_TAM_BIRI_rapor_veya_metrik()
    {
        var v = new SaveAlertValidation();
        SaveAlertRequest Istek(Guid? rapor, Guid? metrik) => new(null, "U", rapor, null,
            OzetTuru.SatirSayisi, UyariOp.Buyuk, 0, ["a@x.com"], null, true, false, MetrikId: metrik);

        Assert.False(v.Validate(Istek(null, null)).IsValid);                    // kaynaksız
        Assert.False(v.Validate(Istek(Guid.NewGuid(), Guid.NewGuid())).IsValid); // ikisi birden
        Assert.True(v.Validate(Istek(Guid.NewGuid(), null)).IsValid);
        Assert.True(v.Validate(Istek(null, Guid.NewGuid())).IsValid);
    }

    [Fact]
    public void metrik_uyarisinda_kolon_kurali_ISLEMEZ()
    {
        // Rapor yolunda 'Toplam' özeti kolon ister; metrik yolunda kolon kavramı yoktur —
        // aynı istek metrikle geçerli olmalı.
        var v = new SaveAlertValidation();
        var istek = new SaveAlertRequest(null, "U", null, null,
            OzetTuru.Toplam, UyariOp.Buyuk, 0, ["a@x.com"], null, true, false, MetrikId: Guid.NewGuid());
        Assert.True(v.Validate(istek).IsValid);
    }

    // ---------- metrik uyarısının koşumu ----------

    [Fact]
    public async Task metrik_uyarisi_kaydedilir_ve_metrigin_degeriyle_degerlendirilir()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;
        var m = Metrik();
        ctx.Metrikler.Add(m);
        await ctx.SaveChangesAsync();

        var kb = new SahteKb(null, true, true);
        var ozet = await new SaveAlertHandler(ctx, kb, new KuralsizCozucu()).Handle(
            new SaveAlertRequest(null, "Ciro düştü mü", null, null, OzetTuru.SatirSayisi,
                UyariOp.Buyuk, 10, ["a@x.com"], null, true, false, MetrikId: m.Id), default);

        Assert.Equal(m.Id, ozet.MetrikId);
        Assert.Equal("Toplam Ciro", ozet.MetrikAd);
        Assert.Null(ozet.RaporAd);

        var gonderici = new MetrikSonucGonderici(m.Ad);
        var sonuc = await new RunAlertHandler(ctx, gonderici, new SahteEposta(), new KuralsizCozucu())
            .Handle(new RunAlertRequest(ozet.Id, BildirimGonder: false), default);

        Assert.Equal(42m, sonuc.Deger);
        Assert.True(sonuc.Tetiklendi);   // 42 > 10
        // Koşulan model MeasureMetric ile aynı biçimde olmalı: kırılımsız, tek metrik referansı.
        Assert.NotNull(gonderici.Kosulan);
        Assert.Empty(gonderici.Kosulan!.Alanlar);
        Assert.Equal(m.Id, Assert.Single(gonderici.Kosulan.MetrikAlanlar!).MetrikId);
    }

    // ---------- sürüm geçmişi ----------

    private sealed class SahteHaritalar : IDataMapReader
    {
        public Task<DataMapDetail?> GetDetailAsync(Guid haritaId, CancellationToken ct) =>
            Task.FromResult<DataMapDetail?>(haritaId == HaritaId
                ? new DataMapDetail(HaritaId, "Satış Haritası", Guid.NewGuid(), default, [], [])
                : null);
    }

    private sealed class SahteUretici : IQueryGenerator
    {
        public GeneratedQuery Generate(QueryModel model, DataMapDetail dataMap,
            IReadOnlyDictionary<string, object?>? parametreDegerleri = null,
            IReadOnlyList<RowFilter>? satirKurallari = null) => new("SELECT 1", new Dictionary<string, object?>());
    }

    /// <summary>SaveMetricRequest'i gerçek handler'a yönlendirir (Restore/Import bu yoldan kaydeder).</summary>
    private sealed class KayitYonlendirici(SaveMetricHandler kayit) : ISender
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default) =>
            request is SaveMetricRequest k
                ? (TResponse)(object)await kayit.Handle(k, ct)
                : throw new NotSupportedException(request.GetType().Name);

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private static SaveMetricHandler KayitHandler(KdsDbContext ctx) =>
        new(ctx, new SahteKb(null, true, true), new SahteHaritalar(), new SahteUretici());

    [Fact]
    public async Task guncelleme_ONCEKI_hali_surum_gecmisine_yazar_durum_degisikligi_yazmaz()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;
        var kayit = KayitHandler(ctx);

        var ilk = await kayit.Handle(new SaveMetricRequest(null, "Ciro", HaritaId, "SUM({O.T})", MetrikBicimi.Para, null), default);
        Assert.Empty(await ctx.MetrikSurumleri.ToListAsync());   // yeni kayıt sürüm üretmez

        await kayit.Handle(new SaveMetricRequest(ilk.Id, "Ciro", HaritaId, "SUM({O.T}) - SUM({I.T})", MetrikBicimi.Para, null), default);
        var surum = Assert.Single(await ctx.MetrikSurumleri.ToListAsync());
        Assert.Equal(1, surum.SurumNo);
        Assert.Equal("SUM({O.T})", surum.Ifade);                 // satır = ESKİ hâl

        // Yalnız Aktif değişimi tanım değişikliği DEĞİLDİR — geçmişi şişirmemeli.
        await kayit.Handle(new SaveMetricRequest(ilk.Id, "Ciro", HaritaId, "SUM({O.T}) - SUM({I.T})", MetrikBicimi.Para, null, Aktif: false), default);
        Assert.Equal(1, await ctx.MetrikSurumleri.CountAsync());
    }

    [Fact]
    public async Task surume_donus_tanimi_geri_yukler_ve_KENDISI_de_surum_uretir()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;
        var kayit = KayitHandler(ctx);

        var m = await kayit.Handle(new SaveMetricRequest(null, "Ciro", HaritaId, "ESKI", MetrikBicimi.Para, "ilk tanım"), default);
        await kayit.Handle(new SaveMetricRequest(m.Id, "Ciro", HaritaId, "YENI", MetrikBicimi.Para, "revize"), default);

        var geri = await new RestoreMetricVersionHandler(ctx, new KayitYonlendirici(kayit))
            .Handle(new RestoreMetricVersionRequest(m.Id, 1), default);

        Assert.Equal("ESKI", geri.Ifade);
        Assert.Equal("ilk tanım", geri.Aciklama);
        // Dönüş de sürüm üretti (YENI hâli tarihe gömüldü) → geçmiş hiç kaybolmaz.
        Assert.Equal(2, await ctx.MetrikSurumleri.CountAsync());
        var son = await ctx.MetrikSurumleri.OrderByDescending(v => v.SurumNo).FirstAsync();
        Assert.Equal("YENI", son.Ifade);
    }

    // ---------- içe / dışa aktarma ----------

    [Fact]
    public async Task disa_aktarma_harita_ADIYLA_yazar_ice_alim_adla_cozer_ve_strateji_uygular()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;
        var kayit = KayitHandler(ctx);
        await kayit.Handle(new SaveMetricRequest(null, "Ciro", HaritaId, "SUM({O.T})", MetrikBicimi.Para, null), default);

        var disa = await new ExportMetricsHandler(ctx).Handle(new ExportMetricsRequest(), default);
        var oge = Assert.Single(disa);
        Assert.Equal("Satış Haritası", oge.HaritaAd);   // GUID değil AD — dosya ortamlar arası taşınır

        var ice = new ImportMetricsHandler(ctx, new KayitYonlendirici(kayit));

        // Çakışma + güncelleme kapalı → atlanır; bilinmeyen harita → öğe hatası (koşum sürer).
        var sonuc1 = await ice.Handle(new ImportMetricsRequest([
            oge,
            new MetricExportItem("Olmayan Harita", "Yeni Metrik", "1", MetrikBicimi.Sayi, null),
        ]), default);
        Assert.Equal(MetrikIceAlmaSonucu.Atlandi, sonuc1[0].Sonuc);
        Assert.Equal(MetrikIceAlmaSonucu.Hata, sonuc1[1].Sonuc);
        Assert.Contains("Olmayan Harita", sonuc1[1].Mesaj);

        // Güncelleme açık → mevcut tanım güncellenir ve sürüm geçmişine yazılır.
        var sonuc2 = await ice.Handle(new ImportMetricsRequest(
            [oge with { Ifade = "SUM({O.T}) * 2" }], CakismadaGuncelle: true), default);
        Assert.Equal(MetrikIceAlmaSonucu.Guncellendi, Assert.Single(sonuc2).Sonuc);
        Assert.Equal("SUM({O.T}) * 2", (await ctx.Metrikler.SingleAsync()).Ifade);
        Assert.Equal(1, await ctx.MetrikSurumleri.CountAsync());
    }
}
