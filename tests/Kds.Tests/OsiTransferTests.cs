using Kds.Business.DataMaps;
using Kds.Business.Identity;
using Kds.Business.Metrics;
using Kds.Contracts.DataMaps;
using Kds.Contracts.Metrics;
using Kds.Contracts.QueryModel;
using Kds.Data;
using Kds.Data.Entities;
using Kds.Execution;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Kds.Tests;

/// <summary>S46 OSI (Apache Ossie) uyumu: dışa aktarım spec yapısını üretir (version +
/// semantic_model + datasets/relationships/metrics + LST_KDS uzantısı), KDS→KDS round-trip
/// KAYIPSIZDIR (Y1 uzantıdan aynen döner), uzantısız (başka üründen gelen) dosyada ANSI ifade
/// Y1'e çevrilir, harita/dosya hataları öğe öğe raporlanır.</summary>
public sealed class OsiTransferTests
{
    private static readonly Guid Kiraci = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid HaritaId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid TabloOdeme = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid TabloMusteri = Guid.Parse("33333333-0000-0000-0000-000000000002");

    private sealed record SahteKb(Guid? KeycloakId, bool IsAdmin, bool CanDesign) : IUserContext;

    /// <summary>Tablolu/ilişkili harita — dataset/relationship yazımı bununla doğrulanır.</summary>
    private sealed class SahteHaritalar : IDataMapReader
    {
        public Task<DataMapDetail?> GetDetailAsync(Guid haritaId, CancellationToken ct) =>
            Task.FromResult<DataMapDetail?>(haritaId == HaritaId
                ? new DataMapDetail(HaritaId, "Satış Haritası", Guid.NewGuid(), default,
                    [
                        new DataMapTableDto(TabloOdeme, "dbo", "ODM_MSTR", "Odemeler",
                        [
                            new DataMapColumnDto("ID", "Id", ColumnRole.Anahtar, "int"),
                            new DataMapColumnDto("TUTAR", "Tutar", ColumnRole.Olcu, "decimal"),
                            new DataMapColumnDto("GIZLI_KOD", null, ColumnRole.Gizli, "int"),
                        ]),
                        new DataMapTableDto(TabloMusteri, "dbo", "MST_MSTR", "Musteriler",
                        [
                            new DataMapColumnDto("ID", "Id", ColumnRole.Anahtar, "int"),
                        ]),
                    ],
                    [new DataMapRelationshipDto(TabloOdeme, "MUSTERI_ID", TabloMusteri, "ID", true)])
                : null);
    }

    private sealed class SahteUretici : IQueryGenerator
    {
        public GeneratedQuery Generate(QueryModel model, DataMapDetail dataMap,
            IReadOnlyDictionary<string, object?>? parametreDegerleri = null,
            IReadOnlyList<RowFilter>? satirKurallari = null) => new("SELECT 1", new Dictionary<string, object?>());
    }

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

    private static SaveMetricHandler Kayit(KdsDbContext ctx) =>
        new(ctx, new SahteKb(null, true, true), new SahteHaritalar(), new SahteUretici());

    private static ImportOsiMetricsHandler IceAl(KdsDbContext ctx) => new(ctx, new KayitYonlendirici(Kayit(ctx)));

    private static YamlMappingNode YamlKok(string yaml)
    {
        var akis = new YamlStream();
        akis.Load(new StringReader(yaml));
        return (YamlMappingNode)akis.Documents[0].RootNode;
    }

    private static YamlNode Al(YamlNode dugum, string yol)
    {
        foreach (var parca in yol.Split('/'))
        {
            dugum = int.TryParse(parca, out var i)
                ? ((YamlSequenceNode)dugum).Children[i]
                : ((YamlMappingNode)dugum).Children[new YamlScalarNode(parca)];
        }

        return dugum;
    }

    private static string Metin(YamlNode dugum, string yol) => ((YamlScalarNode)Al(dugum, yol)).Value!;

