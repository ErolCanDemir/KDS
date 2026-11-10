using Kds.Execution;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Kds.Tests;

/// <summary>S44 artımlı yükleme — GERÇEK LocalDB entegrasyonu (SqlBulkCopy/MERGE SQLite'ta yok):
/// ilk koşu tam + su seviyesi döner · delta yalnız yeni/değişen satırları taşır (rowversion
/// güncellemeyi de yakalar) · şema kayması ve PK yokluğu tam yüklemeye düşürür · silinen satır
/// artımlıda KALIR, tam yükleme temizler (belgelenen sınırın testi). Her test kendi kaynak+depo
/// DB'sini yaratır ve siler.</summary>
public sealed class ArtimliYuklemeTests
{
    private const string Master =
        @"Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True";

    private static string Dize(string db) =>
        $@"Server=(localdb)\MSSQLLocalDB;Database={db};Trusted_Connection=True;TrustServerCertificate=True";

    private sealed class Ortam : IAsyncDisposable
    {
        public string KaynakDb = $"KdsArtimliKaynak_{Guid.NewGuid():N}";
        public string DepoDb = $"KdsArtimliDepo_{Guid.NewGuid():N}";
        public string Kaynak => Dize(KaynakDb);
        public string Depo => Dize(DepoDb);

        public async ValueTask DisposeAsync()
        {
            await SilAsync(KaynakDb);
            await SilAsync(DepoDb);
        }

        private static async Task SilAsync(string db)
        {
            await using var baglanti = new SqlConnection(Master);
            await baglanti.OpenAsync();
            await using var komut = baglanti.CreateCommand();
            komut.CommandText =
                $"IF DB_ID(N'{db}') IS NOT NULL BEGIN ALTER DATABASE [{db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{db}]; END";
            await komut.ExecuteNonQueryAsync();
        }
    }

    private static async Task<Ortam> KurAsync(string kaynakTabloDdl)
    {
        var o = new Ortam();
        await using (var master = new SqlConnection(Master))
        {
            await master.OpenAsync();
            foreach (var db in new[] { o.KaynakDb, o.DepoDb })
            {
                await using var komut = master.CreateCommand();
                komut.CommandText = $"CREATE DATABASE [{db}]";
                await komut.ExecuteNonQueryAsync();
            }
        }

        await CalistirAsync(o.Kaynak, kaynakTabloDdl);
        return o;
    }

    private static async Task CalistirAsync(string dize, string sql)
    {
        await using var baglanti = new SqlConnection(dize);
        await baglanti.OpenAsync();
        await using var komut = baglanti.CreateCommand();
        komut.CommandText = sql;
        await komut.ExecuteNonQueryAsync();
    }

    private static async Task<object?> TekAsync(string dize, string sql)
    {
        await using var baglanti = new SqlConnection(dize);
        await baglanti.OpenAsync();
        await using var komut = baglanti.CreateCommand();
        komut.CommandText = sql;
        return await komut.ExecuteScalarAsync();
    }

    // ---- rowversion kolonuyla: ekleme + GÜNCELLEME de taşınır ----

    [Fact]
    public async Task rowversion_ile_ilk_kosu_tam_delta_ekleme_ve_guncellemeyi_tasir()
    {
        await using var o = await KurAsync("""
            CREATE TABLE dbo.Musteriler (
                Id int NOT NULL PRIMARY KEY,
                Ad nvarchar(50) NOT NULL,
                Rv rowversion NOT NULL);
            INSERT INTO dbo.Musteriler (Id, Ad) VALUES (1, N'Ali'), (2, N'Ayşe');
            """);
        var kosucu = new SqlIngestRunner();
        var tablolar = new[] { new IngestTableSpec("dbo", "Musteriler", null, "Rv") };

        // 1) İlk koşu: su seviyesi yok → TAM yüklenir ama yeni seviye DÖNER.
        var ilk = await kosucu.RunHedefeAsync(o.Kaynak, o.Depo, tablolar,
            new Dictionary<string, string>(), default);
        Assert.Equal(0, ilk.ArtimliTabloSayisi);                      // tam yüklemeydi
        Assert.Equal(2, ilk.ToplamSatir);
        var seviye = Assert.Contains("dbo.Musteriler", ilk.YeniSuSeviyeleri!);
        Assert.StartsWith("0x", seviye);                              // rowversion hex saklanır

        // 2) Kaynakta 1 ekleme + 1 güncelleme; delta ikisini de taşımalı (rowversion yazımda artar).
        await CalistirAsync(o.Kaynak, """
            INSERT INTO dbo.Musteriler (Id, Ad) VALUES (3, N'Veli');
            UPDATE dbo.Musteriler SET Ad = N'Ayşe Yeni' WHERE Id = 2;
            """);
        var delta = await kosucu.RunHedefeAsync(o.Kaynak, o.Depo, tablolar,
            ilk.YeniSuSeviyeleri!, default);
        Assert.Equal(1, delta.ArtimliTabloSayisi);                    // bu kez ARTIMLI koştu
        Assert.Equal(3, delta.ToplamSatir);
        Assert.Equal("Ayşe Yeni",
            await TekAsync(o.Depo, "SELECT Ad FROM dbo.Musteriler WHERE Id = 2"));  // güncelleme geldi
        Assert.Equal(1, await TekAsync(o.Depo, "SELECT COUNT(*) FROM dbo.Musteriler WHERE Id = 3"));
        Assert.NotEqual(seviye, delta.YeniSuSeviyeleri!["dbo.Musteriler"]);         // seviye ilerledi

        // 3) Değişiklik yokken delta: satır sayısı sabit kalır (sınır satırı MERGE ile tekilleşir).
        var bos = await kosucu.RunHedefeAsync(o.Kaynak, o.Depo, tablolar,
            delta.YeniSuSeviyeleri!, default);
        Assert.Equal(3, bos.ToplamSatir);
        Assert.Equal(1, bos.ArtimliTabloSayisi);
    }

