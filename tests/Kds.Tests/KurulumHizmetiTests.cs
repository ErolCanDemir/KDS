using Kds.Api.Kurulum;
using Kds.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S16/A4 — SIFIRDAN KURULUM senaryosu, gerçek SQL Server'a (LocalDB) karşı: var olmayan
/// bir veritabanı adı verilir → test "yok ama oluşturulabilir" der → tamamla veritabanını yaratır,
/// şemayı kurar, ilk kiracıyı ekler ve kurulum durumunu yapılandırılmış hâle getirir. Kendi geçici
/// DB'sini yaratır ve siler; kurulum dosyasını da geçici bir veri dizinine yazar (makineye bulaşmaz).</summary>
[Collection(VeriDiziniKoleksiyonu.Ad)]   // S40: ortam değişkeni yarışı — gerekçe koleksiyon tanımında
public sealed class KurulumHizmetiTests : IDisposable
{
    private readonly string veriDizini = Path.Combine(
        Path.GetTempPath(), "kds-kurulum-test-" + Guid.NewGuid().ToString("N"));
    private readonly string? eskiDizin = Environment.GetEnvironmentVariable(VeriDizini.OrtamDegiskeni);

    public KurulumHizmetiTests() =>
        Environment.SetEnvironmentVariable(VeriDizini.OrtamDegiskeni, veriDizini);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(VeriDizini.OrtamDegiskeni, eskiDizin);
        if (Directory.Exists(veriDizini)) Directory.Delete(veriDizini, recursive: true);
    }

    private sealed class BosIstemciFabrikasi : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    [Fact]
    public async Task sifirdan_kurulum_veritabanini_yaratir_semayi_kurar_kiraciyi_ekler()
    {
        var dbAdi = "KdsKurulumIT_" + Guid.NewGuid().ToString("N")[..12];
        var depo = new KurulumDeposu();
        var durum = new KurulumDurumu(depo, new KurulumAyarlari(), "yok (kurulum modu)");
        var hizmet = new KurulumHizmeti(durum, new BosIstemciFabrikasi());
        var istek = new VeritabaniIstek(@"(localdb)\MSSQLLocalDB", dbAdi, null, null);

        Assert.False(durum.Yapilandirildi);

        // 1) Veritabanı yok → "oluşturulacak" denmeli (sihirbazın 2. adımı).
        var test = await hizmet.VeritabaniTestAsync(istek, default);
        Assert.True(test.Basarili);
        Assert.False(test.VeritabaniVar);
        Assert.True(test.OlusturabilirMi);

        try
        {
            // 2) Tamamla: DB yarat + şema + ilk kiracı + yapılandırmayı şifreli yaz.
            var sonuc = await hizmet.TamamlaAsync(new TamamlaIstek(
                istek, Kimlik: null, KiraciAdi: "Entegrasyon A.Ş.", KiraciId: null,
                OnyuzAdres: "http://localhost:5173", DepoSunucusu: null, LogBaglantisi: null), default);

            Assert.True(sonuc.Basarili);
            Assert.NotEqual(Guid.Empty, sonuc.KiraciId);

            // 3) Uygulama artık kurulmuş sayılır ve bağlantıyı verir (restart gerekmeden).
            Assert.True(durum.Yapilandirildi);
            Assert.Contains(dbAdi, durum.Dize(), StringComparison.Ordinal);

            // 4) Şema gerçekten kuruldu ve kiracı yazıldı.
            var opts = new DbContextOptionsBuilder<KdsDbContext>().UseSqlServer(durum.Dize()).Options;
            await using var ctx = new KdsDbContext(opts, new FixedTenantContext(sonuc.KiraciId));
            Assert.True(await ctx.Baglantilar.AnyAsync() || true); // tablo var (sorgu patlamıyor)
            var kiraci = await ctx.Tenantlar.IgnoreQueryFilters()
                .SingleOrDefaultAsync(t => t.Id == sonuc.KiraciId);
            Assert.Equal("Entegrasyon A.Ş.", kiraci?.Ad);

            // 5) Yapılandırma diske ŞİFRELİ yazıldı (düz metin bağlantı dizesi görünmez).
            var dosya = Path.Combine(veriDizini, "kurulum.json");
            Assert.True(File.Exists(dosya));
            Assert.DoesNotContain(dbAdi, await File.ReadAllTextAsync(dosya), StringComparison.Ordinal);

            // 6) Aynı dosyadan yeniden okuma aynı bağlantıyı verir (yeniden başlatma senaryosu).
            var (yeniden, kaynak) = new KurulumDeposu().Oku(
                new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
            Assert.Equal("kurulum dosyası", kaynak);
            Assert.Contains(dbAdi, yeniden.MetadataBaglantisi, StringComparison.Ordinal);
        }
        finally
        {
            await VeritabaniniSilAsync(dbAdi);
        }
    }

    private static async Task VeritabaniniSilAsync(string dbAdi)
    {
        await using var baglanti = new SqlConnection(
            @"Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True");
        await baglanti.OpenAsync();
        await using var komut = baglanti.CreateCommand();
        komut.CommandText =
            $"IF DB_ID(N'{dbAdi}') IS NOT NULL BEGIN ALTER DATABASE [{dbAdi}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{dbAdi}]; END";
        await komut.ExecuteNonQueryAsync();
    }
}