    [Fact]
    public async Task disa_aktarma_osi_yapisini_uretir_gizli_kolonu_yazmaz()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;
        await Kayit(ctx).Handle(new SaveMetricRequest(
            null, "Toplam Ciro", HaritaId, "SUM({Odemeler.Tutar})", MetrikBicimi.Para, "brüt ciro"), default);

        var yaml = await new ExportOsiMetricsHandler(ctx, new SahteHaritalar())
            .Handle(new ExportOsiMetricsRequest(), default);
        var kok = YamlKok(yaml);   // üretilen YAML gerçek bir ayrıştırıcıyla okunabiliyor

        Assert.Equal("0.1.0", Metin(kok, "version"));
        var model = Al(kok, "semantic_model/0");
        Assert.Equal("Satış Haritası", Metin(model, "name"));

        // datasets: dostça ad + kaynak; GİZLİ kolon dışarı YAZILMAZ (son kullanıcıya kapalı olan,
        // değişim dosyasına da girmez). primary_key de bilerek yok: haritadaki "Anahtar" rolü
        // PK+FK karışımıdır — yanlış PK beyanı alıcı üründe çıkarımı sessizce bozardı.
        Assert.Equal("Odemeler", Metin(model, "datasets/0/name"));
        Assert.Equal("dbo.ODM_MSTR", Metin(model, "datasets/0/source"));
        Assert.DoesNotContain("primary_key", yaml);
        Assert.DoesNotContain("GIZLI_KOD", yaml);

        // relationship: dostça adlarla from/to + kolon listeleri
        Assert.Equal("Odemeler", Metin(model, "relationships/0/from"));
        Assert.Equal("Musteriler", Metin(model, "relationships/0/to"));
        Assert.Equal("MUSTERI_ID", Metin(model, "relationships/0/from_columns/0"));