    // ---- Id (int) kolonuyla: yeni satır gelir; silinen KALIR, tam yükleme temizler ----

    [Fact]
    public async Task id_kolonuyla_delta_yeni_satiri_tasir_silinen_tam_yuklemeyle_temizlenir()
    {
        await using var o = await KurAsync("""
            CREATE TABLE dbo.Olaylar (
                Id int NOT NULL PRIMARY KEY,
                Mesaj nvarchar(100) NOT NULL);
            INSERT INTO dbo.Olaylar VALUES (1, N'a'), (2, N'b');
            """);
        var kosucu = new SqlIngestRunner();
        var tablolar = new[] { new IngestTableSpec("dbo", "Olaylar", null, "Id") };

        var ilk = await kosucu.RunHedefeAsync(o.Kaynak, o.Depo, tablolar,
            new Dictionary<string, string>(), default);
        Assert.Equal("2", ilk.YeniSuSeviyeleri!["dbo.Olaylar"]);

        // Kaynakta 1 silme + 1 ekleme: delta yalnız EKLENENİ görür — silinen hedefte kalır
        // (artımlının belgelenen sınırı).
        await CalistirAsync(o.Kaynak, "DELETE FROM dbo.Olaylar WHERE Id = 1; INSERT INTO dbo.Olaylar VALUES (3, N'c');");
        var delta = await kosucu.RunHedefeAsync(o.Kaynak, o.Depo, tablolar, ilk.YeniSuSeviyeleri!, default);
        Assert.Equal(1, delta.ArtimliTabloSayisi);
        Assert.Equal(3, delta.ToplamSatir);   // hedef: 1(silinmiş ama kalan) + 2 + 3
        Assert.Equal(1, await TekAsync(o.Depo, "SELECT COUNT(*) FROM dbo.Olaylar WHERE Id = 1"));

        // Tam yükleme (boş su seviyesi = "Tam Yükle" düğmesinin yaptığı) silineni temizler.
        var tam = await kosucu.RunHedefeAsync(o.Kaynak, o.Depo, tablolar,
            new Dictionary<string, string>(), default);
        Assert.Equal(0, tam.ArtimliTabloSayisi);
        Assert.Equal(2, tam.ToplamSatir);
        Assert.Equal(0, await TekAsync(o.Depo, "SELECT COUNT(*) FROM dbo.Olaylar WHERE Id = 1"));
    }

    // ---- Güvenli düşüşler: şema kayması ve PK yokluğu ----

    [Fact]
    public async Task sema_kaymasi_tam_yuklemeye_duser_ve_yeni_kolon_hedefe_gelir()
    {
        await using var o = await KurAsync("""
            CREATE TABLE dbo.Urunler (Id int NOT NULL PRIMARY KEY, Ad nvarchar(50) NOT NULL);
            INSERT INTO dbo.Urunler VALUES (1, N'x');
            """);
        var kosucu = new SqlIngestRunner();
        var tablolar = new[] { new IngestTableSpec("dbo", "Urunler", null, "Id") };

        var ilk = await kosucu.RunHedefeAsync(o.Kaynak, o.Depo, tablolar,
            new Dictionary<string, string>(), default);

        // Kaynağa kolon eklendi (şema kayması): delta MERGE hedefteki eksik kolona yazamazdı →
        // koşucu bunu koşudan ÖNCE fark edip TAM yüklemeye düşmeli, hedef yeni şemayı almalı.
        // (ALTER ve UPDATE ayrı batch'lerde — aynı batch'te parser yeni kolonu henüz görmez.)
        await CalistirAsync(o.Kaynak, "ALTER TABLE dbo.Urunler ADD Fiyat decimal(10,2) NULL;");
        await CalistirAsync(o.Kaynak, "UPDATE dbo.Urunler SET Fiyat = 9.5;");
        var sonuc = await kosucu.RunHedefeAsync(o.Kaynak, o.Depo, tablolar, ilk.YeniSuSeviyeleri!, default);
        Assert.Equal(0, sonuc.ArtimliTabloSayisi);   // tam yüklemeye düştü (sessiz değil, sayıda görünür)
        Assert.Equal(9.5m, await TekAsync(o.Depo, "SELECT Fiyat FROM dbo.Urunler WHERE Id = 1"));
    }

    [Fact]
    public async Task pk_olmayan_tablo_hep_tam_yuklenir()
    {
        await using var o = await KurAsync("""
            CREATE TABLE dbo.Kayitlar (Sira int NOT NULL, Metin nvarchar(50) NOT NULL);
            INSERT INTO dbo.Kayitlar VALUES (1, N'a');
            """);
        var kosucu = new SqlIngestRunner();
        var tablolar = new[] { new IngestTableSpec("dbo", "Kayitlar", null, "Sira") };

        var ilk = await kosucu.RunHedefeAsync(o.Kaynak, o.Depo, tablolar,
            new Dictionary<string, string>(), default);
        // MERGE anahtarı yok → artımlı hiç başlamaz; ikinci koşu da (seviye dönmüş olsa bile) tam.
        var ikinci = await kosucu.RunHedefeAsync(o.Kaynak, o.Depo, tablolar, ilk.YeniSuSeviyeleri!, default);
        Assert.Equal(0, ikinci.ArtimliTabloSayisi);
        Assert.Equal(1, ikinci.ToplamSatir);
    }
}
