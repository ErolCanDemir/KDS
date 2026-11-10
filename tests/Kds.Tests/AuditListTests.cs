using Kds.Business.Audit;
using Kds.Contracts.Audit;
using Kds.Data;
using Kds.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S13 Part2 denetim okuma: filtre (başarılı/arama) + sayfalama + kullanıcı/bağlantı ad çözümü
/// (ilişkili alt-sorgu) + en-yeni-önce (DateTimeOffset ORDER BY). GERÇEK SQL Server'a (LocalDB) karşı —
/// SQLite DateTimeOffset ORDER BY'ı desteklemez ve sunucu-tarafı sayfalama+sıralama üretimde SQL Server'da.
/// Kendi geçici DB'sini yaratır/siler.</summary>
public sealed class AuditListTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static DateTimeOffset T(int dakika) => new(2026, 8, 9, 10, dakika, 0, TimeSpan.Zero);

    [Fact]
    public async Task filtre_sayfalama_ve_ad_cozumu()
    {
        var dbAdi = "KdsAuditTest_" + Guid.NewGuid().ToString("N");
        var cs = $@"Server=(localdb)\MSSQLLocalDB;Database={dbAdi};Trusted_Connection=True;TrustServerCertificate=True";
        var opts = new DbContextOptionsBuilder<KdsDbContext>().UseSqlServer(cs).Options;

        await using var ctx = new KdsDbContext(opts, new FixedTenantContext(TenantA));
        await ctx.Database.EnsureCreatedAsync();
        try
        {
            var kul = new User { KeycloakId = Guid.NewGuid(), KullaniciAdi = "testci" };
            ctx.Kullanicilar.Add(kul);
            var bag = new Connection { Ad = "Depo", Sunucu = "s", Veritabani = "d" };
            ctx.Baglantilar.Add(bag);
            await ctx.SaveChangesAsync();

            ctx.DenetimKayitlari.AddRange(
                new AuditRecord { Sql = "SELECT a", Basarili = true, SureMs = 10, SatirSayisi = 5, KullaniciId = kul.Id, BaglantiId = bag.Id, OlusturmaTarihi = T(1) },
                new AuditRecord { Sql = "DELETE x", Basarili = false, SureMs = 2, Hata = "engellendi", KullaniciId = kul.Id, OlusturmaTarihi = T(2) },
                new AuditRecord { Sql = "SELECT b", Basarili = true, SureMs = 7, SatirSayisi = 3, OlusturmaTarihi = T(3) });
            await ctx.SaveChangesAsync();

            var handler = new ListAuditHandler(ctx);

            // Hepsi — en yeni önce + ad çözümü.
            var hepsi = await handler.Handle(new ListAuditRequest(), default);
            Assert.Equal(3, hepsi.Toplam);
            Assert.Equal("SELECT b", hepsi.Kayitlar[0].Sql);         // T3 (en yeni)
            Assert.Null(hepsi.Kayitlar[0].KullaniciAd);              // T3 kullanıcısız
            Assert.Equal("testci", hepsi.Kayitlar[1].KullaniciAd);  // T2 kullanıcı adı
            Assert.Equal("Depo", hepsi.Kayitlar[2].BaglantiAd);     // T1 bağlantı adı

            // Yalnız başarısız.
            var hatali = await handler.Handle(new ListAuditRequest(Basarili: false), default);
            Assert.Single(hatali.Kayitlar);
            Assert.Equal("DELETE x", hatali.Kayitlar[0].Sql);
            Assert.Equal("engellendi", hatali.Kayitlar[0].Hata);

            // Arama (SQL metninde).
            var ara = await handler.Handle(new ListAuditRequest(Arama: "SELECT"), default);
            Assert.Equal(2, ara.Toplam);

            // Sayfalama.
            var sayfa0 = await handler.Handle(new ListAuditRequest(Sayfa: 0, SayfaBoyut: 2), default);
            Assert.Equal(3, sayfa0.Toplam);
            Assert.Equal(2, sayfa0.Kayitlar.Count);
            var sayfa1 = await handler.Handle(new ListAuditRequest(Sayfa: 1, SayfaBoyut: 2), default);
            Assert.Single(sayfa1.Kayitlar);
            Assert.Equal("SELECT a", sayfa1.Kayitlar[0].Sql); // en eski, son sayfada
        }
        finally
        {
            await ctx.Database.EnsureDeletedAsync();
        }
    }
}
