using System.Text.Json;
using FluentValidation;
using Kds.Business.Embedding;
using Kds.Business.Identity;
using Kds.Contracts.Embedding;
using Kds.Contracts.Identity;
using Kds.Contracts.QueryModel;
using Kds.Contracts.Queries;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S42 — GÖMME güvenliği. Bu testler bir özelliğin "çalıştığını" değil, kötüye
/// kullanımın KAPALI olduğunu sabitler: gömme dışarıya açılan tek yüzeydir ve buradaki her
/// gevşeme doğrudan veri sızıntısıdır.</summary>
public class GommeTests
{
    private static readonly Guid Kiraci = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid BaskaKiraci = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid RaporId = Guid.Parse("bbbb1111-0000-0000-0000-000000000001");
    private static readonly Guid HamSqlRaporId = Guid.Parse("bbbb1111-0000-0000-0000-000000000002");
    private static readonly Guid KullaniciId = Guid.Parse("cccc1111-0000-0000-0000-000000000001");
    private static readonly Guid KeycloakId = Guid.Parse("dddd1111-0000-0000-0000-000000000001");

    /// <summary>Anahtarı KURAN admin bağlamı (yerel kullanıcı kaydına KeycloakId ile bağlanır).</summary>
    private sealed record SahteKb(Guid? KeycloakId, bool IsAdmin, bool CanDesign) : IUserContext;

    /// <summary>Anahtarı olduğu gibi taşıyan sahte koruyucu — imza mantığı Api katmanında (Data
    /// Protection) sınanır; burada sınanan İŞ kurallarıdır.</summary>
    private sealed class SahteKoruyucu : IGommeAnahtarKoruyucu
    {
        public string Uret(Guid anahtarId) => "ANAHTAR:" + anahtarId.ToString("N");
        public Guid? Coz(string anahtar) =>
            anahtar.StartsWith("ANAHTAR:", StringComparison.Ordinal)
            && Guid.TryParse(anahtar["ANAHTAR:".Length..], out var id) ? id : null;
    }

    private sealed class SahteKapsam : IKiraciKapsami
    {
        public Guid? Baglanan;
        public IDisposable Baglan(Guid kiraciId) { Baglanan = kiraciId; return new Bos(); }
        private sealed class Bos : IDisposable { public void Dispose() { } }
    }

    private sealed class SahteGonderici : ISender
    {
        public RunQueryModelRequest? Kosulan;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is RunQueryModelRequest k)
            {
                Kosulan = k;
                return Task.FromResult((TResponse)(object)new QueryRunResult([], [], false, 5));
            }
            throw new NotSupportedException(request.GetType().Name);
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

    private static async Task<KdsDbContext> BaglamAsync(SqliteConnection c)
    {
        await c.OpenAsync();
        var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(Kiraci));
        await ctx.Database.EnsureCreatedAsync();

