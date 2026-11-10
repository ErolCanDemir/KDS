using Kds.Business.Identity;
using Kds.Business.Subscriptions;
using Kds.Contracts.Queries;
using Kds.Contracts.Reports;
using Kds.Contracts.Subscriptions;
using Kds.Data;
using Kds.Data.Entities;
using Kds.Export;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S15 Zamanlanmış Raporlar (rapor aboneliği): CRUD (alıcı/biçim/zamanlama korunur) ve
/// gönderim boru hattı — rapor koşulur (sahte ISender), gerçek exporter'la ek üretilir, e-posta
/// göndericisine (yakalayan sahte) verilir, aboneliğe "Başarılı" durumu yazılır.</summary>
public sealed class SubscriptionTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed record SahteKb(Guid? KeycloakId, bool IsAdmin, bool CanDesign) : IUserContext;

    private static KdsDbContext Baglam(SqliteConnection c) =>
        new(new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(TenantA));

    private static IUserContext Kb() => new SahteKb(null, IsAdmin: true, CanDesign: true);

    /// <summary>Abonelik artık rapora ERİŞİM doğruluyor (müşteri rolüne açılınca eklendi), bu yüzden
    /// hedef rapor gerçekten var olmalı; testler de ürünün kuralına uyar.</summary>
    private static async Task<Guid> RaporEkleAsync(KdsDbContext ctx)
    {
        var rapor = new Report
        {
            Id = Guid.NewGuid(), TenantId = TenantA, Ad = "Test Raporu",
            Paylasim = ReportSharing.Ekip, Gorsellestirme = VisualizationType.Tablo,
        };
        ctx.Raporlar.Add(rapor);
        await ctx.SaveChangesAsync();
        return rapor.Id;
    }

    [Fact]
    public async Task kaydet_getir_alicilari_bicimi_zamanlamayi_korur()
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var _ = c;
        await using var ctx = Baglam(c);
        await ctx.Database.EnsureCreatedAsync();

        var raporId = await RaporEkleAsync(ctx);
        var ozet = await new SaveSubscriptionHandler(ctx, Kb(), new KuralsizCozucu()).Handle(new SaveSubscriptionRequest(
            null, "Haftalık Özet", raporId, "Xlsx",
            ["a@x.com", "b@y.com"], "0 8 * * 1", true), default);

        Assert.Equal(2, ozet.AliciSayisi);
        Assert.Equal("Xlsx", ozet.Bicim);
        Assert.True(ozet.Aktif);

        var detay = await new GetSubscriptionHandler(ctx, Kb()).Handle(new GetSubscriptionRequest(ozet.Id), default);
        Assert.NotNull(detay);
        Assert.Equal("0 8 * * 1", detay!.Zamanlama);
        Assert.Equal(new[] { "a@x.com", "b@y.com" }, detay.Alicilar);

        var liste = await new ListSubscriptionsHandler(ctx, Kb()).Handle(new ListSubscriptionsRequest(), default);
        Assert.Single(liste);
        Assert.Equal(2, liste[0].AliciSayisi);
    }

    [Fact]
    public async Task sil_bulunmayanda_false_bulunani_siler()
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var _ = c;
        await using var ctx = Baglam(c);
        await ctx.Database.EnsureCreatedAsync();

        Assert.False(await new DeleteSubscriptionHandler(ctx, Kb()).Handle(new DeleteSubscriptionRequest(Guid.NewGuid()), default));

        var raporId = await RaporEkleAsync(ctx);
        var ozet = await new SaveSubscriptionHandler(ctx, Kb(), new KuralsizCozucu()).Handle(new SaveSubscriptionRequest(
            null, "X", raporId, "Pdf", ["a@x.com"], null, true), default);
        Assert.True(await new DeleteSubscriptionHandler(ctx, Kb()).Handle(new DeleteSubscriptionRequest(ozet.Id), default));
        Assert.Equal(0, await ctx.Abonelikler.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task calistir_raporu_kosar_pdf_uretir_epostalar_durumu_yazar()
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var _ = c;
        await using var ctx = Baglam(c);
        await ctx.Database.EnsureCreatedAsync();

        // SQL raporu (HamSql dalı) — sahte ISender gerçek çalıştırmayı taklit eder.
        var rapor = new Report
        {
            Ad = "Satış", HamSql = "SELECT 1", BaglantiId = Guid.NewGuid(),
            Paylasim = ReportSharing.Ozel, Gorsellestirme = VisualizationType.Tablo,
        };
        ctx.Raporlar.Add(rapor);
        await ctx.SaveChangesAsync();

        var abonelik = await new SaveSubscriptionHandler(ctx, Kb(), new KuralsizCozucu()).Handle(new SaveSubscriptionRequest(
            null, "Günlük Satış", rapor.Id, "Pdf", ["patron@firma.com", "analiz@firma.com"], null, true), default);

        var eposta = new YakalayanEposta();
        var sonuc = await new RunSubscriptionHandler(ctx, new SabitSonucGonderici(), new ResultExporter(), eposta, new KuralsizCozucu())
            .Handle(new RunSubscriptionRequest(abonelik.Id), default);

        // Sonuç: 2 alıcı, ek pdf, satır sayısı sahte sonuçtan.
        Assert.Equal(2, sonuc.AliciSayisi);
        Assert.Equal(1, sonuc.SatirSayisi);
        Assert.EndsWith(".pdf", sonuc.DosyaAdi);

        // E-posta gerçekten "gönderildi" (yakalandı) — 2 alıcı + PDF eki (%PDF sihirli baytları).
        Assert.NotNull(eposta.Son);
        Assert.Equal(2, eposta.Son!.Alicilar.Count);
        Assert.NotNull(eposta.Son.Ek);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(eposta.Son.Ek!.Icerik, 0, 5));

        // Abonelik durumu "Başarılı" + son gönderim damgası yazıldı.
        var guncel = await ctx.Abonelikler.AsNoTracking().FirstAsync(x => x.Id == abonelik.Id);
        Assert.Equal("Basarili", guncel.SonDurum);
        Assert.Equal(2, guncel.SonAliciSayisi);
        Assert.NotNull(guncel.SonGonderimTarihi);
        Assert.Null(guncel.SonHata);
    }

    [Fact]
    public async Task parametreli_model_raporu_kayitli_degerlerle_kosulur()
    {
        // M24: abonelikte saklanan parametre değerleri, gönderimde RunQueryModelRequest'e geçer
        // (eskiden null gidiyor, zorunlu parametreli rapor "değer girin" hatasıyla düşüyordu).
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var _ = c;
        await using var ctx = Baglam(c);
        await ctx.Database.EnsureCreatedAsync();

        var haritaId = Guid.NewGuid();
        var tabloId = Guid.NewGuid();
        var rapor = new Report
        {
            Ad = "Parametreli", HaritaId = haritaId,
            ModelJson = $"{{\"haritaId\":\"{haritaId}\",\"alanlar\":[{{\"tabloId\":\"{tabloId}\",\"kolon\":\"X\",\"toplama\":\"Yok\",\"takmaAd\":null}}],\"filtre\":null,\"siralama\":[],\"satirLimiti\":null}}",
            Paylasim = ReportSharing.Ozel, Gorsellestirme = VisualizationType.Tablo,
        };
        ctx.Raporlar.Add(rapor);
        await ctx.SaveChangesAsync();

        var abonelik = await new SaveSubscriptionHandler(ctx, Kb(), new KuralsizCozucu()).Handle(new SaveSubscriptionRequest(
            null, "Parametreli Abone", rapor.Id, "Pdf", ["a@x.com"], null, true,
            new Dictionary<string, object?>
            {
                ["don"] = new Dictionary<string, object?> { ["baslangic"] = "2026-01-01", ["bitis"] = "$bugun" },
            }), default);

        var gonderici = new SabitSonucGonderici();
        await new RunSubscriptionHandler(ctx, gonderici, new ResultExporter(), new YakalayanEposta(), new KuralsizCozucu())
            .Handle(new RunSubscriptionRequest(abonelik.Id), default);

        var istek = Assert.IsType<Kds.Contracts.QueryModel.RunQueryModelRequest>(gonderici.Yakalanan);
        Assert.NotNull(istek.ParametreDegerleri);
        Assert.True(istek.ParametreDegerleri!.ContainsKey("don"), "kayıtlı parametre değerleri koşuma geçmedi");
    }

    /// <summary>RunSubscriptionHandler'ın ISender'ına takılan sahte: rapor çalıştırmayı taklit eder,
    /// tek kolon/tek satırlık sonuç döner (export'un içerik üretmesi için).</summary>
    private sealed class SabitSonucGonderici : ISender
    {
        public object? Yakalanan;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Yakalanan = request;
            return Task.FromResult((TResponse)(object)new QueryRunResult(
                [new QueryColumnDto("Deger", "int")], [new object?[] { 42 }], SatirSiniriUygulandi: false, SureMs: 3));
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

    private sealed class YakalayanEposta : IEpostaGonderici
    {
        public EpostaMesaji? Son;
        public Task<bool> GonderAsync(EpostaMesaji mesaj, CancellationToken cancellationToken)
        {
            Son = mesaj;
            return Task.FromResult(true);
        }
    }
}
