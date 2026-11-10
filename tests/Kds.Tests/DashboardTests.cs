using Kds.Business.Dashboards;
using Kds.Business.Identity;
using Kds.Contracts.Dashboards;
using Kds.Contracts.Reports;
using Kds.Data;
using Kds.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S11 Panolar: kaydet/getir round-trip (sıralı rapor id'leri korunur), Özel/Ekip
/// görünürlüğü (sahip · başkası · admin) ve sahiplik-korumalı silme.</summary>
public sealed class DashboardTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BenKc = Guid.Parse("bbbbbbbb-1111-0000-0000-000000000001");
    private static readonly Guid BaskaKc = Guid.Parse("bbbbbbbb-1111-0000-0000-000000000002");

    private sealed record SahteKb(Guid? KeycloakId, bool IsAdmin, bool CanDesign) : IUserContext;

    private sealed class Ortam : IAsyncDisposable
    {
        public required SqliteConnection Connection { get; init; }
        public required KdsDbContext Baglam { get; init; }
        public Guid BenId; public Guid BaskaId;
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
        await context.SaveChangesAsync();
        return new Ortam { Connection = baglanti, Baglam = context, BenId = ben.Id, BaskaId = baska.Id };
    }

    private static SahteKb Ben(bool admin = false) => new(BenKc, admin, true);
    private static SahteKb Baska(bool admin = false) => new(BaskaKc, admin, true);

    [Fact]
    public async Task kaydet_getir_rapor_idlerini_sirayla_korur()
    {
        await using var o = await KurAsync();
        var r1 = Guid.NewGuid(); var r2 = Guid.NewGuid();

        var ozet = await new SaveDashboardHandler(o.Baglam, Ben())
            .Handle(new SaveDashboardRequest(null, "Satış Panom", ReportSharing.Ozel, [r1, r2]), default);
        Assert.Equal(2, ozet.RaporSayisi);
        Assert.True(ozet.BenimMi);

        var detay = await new GetDashboardHandler(o.Baglam, Ben())
            .Handle(new GetDashboardRequest(ozet.Id), default);
        Assert.NotNull(detay);
        Assert.Equal(new[] { r1, r2 }, detay!.Raporlar);   // sıra korunur
        Assert.Equal("Satış Panom", detay.Ad);
    }

    [Fact]
    public async Task ozel_pano_baskasina_gorunmez_ekip_ve_admin_gorur()
    {
        await using var o = await KurAsync();
        await new SaveDashboardHandler(o.Baglam, Ben()).Handle(new SaveDashboardRequest(null, "Özel", ReportSharing.Ozel, []), default);
        await new SaveDashboardHandler(o.Baglam, Ben()).Handle(new SaveDashboardRequest(null, "Ekip", ReportSharing.Ekip, []), default);

        var baskaListe = await new ListDashboardsHandler(o.Baglam, Baska()).Handle(new ListDashboardsRequest(), default);
        Assert.Equal("Ekip", Assert.Single(baskaListe).Ad);   // başkası yalnız Ekip'i görür

        var adminListe = await new ListDashboardsHandler(o.Baglam, Baska(admin: true)).Handle(new ListDashboardsRequest(), default);
        Assert.Equal(2, adminListe.Count);   // admin hepsini görür
    }

    [Fact]
    public async Task yerlesim_kaydedilir_ve_rapor_degisince_korunur()
    {
        await using var o = await KurAsync();
        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();

        // Widget yerleşimiyle kaydet
        var ozet = await new SaveDashboardHandler(o.Baglam, Ben())
            .Handle(new SaveDashboardRequest(null, "Yerleşimli", ReportSharing.Ozel, [r1, r2],
                [new DashboardWidget(r1, 0, 0, 4, 6), new DashboardWidget(r2, 4, 0, 8, 6)]), default);

        var detay = await new GetDashboardHandler(o.Baglam, Ben()).Handle(new GetDashboardRequest(ozet.Id), default);
        Assert.NotNull(detay);
        Assert.Equal(2, detay!.Yerlesim.Count);
        Assert.Equal(8, detay.Yerlesim.Single(w => w.RaporId == r2).W);   // boyut round-trip

        // Editör raporu değiştirir ama yerleşim GÖNDERMEZ (null) → yerleşim KORUNUR.
        await new SaveDashboardHandler(o.Baglam, Ben())
            .Handle(new SaveDashboardRequest(ozet.Id, "Yerleşimli", ReportSharing.Ozel, [r1], null), default);
        var detay2 = await new GetDashboardHandler(o.Baglam, Ben()).Handle(new GetDashboardRequest(ozet.Id), default);
        Assert.Equal(2, detay2!.Yerlesim.Count);       // yerleşim korundu (null → dokunulmadı)
        Assert.Equal(new[] { r1 }, detay2.Raporlar);   // rapor listesi güncellendi
    }

    [Fact]
    public async Task sil_yalniz_sahibi_baskasi_false()
    {
        await using var o = await KurAsync();
        var ozet = await new SaveDashboardHandler(o.Baglam, Ben())
            .Handle(new SaveDashboardRequest(null, "P", ReportSharing.Ekip, []), default);

        Assert.False(await new DeleteDashboardHandler(o.Baglam, Baska()).Handle(new DeleteDashboardRequest(ozet.Id), default));
        Assert.True(await new DeleteDashboardHandler(o.Baglam, Ben()).Handle(new DeleteDashboardRequest(ozet.Id), default));
    }

    // ---- S43: ana sayfa panosu ----

    [Fact]
    public async Task ana_sayfa_panosu_secilir_listede_isaretlenir_ve_kaldirilir()
    {
        await using var o = await KurAsync();
        var ozet = await new SaveDashboardHandler(o.Baglam, Ben())
            .Handle(new SaveDashboardRequest(null, "Satış", ReportSharing.Ekip, []), default);

        Assert.True(await new SetHomeDashboardHandler(o.Baglam, Ben())
            .Handle(new SetHomeDashboardRequest(ozet.Id), default));
        Assert.Equal(ozet.Id, (await new GetHomeDashboardHandler(o.Baglam, Ben())
            .Handle(new GetHomeDashboardRequest(), default)).PanoId);

        var liste = await new ListDashboardsHandler(o.Baglam, Ben()).Handle(new ListDashboardsRequest(), default);
        Assert.True(Assert.Single(liste).AnaSayfamMi);

        // null → seçim kalkar
        Assert.True(await new SetHomeDashboardHandler(o.Baglam, Ben())
            .Handle(new SetHomeDashboardRequest(null), default));
        Assert.Null((await new GetHomeDashboardHandler(o.Baglam, Ben()).Handle(new GetHomeDashboardRequest(), default)).PanoId);
    }

    [Fact]
    public async Task ana_sayfa_secimi_kullaniciya_ozeldir()
    {
        await using var o = await KurAsync();
        var ozet = await new SaveDashboardHandler(o.Baglam, Ben())
            .Handle(new SaveDashboardRequest(null, "Ekip Panosu", ReportSharing.Ekip, []), default);

        await new SetHomeDashboardHandler(o.Baglam, Ben()).Handle(new SetHomeDashboardRequest(ozet.Id), default);

        // Aynı panoyu gören BAŞKA kullanıcının ana sayfası etkilenmez.
        Assert.Null((await new GetHomeDashboardHandler(o.Baglam, Baska()).Handle(new GetHomeDashboardRequest(), default)).PanoId);
        var baskaListe = await new ListDashboardsHandler(o.Baglam, Baska()).Handle(new ListDashboardsRequest(), default);
        Assert.False(Assert.Single(baskaListe).AnaSayfamMi);
    }

    [Fact]
    public async Task gorunmeyen_pano_ana_sayfa_yapilamaz()
    {
        await using var o = await KurAsync();
        // Ben'in ÖZEL panosu — Baska bunu görmez, dolayısıyla ana sayfası yapamamalı.
        var ozet = await new SaveDashboardHandler(o.Baglam, Ben())
            .Handle(new SaveDashboardRequest(null, "Gizli", ReportSharing.Ozel, []), default);

        Assert.False(await new SetHomeDashboardHandler(o.Baglam, Baska())
            .Handle(new SetHomeDashboardRequest(ozet.Id), default));
        Assert.Null((await new GetHomeDashboardHandler(o.Baglam, Baska()).Handle(new GetHomeDashboardRequest(), default)).PanoId);
    }

    [Fact]
    public async Task secimden_sonra_pano_gorunmez_olursa_ana_sayfa_null_doner()
    {
        await using var o = await KurAsync();
        var ozet = await new SaveDashboardHandler(o.Baglam, Ben())
            .Handle(new SaveDashboardRequest(null, "Ekip", ReportSharing.Ekip, []), default);

        // Baska panoyu görüyorken ana sayfası yapar.
        Assert.True(await new SetHomeDashboardHandler(o.Baglam, Baska())
            .Handle(new SetHomeDashboardRequest(ozet.Id), default));

        // Sahibi paylaşımı Özel'e çeker → seçim ayakta kalır ama OKUMA null döner.
        await new SaveDashboardHandler(o.Baglam, Ben())
            .Handle(new SaveDashboardRequest(ozet.Id, "Ekip", ReportSharing.Ozel, []), default);
        Assert.Null((await new GetHomeDashboardHandler(o.Baglam, Baska()).Handle(new GetHomeDashboardRequest(), default)).PanoId);

        // Paylaşım geri açılınca tercih yerinde: kayıt temizlenmediği için seçim geri gelir.
        await new SaveDashboardHandler(o.Baglam, Ben())
            .Handle(new SaveDashboardRequest(ozet.Id, "Ekip", ReportSharing.Ekip, []), default);
        Assert.Equal(ozet.Id, (await new GetHomeDashboardHandler(o.Baglam, Baska())
            .Handle(new GetHomeDashboardRequest(), default)).PanoId);
    }

    [Fact]
    public async Task silinen_pano_ana_sayfayi_cokertmez()
    {
        await using var o = await KurAsync();
        var ozet = await new SaveDashboardHandler(o.Baglam, Ben())
            .Handle(new SaveDashboardRequest(null, "Geçici", ReportSharing.Ekip, []), default);
        await new SetHomeDashboardHandler(o.Baglam, Ben()).Handle(new SetHomeDashboardRequest(ozet.Id), default);

        await new DeleteDashboardHandler(o.Baglam, Ben()).Handle(new DeleteDashboardRequest(ozet.Id), default);

        // FK yok: kullanıcı kaydı ayakta, okuma sessizce null (ana sayfa karşılamaya düşer).
        Assert.Null((await new GetHomeDashboardHandler(o.Baglam, Ben()).Handle(new GetHomeDashboardRequest(), default)).PanoId);
    }
}