        // metric: ANSI lehçesi (yer tutucular açılmış) + LST_KDS uzantısında orijinal Y1
        Assert.Equal("Toplam Ciro", Metin(model, "metrics/0/name"));
        Assert.Equal("ANSI_SQL", Metin(model, "metrics/0/expression/dialects/0/dialect"));
        Assert.Equal("SUM(Odemeler.Tutar)", Metin(model, "metrics/0/expression/dialects/0/expression"));
        Assert.Equal("LST_KDS", Metin(model, "metrics/0/custom_extensions/0/vendor_name"));
        Assert.Contains("SUM({Odemeler.Tutar})", Metin(model, "metrics/0/custom_extensions/0/data"));
    }

    [Fact]
    public async Task kds_den_kds_ye_round_trip_kayipsiz()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;
        await Kayit(ctx).Handle(new SaveMetricRequest(
            null, "Ciro", HaritaId, "SUM({Odemeler.Tutar}) - SUM({Iadeler.Tutar})", MetrikBicimi.Para, "net"), default);

        var yaml = await new ExportOsiMetricsHandler(ctx, new SahteHaritalar())
            .Handle(new ExportOsiMetricsRequest(), default);
        var sonuclar = await IceAl(ctx).Handle(new ImportOsiMetricsRequest(yaml, CakismadaGuncelle: true), default);

        Assert.Equal(MetrikIceAlmaSonucu.Guncellendi, Assert.Single(sonuclar).Sonuc);
        var m = await ctx.Metrikler.SingleAsync();
        Assert.Equal("SUM({Odemeler.Tutar}) - SUM({Iadeler.Tutar})", m.Ifade);   // Y1 uzantıdan AYNEN
        Assert.Equal("Para", m.Bicim);                                          // biçim de uzantıdan
    }

    [Fact]
    public async Task uzantisiz_dosyada_ansi_ifade_y1e_cevrilir()
    {
        // Başka üründen gelen dosya: LST_KDS uzantısı yok, yalnız ANSI_SQL lehçesi var.
        var (ctx, c) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;
        const string yaml = """
            version: 0.1.0
            semantic_model:
              - name: "Satış Haritası"
                metrics:
                  - name: "Ortalama Sepet"
                    description: "sipariş başına"
                    expression:
                      dialects:
                        - dialect: ANSI_SQL
                          expression: "SUM(Odemeler.Tutar) / COUNT(Odemeler.Id)"
            """;

        var sonuclar = await IceAl(ctx).Handle(new ImportOsiMetricsRequest(yaml), default);

        Assert.Equal(MetrikIceAlmaSonucu.Eklendi, Assert.Single(sonuclar).Sonuc);
        var m = await ctx.Metrikler.SingleAsync();
        Assert.Equal("SUM({Odemeler.Tutar}) / COUNT({Odemeler.Id})", m.Ifade);
        Assert.Equal("sipariş başına", m.Aciklama);
    }

    [Fact]
    public async Task harita_yoksa_oge_hatasi_bozuk_dosyada_dosya_hatasi()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;
        var ice = IceAl(ctx);

        // Kurulumda olmayan harita → o modelin metrikleri tek öğe hatasıyla raporlanır.
        var haritaYok = await ice.Handle(new ImportOsiMetricsRequest("""
            version: 0.1.0
            semantic_model:
              - name: "Olmayan Harita"
                metrics:
                  - name: "X"
                    expression:
                      dialects:
                        - dialect: ANSI_SQL
                          expression: "COUNT(T.Id)"
            """), default);
        Assert.Contains("haritayı kurun", Assert.Single(haritaYok).Mesaj);

        // Bozuk YAML ve OSI olmayan YAML: dosya düzeyinde tek, açıklayıcı hata.
        Assert.Contains("YAML okunamadı",
            Assert.Single(await ice.Handle(new ImportOsiMetricsRequest("::: {bozuk"), default)).Mesaj);
        Assert.Contains("OSI semantik model",
            Assert.Single(await ice.Handle(new ImportOsiMetricsRequest("baska: dosya"), default)).Mesaj);
    }

    [Fact]
    public async Task ayni_ad_guncelleme_kapaliyken_atlanir()
    {
        var (ctx, c) = await BaglamAsync();
        await using var _1 = c; await using var _2 = ctx;
        await Kayit(ctx).Handle(new SaveMetricRequest(null, "Ciro", HaritaId, "SUM({O.T})", MetrikBicimi.Sayi, null), default);
        var yaml = await new ExportOsiMetricsHandler(ctx, new SahteHaritalar())
            .Handle(new ExportOsiMetricsRequest(), default);

        var sonuclar = await IceAl(ctx).Handle(new ImportOsiMetricsRequest(yaml), default);   // guncelle=false

        Assert.Equal(MetrikIceAlmaSonucu.Atlandi, Assert.Single(sonuclar).Sonuc);
    }

    [Fact]
    public void ifade_donusumu_literal_korur_ve_uc_parcali_adi_cevirmez()
    {
        // Y1 → ANSI: yer tutucu açılır, tek-tırnaklı literalin İÇİ veridir, dokunulmaz.
        Assert.Equal("SUM(Odemeler.Tutar) + 'sabit {X.Y} metin'",
            OsiIfade.Y1denAnsi("SUM({Odemeler.Tutar}) + 'sabit {X.Y} metin'"));

        // ANSI → Y1: Tablo.Kolon sarılır; literal içi ve fonksiyon çağrıları (a.b(...)) sarılmaz.
        Assert.Equal("SUM({Odemeler.Tutar}) / NULLIF(COUNT({Odemeler.Id}), 0) + 'a.b'",
            OsiIfade.AnsidenY1("SUM(Odemeler.Tutar) / NULLIF(COUNT(Odemeler.Id), 0) + 'a.b'"));

        // Üç parçalı ad (şema.tablo.kolon) Y1'de karşılıksız — DOKUNULMAZ; kayıt anında üretici
        // doğrulaması açık hatayla düşürür (sessizce yanlış sarım yerine).
        Assert.Equal("SUM(dbo.Odemeler.Tutar)", OsiIfade.AnsidenY1("SUM(dbo.Odemeler.Tutar)"));
    }
}
