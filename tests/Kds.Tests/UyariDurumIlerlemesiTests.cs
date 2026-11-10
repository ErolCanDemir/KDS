using Kds.Business.Alerts;
using Kds.Business.Identity;
using Kds.Business.Subscriptions;
using Kds.Contracts.Alerts;
using Kds.Contracts.Queries;
using Kds.Contracts.Reports;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>Uyarının en kritik davranışı: <b>durum yalnız bildirim borcu kapandığında ilerler.</b>
///
/// <para>Bayrağı koşulsuz yazmak geçişi YUTAR: "Şimdi Dene" ya da gönderilemeyen bir bildirim
/// durumu tetiklenmişe çevirir, zamanlayıcı sonraki turda "değişiklik yok" der ve gerçek uyarı bir
/// daha HİÇ gitmez. 19 Ağu 2026 canlı denemesinde tam bu yaşandı ve üç turluk sessizlik yanlışlıkla
/// "susma kuralı çalışıyor" diye okundu — bağımsız kod incelemesi yakaladı. Bu testler kuralı
/// kopyalamaz, <see cref="RunAlertHandler"/>'ı gerçekten koşturur.</para></summary>
public sealed class UyariDurumIlerlemesiTests
{
    private static readonly Guid Kiraci = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed record SahteKb(Guid? KeycloakId, bool IsAdmin, bool CanDesign) : IUserContext;

    /// <summary>Raporu koşmuş gibi yapar: tek satır → "Satır sayısı > 0" eşiği aşılır.</summary>
    private sealed class TekSatirGonderici : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default) =>
            Task.FromResult((TResponse)(object)new QueryRunResult(
                [new QueryColumnDto("Deger", "int")], [new object?[] { 42 }], SatirSiniriUygulandi: false, SureMs: 1));

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    /// <summary><paramref name="Basarili"/> false = SMTP yapılandırılmamış (gönderici yalnız günlüğe yazar).</summary>
    private sealed class SahteEposta(bool basarili) : IEpostaGonderici
    {
        public int Cagri;
        public Task<bool> GonderAsync(EpostaMesaji mesaj, CancellationToken ct)
        {
            Cagri++;
            return Task.FromResult(basarili);
        }
    }

    private static async Task<(KdsDbContext Ctx, SqliteConnection C, Guid UyariId)> KurAsync(
        IReadOnlyList<string> alicilar)
    {
        var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(Kiraci));
        await ctx.Database.EnsureCreatedAsync();

        var rapor = new Report
        {
            Id = Guid.NewGuid(), TenantId = Kiraci, Ad = "Kaynak", HamSql = "SELECT 1",
            BaglantiId = Guid.NewGuid(), Paylasim = ReportSharing.Ekip, Gorsellestirme = VisualizationType.Tablo,
        };
        ctx.Raporlar.Add(rapor);
        await ctx.SaveChangesAsync();

        var ozet = await new SaveAlertHandler(ctx, new SahteKb(null, true, true), new KuralsizCozucu()).Handle(
            new SaveAlertRequest(null, "Kayıt var mı", rapor.Id, null, OzetTuru.SatirSayisi,
                UyariOp.Buyuk, 0, alicilar, null, true, false), default);
        return (ctx, c, ozet.Id);
    }

    private static Task<AlertRunResult> Kos(KdsDbContext ctx, Guid id, bool bildirimGonder, SahteEposta eposta) =>
        new RunAlertHandler(ctx, new TekSatirGonderici(), eposta, new KuralsizCozucu())
            .Handle(new RunAlertRequest(id, bildirimGonder), default);

    [Fact]
    public async Task simdi_dene_gercek_uyariyi_CALMAZ()
    {
        var (ctx, c, id) = await KurAsync(["a@x.com"]);
        await using var _1 = c; await using var _2 = ctx;
        var eposta = new SahteEposta(basarili: true);

        // Deneme: eşik aşılı ölçülür ama posta atılmaz.
        var deneme = await Kos(ctx, id, bildirimGonder: false, eposta);
        Assert.True(deneme.Tetiklendi);
        Assert.False(deneme.BildirimGonderildi);
        Assert.Equal(0, eposta.Cagri);

        // KRİTİK: deneme durumu tüketmemeli — zamanlanmış koşum bildirimi HÂLÂ göndermeli.
        var zamanlanmis = await Kos(ctx, id, bildirimGonder: true, eposta);
        Assert.True(zamanlanmis.BildirimGonderildi);
        Assert.Equal(1, eposta.Cagri);

        var guncel = await ctx.Uyarilar.AsNoTracking().FirstAsync(u => u.Id == id);
        Assert.True(guncel.Tetiklendi);
        Assert.NotNull(guncel.SonBildirimTarihi);
    }

    [Fact]
    public async Task gonderilemeyen_bildirim_durumu_TUKETMEZ_sonraki_kosum_tekrar_dener()
    {
        var (ctx, c, id) = await KurAsync(["a@x.com"]);
        await using var _1 = c; await using var _2 = ctx;

        // SMTP yapılandırılmamış → gönderici false döner (yalnız günlüğe yazdı).
        var smtpYok = new SahteEposta(basarili: false);
        var ilk = await Kos(ctx, id, bildirimGonder: true, smtpYok);
        Assert.False(ilk.BildirimGonderildi);

        var araDurum = await ctx.Uyarilar.AsNoTracking().FirstAsync(u => u.Id == id);
        Assert.False(araDurum.Tetiklendi);   // borç kapanmadı → bayrak İLERLEMEDİ

        // SMTP düzeltildi → aynı aşım için bildirim şimdi gitmeli (kalıcı sessizlik YOK).
        var smtpVar = new SahteEposta(basarili: true);
        var ikinci = await Kos(ctx, id, bildirimGonder: true, smtpVar);
        Assert.True(ikinci.BildirimGonderildi);
        Assert.Equal(1, smtpVar.Cagri);
    }

    [Fact]
    public async Task alicisiz_uyari_durumu_TUKETMEZ()
    {
        var (ctx, c, id) = await KurAsync([]);   // alıcı yok
        await using var _1 = c; await using var _2 = ctx;
        var eposta = new SahteEposta(basarili: true);

        var sonuc = await Kos(ctx, id, bildirimGonder: true, eposta);
        Assert.True(sonuc.Tetiklendi);
        Assert.False(sonuc.BildirimGonderildi);
        Assert.Equal(0, eposta.Cagri);

        // Alıcı eklenince bildirim gidebilmeli — bayrak tüketilmiş olsaydı kimse haber almazdı.
        var satir = await ctx.Uyarilar.FirstAsync(u => u.Id == id);
        Assert.False(satir.Tetiklendi);
    }

    [Fact]
    public async Task esik_asili_kalirsa_ikinci_kosumda_SUSAR()
    {
        var (ctx, c, id) = await KurAsync(["a@x.com"]);
        await using var _1 = c; await using var _2 = ctx;
        var eposta = new SahteEposta(basarili: true);

        await Kos(ctx, id, bildirimGonder: true, eposta);   // geçiş → 1 posta
        var ikinci = await Kos(ctx, id, bildirimGonder: true, eposta);

        Assert.False(ikinci.DurumDegisti);
        Assert.False(ikinci.BildirimGonderildi);
        Assert.Equal(1, eposta.Cagri);   // hâlâ tek posta: gürültü yok
    }
}