        ctx.Tenantlar.Add(new Tenant { Id = Kiraci, Ad = "Gömme A.Ş.", MarkaRengi = "#0D8B8B" });
        ctx.Kullanicilar.Add(new User
        {
            Id = KullaniciId, TenantId = Kiraci, KullaniciAdi = "bayi1", EPosta = "bayi1@firma.com",
            KeycloakId = KeycloakId,
        });
        var model = new Contracts.QueryModel.QueryModel(Guid.NewGuid(),
            [new SelectedField(Guid.NewGuid(), "Tutar", ToplamaTuru.Topla)], null, [], null);
        ctx.Raporlar.AddRange(
            new Report { Id = RaporId, TenantId = Kiraci, Ad = "Bayi Cirosu", ModelJson = JsonSerializer.Serialize(model) },
            new Report { Id = HamSqlRaporId, TenantId = Kiraci, Ad = "Elle SQL", HamSql = "SELECT 1", BaglantiId = Guid.NewGuid() });
        await ctx.SaveChangesAsync();
        return ctx;
    }

    private static CreateEmbedTokenRequest Istek(
        IReadOnlyList<string>? roller = null, IReadOnlyList<string>? alanlar = null, Guid? raporId = null) =>
        new("Bayi portalı", raporId ?? RaporId, null, KeycloakId,
            roller ?? ["bayi"], alanlar ?? ["portal.firma.com"], 30);

    private static async Task<EmbedToken> AnahtarKurAsync(KdsDbContext ctx, string alanlar = "portal.firma.com")
    {
        var kimlik = new CalistiranKullanici(["bayi"], "bayi1", "bayi1@firma.com", new Dictionary<string, string>());
        var a = new EmbedToken
        {
            TenantId = Kiraci, Ad = "Bayi portalı", RaporId = RaporId,
            KimlikJson = JsonSerializer.Serialize(kimlik), IzinliAlanlar = alanlar,
            GecerlilikBitis = DateTimeOffset.UtcNow.AddDays(30),
        };
        ctx.GommeAnahtarlari.Add(a);
        await ctx.SaveChangesAsync();
        return a;
    }

    // ---------- Kurulum kuralları ----------

    [Fact]
    public void dogrulama_ROLSUZ_anahtari_reddeder()
    {
        /* Satır kuralları "hangi role uygulanır" diye bakar; ROL TAŞIMAYAN kimlik role bağlı
         * kuralları ATLAR ve beklenenden geniş veri görür. Rolsüz gömme, RLS'i sessizce delmenin
         * en kolay yoludur. */
        var s = new CreateEmbedTokenValidation().Validate(Istek(roller: []));
        Assert.Contains(s.Errors, e => e.PropertyName == nameof(CreateEmbedTokenRequest.Roller));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Admin")]
    [InlineData("ADMIN")]
    public void dogrulama_ADMIN_rolunu_reddeder(string rol)
    {
        // Admin RLS'ten MUAFTIR: admin rollü bir anahtar bütün satır kurallarını devre dışı
        // bırakır ve dış siteye kısıtsız veri açardı. Büyük/küçük harfle de kaçamaz.
        var s = new CreateEmbedTokenValidation().Validate(Istek(roller: [rol]));
        Assert.Contains(s.Errors, e => e.ErrorMessage.Contains("admin rolü verilemez"));
    }

    [Fact]
    public void dogrulama_IZINLI_ALAN_zorunlu()
    {
        // Güvenli varsayılan KAPALI: "her yerden gömülebilir" seçeneği bilerek yok.
        var s = new CreateEmbedTokenValidation().Validate(Istek(alanlar: []));
        Assert.Contains(s.Errors, e => e.PropertyName == nameof(CreateEmbedTokenRequest.IzinliAlanlar));
    }

    [Fact]
    public async Task uret_HAM_SQL_raporunu_reddeder()
    {
        /* Ham SQL raporunda satır güvenliği UYGULANAMIYOR (RunReportHandler aynı sebeple kurala
         * tabi kullanıcıyı reddediyor). Gömme dışarıya açılan yüzeydir: RLS'in uygulanamadığı
         * içeriği oraya koymak, kuralları sessizce delmek olurdu. */
        await using var c = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await BaglamAsync(c);

        var hata = await Assert.ThrowsAsync<ValidationException>(() =>
            new CreateEmbedTokenHandler(ctx, new SahteKoruyucu(), new SahteKb(KeycloakId, true, true))
                .Handle(Istek(raporId: HamSqlRaporId), CancellationToken.None));

        Assert.Contains("gömülemez", hata.Message);
    }

    [Fact]
    public async Task uret_kimligi_DONDURUR_ve_anahtari_bir_kez_dondurur()
    {
        await using var c = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await BaglamAsync(c);

        var sonuc = await new CreateEmbedTokenHandler(ctx, new SahteKoruyucu(), new SahteKb(KeycloakId, true, true))
            .Handle(Istek(), CancellationToken.None);

        Assert.StartsWith("ANAHTAR:", sonuc.Anahtar);
        Assert.Contains("<iframe", sonuc.GomKodu);

        var kayit = await ctx.GommeAnahtarlari.FirstAsync();
        var kimlik = JsonSerializer.Deserialize<CalistiranKullanici>(kayit.KimlikJson)!;
        Assert.Equal(["bayi"], kimlik.Roller);          // roller İSTEKTEN donduruldu
        Assert.Equal("bayi1", kimlik.KullaniciAdi);     // kullanıcı adı KAYITTAN
        // Anahtarın kendisi saklanmaz: saklanan sır, sızabilecek ikinci kopyadır.
        Assert.DoesNotContain(sonuc.Anahtar, kayit.KimlikJson);
    }

    // ---------- Gömülü koşum: reddetme zinciri ----------

    private RunEmbeddedHandler Kosucu(KdsDbContext ctx, out SahteGonderici g, out SahteKapsam k)
    {
        g = new SahteGonderici();
        k = new SahteKapsam();
        return new RunEmbeddedHandler(ctx, new SahteKoruyucu(), k, g);
    }

    [Fact]
    public async Task kosum_GECERSIZ_anahtari_reddeder()
    {
        await using var c = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await BaglamAsync(c);
        var h = Kosucu(ctx, out _, out _);

        await Assert.ThrowsAsync<GommeReddi>(() =>
            h.Handle(new RunEmbeddedRequest("kurcalanmis", "https://portal.firma.com"), CancellationToken.None));
    }

    [Fact]
    public async Task kosum_IPTAL_edilmis_anahtari_reddeder()
    {
        await using var c = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await BaglamAsync(c);
        var a = await AnahtarKurAsync(ctx);
        a.IptalTarihi = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        var h = Kosucu(ctx, out _, out _);

        await Assert.ThrowsAsync<GommeReddi>(() =>
            h.Handle(new RunEmbeddedRequest("ANAHTAR:" + a.Id.ToString("N"), "https://portal.firma.com"), CancellationToken.None));
    }

    [Fact]
    public async Task kosum_SURESI_dolmus_anahtari_reddeder()
    {
        await using var c = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await BaglamAsync(c);
        var a = await AnahtarKurAsync(ctx);
        a.GecerlilikBitis = DateTimeOffset.UtcNow.AddMinutes(-1);
        await ctx.SaveChangesAsync();
        var h = Kosucu(ctx, out _, out _);

        await Assert.ThrowsAsync<GommeReddi>(() =>
            h.Handle(new RunEmbeddedRequest("ANAHTAR:" + a.Id.ToString("N"), "https://portal.firma.com"), CancellationToken.None));
    }

    [Theory]
    [InlineData("https://saldirgan.com")]          // izinsiz site
    [InlineData("https://portal.firma.com.evil.co")] // benzer ad — alt dize eşleşmesi olmamalı
    [InlineData(null)]                              // kaynaksız (doğrudan) çağrı
    public async Task kosum_IZINSIZ_kaynagi_reddeder(string? kaynak)
    {
        /* Çalınan bir anahtar saldırganın kendi sayfasında işe yaramaz. Alt alan adı joker'i
         * bilerek yok: "*.firma.com" olsaydı ele geçirilmiş tek bir alt alan yeterdi. */
        await using var c = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await BaglamAsync(c);
        var a = await AnahtarKurAsync(ctx);
        var h = Kosucu(ctx, out _, out _);

        await Assert.ThrowsAsync<GommeReddi>(() =>
            h.Handle(new RunEmbeddedRequest("ANAHTAR:" + a.Id.ToString("N"), kaynak), CancellationToken.None));
    }

    // ---------- Gömülü koşum: doğru yol ----------

    [Fact]
    public async Task kosum_izinli_kaynakta_ANAHTARIN_KIMLIGIYLE_kosar()
    {
        await using var c = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await BaglamAsync(c);
        var a = await AnahtarKurAsync(ctx);
        var h = Kosucu(ctx, out var gonderici, out var kapsam);

        var sonuc = await h.Handle(
            new RunEmbeddedRequest("ANAHTAR:" + a.Id.ToString("N"), "https://portal.firma.com/sayfa"),
            CancellationToken.None);

        // Kiracı anahtardan öğrenildi ve bağlandı (oturum yok).
        Assert.Equal(Kiraci, kapsam.Baglanan);
        // Koşum SABİT servis hesabıyla değil, anahtarın taşıdığı kimlikle yapıldı → RLS uygulanır.
        Assert.NotNull(gonderici.Kosulan?.KosanKimlik);
        Assert.Equal("bayi1", gonderici.Kosulan!.KosanKimlik!.KullaniciAdi);
        Assert.Equal(["bayi"], gonderici.Kosulan.KosanKimlik.Roller);
        Assert.Equal(RaporId, gonderici.Kosulan.RaporId);
        // Beyaz-etiket: kuruluş markası gömülü görünüme taşınır.
        Assert.Equal("Gömme A.Ş.", sonuc.Marka.Ad);
        Assert.Equal("#0D8B8B", sonuc.Marka.MarkaRengi);
    }

    [Fact]
    public async Task kosum_basarili_olunca_SON_KULLANIMI_damgalar()
    {
        await using var c = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await BaglamAsync(c);
        var a = await AnahtarKurAsync(ctx);
        Assert.Null(a.SonKullanim);
        var h = Kosucu(ctx, out _, out _);

        await h.Handle(new RunEmbeddedRequest("ANAHTAR:" + a.Id.ToString("N"), "https://portal.firma.com"), CancellationToken.None);

        var tazelenmis = await ctx.GommeAnahtarlari.AsNoTracking().FirstAsync(x => x.Id == a.Id);
        Assert.NotNull(tazelenmis.SonKullanim);
    }

    [Fact]
    public async Task kosum_BASKA_KIRACININ_raporunu_acmaz()
    {
        /* Anahtar kiracı A'ya ait ama rapor kiracı B'ye taşınmışsa (ya da kimlik uydurulmuşsa)
         * koşum düşer. Gömülü yol query filter'ı bilerek atladığı için bu kontrol ELLE yazılıdır
         * — atlanırsa kiracılar arası sızıntı olurdu. */
        await using var c = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await BaglamAsync(c);
        var a = await AnahtarKurAsync(ctx);
        var rapor = await ctx.Raporlar.FirstAsync(r => r.Id == RaporId);
        rapor.TenantId = BaskaKiraci;
        await ctx.SaveChangesAsync();
        var h = Kosucu(ctx, out _, out _);

        await Assert.ThrowsAsync<GommeReddi>(() =>
            h.Handle(new RunEmbeddedRequest("ANAHTAR:" + a.Id.ToString("N"), "https://portal.firma.com"), CancellationToken.None));
    }

    [Fact]
    public async Task iptal_kaydi_SILMEZ_isaretler()
    {
        // Silmek, sızıntı sonrası "hangi anahtar ne zaman kapatıldı" soruşturmasını imkânsız kılardı.
        await using var c = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await BaglamAsync(c);
        var a = await AnahtarKurAsync(ctx);

        var ilk = await new RevokeEmbedTokenHandler(ctx).Handle(new RevokeEmbedTokenRequest(a.Id), CancellationToken.None);
        var ikinci = await new RevokeEmbedTokenHandler(ctx).Handle(new RevokeEmbedTokenRequest(a.Id), CancellationToken.None);

        Assert.True(ilk);
        Assert.False(ikinci);                       // ikinci kez iptal edilemez
        var kayit = await ctx.GommeAnahtarlari.AsNoTracking().FirstAsync(x => x.Id == a.Id);
        Assert.NotNull(kayit.IptalTarihi);          // kayıt duruyor
    }
}
