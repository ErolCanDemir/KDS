using Kds.Business.Identity;
using Kds.Business.Reports;
using Kds.Contracts.Connections;
using Kds.Contracts.DataMaps;
using Kds.Contracts.Reports;
using Kds.Contracts.QueryModel;
using Kds.Contracts.Queries;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S10 Raporlar: kaydet/listele/getir round-trip (model JSON'u dahil), Özel/Ekip
/// görünürlüğü (sahip · başkası · admin), favori toggle ve klasör silme davranışı.</summary>
public sealed class ReportTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BenKc = Guid.Parse("aaaaaaaa-1111-0000-0000-000000000001");
    private static readonly Guid BaskaKc = Guid.Parse("aaaaaaaa-1111-0000-0000-000000000002");

    private sealed record SahteKb(Guid? KeycloakId, bool IsAdmin, bool CanDesign) : IUserContext;

    /// <summary>SQL raporu kayıtta da SELECT-only bekçisinden geçer — gerçek bekçiyle test edilir.</summary>
    private static readonly Kds.Execution.ScriptDomQueryGuard Bekci = new();

    private static QueryModel OrnekModel(Guid haritaId, Guid tabloId) => new(haritaId,
        [new SelectedField(tabloId, "Sehir"), new SelectedField(tabloId, "Tutar", ToplamaTuru.Topla, "Toplam")],
        new FilterGroup(MantikBaglaci.Ve, [new FilterCondition(tabloId, "Sehir", KarsilastirmaOp.Esit, ["Ankara"])]),
        [new SortItem(tabloId, "Tutar", ToplamaTuru.Topla, Azalan: true)],
        500);

    private sealed class Ortam : IAsyncDisposable
    {
        public required SqliteConnection Connection { get; init; }
        public required KdsDbContext Baglam { get; init; }
        public Guid BenId; public Guid BaskaId; public Guid HaritaId; public Guid TabloId;

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

        var ben = new User { KeycloakId = BenKc, KullaniciAdi = "ben" };
        var baska = new User { KeycloakId = BaskaKc, KullaniciAdi = "baska" };
        context.Kullanicilar.AddRange(ben, baska);
        var tabloId = Guid.NewGuid();
        var dataMap = new DataMap
        {
            Ad = "H", BaglantiId = Guid.NewGuid(), Mod = DataMode.Canli,
            Tablolar = [new DataMapTable { Id = tabloId, Sema = "dbo", HamAd = "T", DostcaAd = "T" }],
        };
        context.Haritalar.Add(dataMap);
        await context.SaveChangesAsync();

        return new Ortam { Connection = baglanti, Baglam = context, BenId = ben.Id, BaskaId = baska.Id, HaritaId = dataMap.Id, TabloId = tabloId };
    }

    private static SahteKb Ben(bool admin = false) => new(BenKc, admin, true);
    private static SahteKb Baska(bool admin = false) => new(BaskaKc, admin, true);

    [Fact]
    public async Task kaydet_listele_getir_roundtrip_model_dahil()
    {
        await using var o = await KurAsync();
        var kb = Ben();

        var ozet = await new SaveReportHandler(o.Baglam, kb, Bekci).Handle(
            new SaveReportRequest(null, "Satış Raporu", null, OrnekModel(o.HaritaId, o.TabloId),
                VisualizationType.Cubuk, ReportSharing.Ekip), default);
        Assert.NotEqual(Guid.Empty, ozet.Id);
        Assert.True(ozet.BenimMi);

        var liste = await new ListReportsHandler(o.Baglam, kb).Handle(new ListReportsRequest(), default);
        var satir = Assert.Single(liste);
        Assert.Equal("Satış Raporu", satir.Ad);
        Assert.Equal(VisualizationType.Cubuk, satir.Gorsellestirme);

        var detay = await new GetReportHandler(o.Baglam, kb).Handle(new GetReportRequest(ozet.Id), default);
        Assert.NotNull(detay);
        var model = detay!.Model!; // model raporu → model dolu
        Assert.Equal(o.HaritaId, model.HaritaId);
        Assert.Equal(2, model.Alanlar.Count);
        var grup = Assert.IsType<FilterGroup>(model.Filtre);                 // polimorfik JSON round-trip
        var kosul = Assert.IsType<FilterCondition>(grup.Cocuklar[0]);
        Assert.Equal(KarsilastirmaOp.Esit, kosul.Op);
        Assert.Equal(500, model.SatirLimiti);
        Assert.Equal(ToplamaTuru.Topla, model.Siralama[0].Toplama);
    }

    [Fact]
    public async Task ozel_rapor_sadece_sahibi_ve_admin_gorur()
    {
        await using var o = await KurAsync();
        var ozet = await new SaveReportHandler(o.Baglam, Ben(), Bekci).Handle(
            new SaveReportRequest(null, "Gizli", null, OrnekModel(o.HaritaId, o.TabloId),
                VisualizationType.Tablo, ReportSharing.Ozel), default);

        Assert.Single(await new ListReportsHandler(o.Baglam, Ben()).Handle(new ListReportsRequest(), default));
        Assert.Empty(await new ListReportsHandler(o.Baglam, Baska()).Handle(new ListReportsRequest(), default));
        Assert.Null(await new GetReportHandler(o.Baglam, Baska()).Handle(new GetReportRequest(ozet.Id), default));
        Assert.Single(await new ListReportsHandler(o.Baglam, Baska(admin: true)).Handle(new ListReportsRequest(), default));
        Assert.NotNull(await new GetReportHandler(o.Baglam, Baska(admin: true)).Handle(new GetReportRequest(ozet.Id), default));

        // Başkası (admin değil) silemez; sahibi siler.
        Assert.False(await new DeleteReportHandler(o.Baglam, Baska()).Handle(new DeleteReportRequest(ozet.Id), default));
        Assert.True(await new DeleteReportHandler(o.Baglam, Ben()).Handle(new DeleteReportRequest(ozet.Id), default));
    }

    [Fact]
    public async Task favori_kullanici_bazlidir_ve_toggle_calisir()
    {
        await using var o = await KurAsync();
        var ozet = await new SaveReportHandler(o.Baglam, Ben(), Bekci).Handle(
            new SaveReportRequest(null, "R", null, OrnekModel(o.HaritaId, o.TabloId),
                VisualizationType.Cizgi, ReportSharing.Ekip), default);

        Assert.True(await new SetReportFavoriteHandler(o.Baglam, Ben()).Handle(new SetReportFavoriteRequest(ozet.Id, true), default));
        Assert.True((await new ListReportsHandler(o.Baglam, Ben()).Handle(new ListReportsRequest(), default))[0].FavoriMi);
        Assert.False((await new ListReportsHandler(o.Baglam, Baska()).Handle(new ListReportsRequest(), default))[0].FavoriMi);

        Assert.True(await new SetReportFavoriteHandler(o.Baglam, Ben()).Handle(new SetReportFavoriteRequest(ozet.Id, false), default));
        Assert.False((await new ListReportsHandler(o.Baglam, Ben()).Handle(new ListReportsRequest(), default))[0].FavoriMi);
    }

    [Fact]
    public async Task klasor_silinince_raporlar_klasorsuze_duser()
    {
        await using var o = await KurAsync();
        var klasor = await new AddFolderHandler(o.Baglam).Handle(new AddFolderRequest("Satış"), default);
        var ozet = await new SaveReportHandler(o.Baglam, Ben(), Bekci).Handle(
            new SaveReportRequest(null, "R", klasor.Id, OrnekModel(o.HaritaId, o.TabloId),
                VisualizationType.Tablo, ReportSharing.Ekip), default);
        Assert.Equal("Satış", ozet.KlasorAd);

        var klasorler = await new ListFoldersHandler(o.Baglam, Ben()).Handle(new ListFoldersRequest(), default);
        Assert.Equal(1, Assert.Single(klasorler).RaporSayisi);

        Assert.True(await new DeleteFolderHandler(o.Baglam).Handle(new DeleteFolderRequest(klasor.Id), default));
        var detay = await new GetReportHandler(o.Baglam, Ben()).Handle(new GetReportRequest(ozet.Id), default);
        Assert.NotNull(detay);
        Assert.Null(detay!.KlasorId); // rapor yaşıyor, klasörsüz
        Assert.Empty(await new ListFoldersHandler(o.Baglam, Ben()).Handle(new ListFoldersRequest(), default));
    }

    /// <summary>RunReportHandler'ın ISender'ına takılan sahte: gönderilen komutu yakalar.</summary>
    private sealed class SahteGonderici : ISender
    {
        public object? Yakalanan;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Yakalanan = request;
            return Task.FromResult((TResponse)(object)new QueryRunResult([], [], false, 0));
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    [Fact]
    public async Task musteri_ekip_raporunu_calistirabilir_ozeli_calistiramaz()
    {
        // Kod incelemesi K1: /api/sorgu/* Admin+Geliştirici kilitli; rapor koşumu RAPOR görünürlüğüyle
        // ayrı uçtan yapılır — musteri (tasarım yetkisiz) Ekip raporunu koşabilmeli.
        await using var o = await KurAsync();
        var ekip = await new SaveReportHandler(o.Baglam, Ben(), Bekci).Handle(
            new SaveReportRequest(null, "Ekip R", null, OrnekModel(o.HaritaId, o.TabloId),
                VisualizationType.Tablo, ReportSharing.Ekip), default);
        var ozel = await new SaveReportHandler(o.Baglam, Ben(), Bekci).Handle(
            new SaveReportRequest(null, "Ozel R", null, OrnekModel(o.HaritaId, o.TabloId),
                VisualizationType.Tablo, ReportSharing.Ozel), default);

        var musteri = new SahteKb(BaskaKc, IsAdmin: false, CanDesign: false);
        var gonderici = new SahteGonderici();

        await new RunReportHandler(o.Baglam, musteri, gonderici, new KuralsizCozucu())
            .Handle(new RunReportRequest(ekip.Id), default);
        Assert.IsType<RunQueryModelRequest>(gonderici.Yakalanan); // model zincirine gitti

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new RunReportHandler(o.Baglam, musteri, gonderici, new KuralsizCozucu())
                .Handle(new RunReportRequest(ozel.Id), default)); // Özel'i başkası koşamaz
    }

    [Fact]
    public async Task tenant_izolasyonu_baska_tenant_raporu_ne_gorur_ne_calistirir()
    {
        // Test borcu (kod incelemesi): çapraz-tenant NEGATİF senaryo — global query filter
        // başka tenant'ın raporunu Id bilinse bile görünmez kılar.
        await using var o = await KurAsync();
        var ozet = await new SaveReportHandler(o.Baglam, Ben(), Bekci).Handle(
            new SaveReportRequest(null, "A Tenant Raporu", null, OrnekModel(o.HaritaId, o.TabloId),
                VisualizationType.Tablo, ReportSharing.Ekip), default);

        // Aynı SQLite bağlantısı, FARKLI tenant bağlamıyla ikinci context.
        var tenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        await using var baglamB = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(o.Connection).Options,
            new FixedTenantContext(tenantB));

        Assert.Null(await new GetReportHandler(baglamB, Ben()).Handle(new GetReportRequest(ozet.Id), default));
        Assert.Empty(await new ListReportsHandler(baglamB, Ben()).Handle(new ListReportsRequest(), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new RunReportHandler(baglamB, Ben(), new SahteGonderici(), new KuralsizCozucu())
                .Handle(new RunReportRequest(ozet.Id), default));
    }

    [Fact]
    public async Task rapor_mod_gecisi_karsi_alanlari_temizler()
    {
        // Test borcu: model→SQL→model güncellemelerinde karşı modun alanları null'lanmalı.
        await using var o = await KurAsync();
        var vb = new Connection { Ad = "B", Sunucu = "s", Veritabani = "d" };
        o.Baglam.Baglantilar.Add(vb);
        await o.Baglam.SaveChangesAsync();

        var ozet = await new SaveReportHandler(o.Baglam, Ben(), Bekci).Handle(
            new SaveReportRequest(null, "R", null, OrnekModel(o.HaritaId, o.TabloId),
                VisualizationType.Tablo, ReportSharing.Ozel), default);

        // model → SQL
        await new SaveReportHandler(o.Baglam, Ben(), Bekci).Handle(
            new SaveReportRequest(ozet.Id, "R", null, Model: null,
                VisualizationType.Tablo, ReportSharing.Ozel,
                HamSql: "SELECT 1 AS X", BaglantiId: vb.Id), default);
        var d1 = await new GetReportHandler(o.Baglam, Ben()).Handle(new GetReportRequest(ozet.Id), default);
        Assert.Null(d1!.Model);
        Assert.Null(d1.HaritaId);
        Assert.Equal("SELECT 1 AS X", d1.HamSql);

        // SQL → model
        await new SaveReportHandler(o.Baglam, Ben(), Bekci).Handle(
            new SaveReportRequest(ozet.Id, "R", null, OrnekModel(o.HaritaId, o.TabloId),
                VisualizationType.Tablo, ReportSharing.Ozel), default);
        var d2 = await new GetReportHandler(o.Baglam, Ben()).Handle(new GetReportRequest(ozet.Id), default);
        Assert.NotNull(d2!.Model);
        Assert.Null(d2.HamSql);
        Assert.Null(d2.BaglantiId);
    }

    [Fact]
    public async Task sql_raporu_kaydet_getir_roundtrip()
    {
        await using var o = await KurAsync();
        var vb = new Connection { Ad = "Demo", Sunucu = "(localdb)", Veritabani = "KdsDemo" };
        o.Baglam.Baglantilar.Add(vb);
        await o.Baglam.SaveChangesAsync();

        const string sql = "SELECT TOP 5 Ad FROM dbo.Markalar ORDER BY Ad";
        var ozet = await new SaveReportHandler(o.Baglam, Ben(), Bekci).Handle(
            new SaveReportRequest(null, "SQL Raporu", null, Model: null,
                VisualizationType.Tablo, ReportSharing.Ekip, HamSql: sql, BaglantiId: vb.Id), default);

        var detay = await new GetReportHandler(o.Baglam, Ben()).Handle(new GetReportRequest(ozet.Id), default);
        Assert.NotNull(detay);
        Assert.Null(detay!.Model);          // SQL raporunda model yok
        Assert.Null(detay.HaritaId);
        Assert.Equal(sql, detay.HamSql);
        Assert.Equal(vb.Id, detay.BaglantiId);
    }

    [Fact]
    public async Task sql_raporu_select_disi_sql_kayitta_reddedilir()
    {
        await using var o = await KurAsync();
        var vb = new Connection { Ad = "Demo", Sunucu = "(localdb)", Veritabani = "KdsDemo" };
        o.Baglam.Baglantilar.Add(vb);
        await o.Baglam.SaveChangesAsync();

        // Bekçi kayıtta çalışır: DELETE saklanmadan reddedilir (icra beklenmez).
        await Assert.ThrowsAsync<Kds.Execution.QuerySecurityException>(() =>
            new SaveReportHandler(o.Baglam, Ben(), Bekci).Handle(
                new SaveReportRequest(null, "Kötü", null, Model: null,
                    VisualizationType.Tablo, ReportSharing.Ozel,
                    HamSql: "DELETE FROM dbo.Markalar", BaglantiId: vb.Id), default));
        Assert.Empty(await o.Baglam.Raporlar.ToListAsync());
    }
}
