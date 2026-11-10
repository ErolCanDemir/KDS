using Microsoft.Data.SqlClient;
using Xunit;

namespace Kds.Tests;

/// <summary>S13 Part4 — başarı ölçütü #4 İKİNCİ KATMAN: read-only DB kullanıcısı okur ama YAZAMAZ.
/// (Birinci katman = SELECT-only bekçisi, ayrı test edilir.) Gerçek SQL Server'a (LocalDB) karşı:
/// geçici DB'de <c>db_datareader</c> üyesi bir kullanıcı adına (EXECUTE AS) SELECT geçer;
/// INSERT/UPDATE/DELETE/DROP DB tarafından reddedilir (SqlException). infra/deploy/readonly-user.sql
/// bu kullanıcıyı üretim kaynaklarında kurar.</summary>
public sealed class ReadOnlyKullaniciTests
{
    private const string Master =
        @"Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True";

    [Fact]
    public async Task db_datareader_kullanici_okur_ama_yazamaz()
    {
        var dbAdi = "KdsRoTest_" + Guid.NewGuid().ToString("N");
        var db = $@"Server=(localdb)\MSSQLLocalDB;Database={dbAdi};Trusted_Connection=True;TrustServerCertificate=True";

        await Calistir(Master, $"CREATE DATABASE [{dbAdi}]");
        try
        {
            await Calistir(db, """
                CREATE TABLE dbo.Musteri (Id int PRIMARY KEY, Ad nvarchar(50) NOT NULL);
                INSERT INTO dbo.Musteri (Id, Ad) VALUES (1, N'Alfa'), (2, N'Beta');
                CREATE USER [kds_okuyucu] WITHOUT LOGIN;
                ALTER ROLE [db_datareader] ADD MEMBER [kds_okuyucu];
                """);

            // Okuma çalışır.
            var say = await SkalerAs(db, "kds_okuyucu", "SELECT COUNT(*) FROM dbo.Musteri");
            Assert.Equal(2, say);

            // Yazma/DDL reddedilir (DB düzeyi — bekçiden bağımsız).
            await YazmaReddedilir(db, "kds_okuyucu", "INSERT INTO dbo.Musteri (Id, Ad) VALUES (3, N'Gama')");
            await YazmaReddedilir(db, "kds_okuyucu", "UPDATE dbo.Musteri SET Ad = N'X' WHERE Id = 1");
            await YazmaReddedilir(db, "kds_okuyucu", "DELETE FROM dbo.Musteri WHERE Id = 1");
            await YazmaReddedilir(db, "kds_okuyucu", "DROP TABLE dbo.Musteri");

            // Veri bozulmadı.
            Assert.Equal(2, await Skaler(db, "SELECT COUNT(*) FROM dbo.Musteri"));
        }
        finally
        {
            await Calistir(Master,
                $"IF DB_ID('{dbAdi}') IS NOT NULL BEGIN ALTER DATABASE [{dbAdi}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbAdi}]; END");
        }
    }

    private static async Task YazmaReddedilir(string cs, string kullanici, string yazmaSql)
    {
        // db_datareader yazamaz → yazma DB tarafından reddedilir (SqlException). REVERT batch
        // abort'ta çalışmasa da bağlantı kapanınca impersonation biter (her çağrı yeni bağlantı).
        // Yazma DB tarafından reddedilmeli: db_datareader'ın yazma yetkisi yok → SqlException.
        // (Sondaki "veri bozulmadı" doğrulaması yazının GERÇEKTEN uygulanmadığını da kanıtlar.)
        await Assert.ThrowsAsync<SqlException>(() => CalistirAs(cs, kullanici, yazmaSql));
    }

    private static async Task CalistirAs(string cs, string kullanici, string sql)
    {
        await using var b = new SqlConnection(cs);
        await b.OpenAsync();
        await using var k = b.CreateCommand();
        k.CommandText = $"EXECUTE AS USER = '{kullanici}'; {sql}; REVERT;";
        await k.ExecuteNonQueryAsync();
    }

    private static async Task<int> SkalerAs(string cs, string kullanici, string sorgu)
    {
        await using var b = new SqlConnection(cs);
        await b.OpenAsync();
        await using var k = b.CreateCommand();
        k.CommandText = $"EXECUTE AS USER = '{kullanici}'; {sorgu}; REVERT;";
        return (int)(await k.ExecuteScalarAsync() ?? 0);
    }

    private static async Task Calistir(string cs, string sql)
    {
        await using var b = new SqlConnection(cs);
        await b.OpenAsync();
        await using var k = b.CreateCommand();
        k.CommandText = sql;
        await k.ExecuteNonQueryAsync();
    }

    private static async Task<int> Skaler(string cs, string sorgu)
    {
        await using var b = new SqlConnection(cs);
        await b.OpenAsync();
        await using var k = b.CreateCommand();
        k.CommandText = sorgu;
        return (int)(await k.ExecuteScalarAsync() ?? 0);
    }
}
