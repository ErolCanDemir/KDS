using System.Diagnostics;
using Kds.Execution;
using Microsoft.Data.SqlClient;
using Xunit;
using Xunit.Abstractions;

namespace Kds.Tests;

/// <summary>S13 Part3 — başarı ölçütü #2: canlı modda BÜYÜK kaynakta (5M satır) tasarımcı sorgusunun
/// ilk sayfası &lt; 3 sn; hiçbir sorgu satır sınırı olmadan koşamaz. Gerçek SQL Server'a (LocalDB) karşı:
/// geçici DB'ye 5M satır + nonclustered columnstore tohumlar, <see cref="SqlQueryExecutor"/> ile ölçer,
/// DB'yi düşürür. (Yavaş tohum → tek ağır test; ölçüt kanıtı bu.)</summary>
public sealed class PerformansTests(ITestOutputHelper cikti)
{
    private const string Master =
        @"Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True";

    [PerfFact]
    public async Task besmilyon_satirda_ilk_sayfa_ve_agregasyon_ucsn_alti_ve_sinir_uygulanir()
    {
        var dbAdi = "KdsPerfTest_" + Guid.NewGuid().ToString("N");
        var db = $@"Server=(localdb)\MSSQLLocalDB;Database={dbAdi};Trusted_Connection=True;TrustServerCertificate=True";

        await Calistir(Master, $"CREATE DATABASE [{dbAdi}]");
        try
        {
            var tohum = Stopwatch.StartNew();
            await Calistir(db, SeedSql, timeoutSn: 0);
            tohum.Stop();
            var say = await Skaler(db, "SELECT COUNT_BIG(*) FROM dbo.PerfSatis");
            cikti.WriteLine($"Tohum {say:N0} satır + columnstore: {tohum.Elapsed.TotalSeconds:F1}s");
            Assert.Equal(5_000_000, say);

            var exec = new SqlQueryExecutor(new ScriptDomQueryGuard());
            var bos = new Dictionary<string, object?>();
            var esik = TimeSpan.FromSeconds(30);

            // 1) İlk sayfa (detay tasarımcı sorgusu): filtre + sıralı + ilk 1000.
            var sw = Stopwatch.StartNew();
            var r1 = await exec.ExecuteAsync(new QueryRequest(db,
                "SELECT TOP (1000) Id, Bolge, Tarih, Tutar FROM dbo.PerfSatis WHERE Tutar > 5000 ORDER BY Id",
                bos, 1000, esik), default);
            sw.Stop();
            cikti.WriteLine($"İlk sayfa: {sw.Elapsed.TotalMilliseconds:F0} ms ({r1.Satirlar.Count} satır)");
            Assert.Equal(1000, r1.Satirlar.Count);
            Assert.True(sw.Elapsed.TotalSeconds < 3, $"ilk sayfa {sw.Elapsed.TotalSeconds:F2}s > 3s");

            // 2) SINIR: TOP'suz "SELECT *" bile varsayılan sınırla kesilir — 5M satır belleğe akmaz.
            var r2 = await exec.ExecuteAsync(new QueryRequest(db,
                "SELECT * FROM dbo.PerfSatis", bos, 1000, esik), default);
            Assert.Equal(1000, r2.Satirlar.Count);
            Assert.True(r2.SatirSiniriUygulandi, "TOP'suz sorguda satır sınırı uygulanmalı (runaway koruması)");

            // 3) Agregasyon (columnstore): 5M satır üstünde GROUP BY < 3 sn.
            sw.Restart();
            var r3 = await exec.ExecuteAsync(new QueryRequest(db,
                "SELECT Bolge, SUM(Tutar) Toplam, COUNT_BIG(*) Adet FROM dbo.PerfSatis GROUP BY Bolge",
                bos, 1000, esik), default);
            sw.Stop();
            cikti.WriteLine($"Agregasyon (GROUP BY): {sw.Elapsed.TotalMilliseconds:F0} ms ({r3.Satirlar.Count} grup)");
            Assert.True(r3.Satirlar.Count >= 1);
            Assert.True(sw.Elapsed.TotalSeconds < 3, $"agregasyon {sw.Elapsed.TotalSeconds:F2}s > 3s");
        }
        finally
        {
            await Calistir(Master,
                $"IF DB_ID('{dbAdi}') IS NOT NULL BEGIN ALTER DATABASE [{dbAdi}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbAdi}]; END");
        }
    }

    private static async Task Calistir(string cs, string sql, int timeoutSn = 60)
    {
        await using var b = new SqlConnection(cs);
        await b.OpenAsync();
        await using var k = b.CreateCommand();
        k.CommandText = sql;
        k.CommandTimeout = timeoutSn;
        await k.ExecuteNonQueryAsync();
    }

    private static async Task<long> Skaler(string cs, string sql)
    {
        await using var b = new SqlConnection(cs);
        await b.OpenAsync();
        await using var k = b.CreateCommand();
        k.CommandText = sql;
        return (long)(await k.ExecuteScalarAsync() ?? 0L);
    }

    // 7×10 çapraz birleşim = 10^7 aday → TOP 5M; her satır rasgele (CHECKSUM(NEWID())).
    private const string SeedSql = """
        CREATE TABLE dbo.PerfSatis (
            Id int NOT NULL,
            Bolge nvarchar(20) NOT NULL,
            Tarih date NOT NULL,
            Tutar decimal(12,2) NOT NULL,
            Adet int NOT NULL);
        WITH N AS (SELECT v FROM (VALUES (0),(0),(0),(0),(0),(0),(0),(0),(0),(0)) t(v))
        INSERT INTO dbo.PerfSatis WITH (TABLOCK) (Id, Bolge, Tarih, Tutar, Adet)
        SELECT TOP (5000000)
            ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
            N'Bolge-' + CAST(ABS(CHECKSUM(NEWID())) % 8 + 1 AS nvarchar(2)),
            DATEADD(DAY, ABS(CHECKSUM(NEWID())) % 1000, CAST('2023-01-01' AS date)),
            CAST(ABS(CHECKSUM(NEWID())) % 1000000 / 100.0 AS decimal(12,2)),
            ABS(CHECKSUM(NEWID())) % 50 + 1
        FROM N a, N b, N c, N d, N e, N f, N g;
        ALTER TABLE dbo.PerfSatis ADD CONSTRAINT PK_PerfSatis PRIMARY KEY CLUSTERED (Id);
        CREATE NONCLUSTERED COLUMNSTORE INDEX CCI_PerfSatis ON dbo.PerfSatis (Bolge, Tarih, Tutar, Adet);
        """;
}
