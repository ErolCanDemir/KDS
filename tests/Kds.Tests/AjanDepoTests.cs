using System.Text.Json;
using Kds.Execution;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Kds.Tests;

/// <summary>S45 — ajanın ittiği verinin depoya yazımı, GERÇEK LocalDB'de: tam push round-trip,
/// artımlı push (staging+MERGE ile güncelleme), hedef kayınca TAM'a düşme. Ayrıca değer
/// kodlamasının (AjanVeri) yaz→JSON→çöz round-trip'i — iki taraf ayrı kodlansaydı değerler
/// sessizce bozulurdu.</summary>
public sealed class AjanDepoTests
{
    private const string Master =
        @"Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True";

    private static string Dize(string db) =>
        $@"Server=(localdb)\MSSQLLocalDB;Database={db};Trusted_Connection=True;TrustServerCertificate=True";

    private static readonly IReadOnlyList<DepoKolon> Kolonlar =
    [
        new DepoKolon("Id", "int", null, null, null, false),
        new DepoKolon("Ad", "nvarchar", 50, null, null, false),
        new DepoKolon("Tutar", "decimal", null, 10, 2, true),
    ];

    [Fact]
    public async Task tam_ve_artimli_push_round_trip_localdb()
    {
        var db = $"KdsAjanDepo_{Guid.NewGuid():N}";
        var yazici = new AjanDepoYazici();
        try
        {
            await yazici.DepoyuHazirlaAsync(Master, db, default);
            var depo = Dize(db);

            // 1) TAM push: hedef yaratılır, satırlar doğrudan yazılır.
            Assert.Equal("Tam", await yazici.TabloBaslaAsync(depo, "dbo", "Satislar", Kolonlar, "Tam", default));
            await yazici.SatirlarYazAsync(depo, "dbo", "Satislar", Kolonlar,
                [[1, "Ali", 10.50m], [2, "Ayşe", null]], default);
            Assert.Equal(2, await yazici.TabloBitirAsync(depo, "dbo", "Satislar", "Tam", Kolonlar, ["Id"], default));

            // 2) ARTIMLI push: hedef uyuşuyor → staging; 1 güncelleme + 1 yeni satır MERGE edilir.
            Assert.Equal("Artimli", await yazici.TabloBaslaAsync(depo, "dbo", "Satislar", Kolonlar, "Artimli", default));
            await yazici.SatirlarYazAsync(depo, "dbo", "Satislar__kds_delta", Kolonlar,
                [[2, "Ayşe Yeni", 7.25m], [3, "Veli", 1m]], default);
            Assert.Equal(3, await yazici.TabloBitirAsync(depo, "dbo", "Satislar", "Artimli", Kolonlar, ["Id"], default));

            await using var baglanti = new SqlConnection(depo);
            await baglanti.OpenAsync();
            await using var komut = baglanti.CreateCommand();
            komut.CommandText = "SELECT Ad FROM dbo.Satislar WHERE Id = 2";
            Assert.Equal("Ayşe Yeni", await komut.ExecuteScalarAsync());   // MERGE güncellemesi geldi

            // 3) Hedef kolonları kayınca artımlı istense de TAM'a düşer (yeni şemayla baştan yaratır).
            var farkli = new List<DepoKolon>(Kolonlar) { new("Yeni", "int", null, null, null, true) };
            Assert.Equal("Tam", await yazici.TabloBaslaAsync(depo, "dbo", "Satislar", farkli, "Artimli", default));
        }
        finally
        {
            await using var master = new SqlConnection(Master);
            await master.OpenAsync();
            await using var sil = master.CreateCommand();
            sil.CommandText =
                $"IF DB_ID(N'{db}') IS NOT NULL BEGIN ALTER DATABASE [{db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{db}]; END";
            await sil.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public void ajan_veri_kodlamasi_yaz_json_coz_round_trip()
    {
        var ayarlar = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        object? Tur(string tip, object? deger)
        {
            var json = JsonSerializer.SerializeToElement(AjanVeri.Yaz(deger), ayarlar);
            return AjanVeri.Coz(tip, json);
        }

        Assert.Equal(123456789012345L, Tur("bigint", 123456789012345L));
        Assert.Equal(12345.6789m, Tur("decimal", 12345.6789m));            // metin taşınır — çift hassasiyet kaybı yok
        Assert.Equal(new DateTime(2026, 9, 1, 13, 45, 30, 123), Tur("datetime2", new DateTime(2026, 9, 1, 13, 45, 30, 123)));
        var dto = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(3));
        Assert.Equal(dto, Tur("datetimeoffset", dto));
        var g = Guid.NewGuid();
        Assert.Equal(g, Tur("uniqueidentifier", g));
        Assert.Equal(new byte[] { 1, 2, 255 }, Tur("varbinary", new byte[] { 1, 2, 255 }));
        Assert.Equal(true, Tur("bit", true));
        Assert.Equal("Ağaç ıİşÖ", Tur("nvarchar", "Ağaç ıİşÖ"));          // Türkçe karakterler bozulmaz
        Assert.Null(Tur("int", null));                                     // NULL her tipte NULL kalır
    }
}
