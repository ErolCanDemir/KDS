using Kds.Execution;
using Xunit;

namespace Kds.Tests;

/// <summary>SELECT-only bekçisi — başarı ölçütü #4'ün kanıtı: SELECT dışı hiçbir şey geçmez
/// (AST tabanlı; string kontrolü değil). Kabul edilen ve reddedilen geniş bir küme.</summary>
public sealed class QueryGuardTests
{
    private readonly IQueryGuard bekci = new ScriptDomQueryGuard();

    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("SELECT * FROM Musteriler")]
    [InlineData("SELECT Ad FROM dbo.Kisi WHERE Id = @id")]
    [InlineData("SELECT COUNT(*) FROM Siparis GROUP BY MusteriId HAVING COUNT(*) > 5")]
    [InlineData("WITH c AS (SELECT Id FROM T) SELECT * FROM c")]
    [InlineData("SELECT * FROM A JOIN B ON A.Id = B.AId ORDER BY A.Id")]
    [InlineData("   select 1   ")]
    [InlineData("SELECT 1; ")]
    public void gecerli_tek_select_gecer(string sql) =>
        Assert.True(bekci.Inspect(sql).Gecerli, $"beklenen GEÇER: {sql}");

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("INSERT INTO T (A) VALUES (1)")]
    [InlineData("UPDATE T SET A = 1")]
    [InlineData("DELETE FROM T")]
    [InlineData("DROP TABLE T")]
    [InlineData("TRUNCATE TABLE T")]
    [InlineData("CREATE TABLE T (A INT)")]
    [InlineData("ALTER TABLE T ADD B INT")]
    [InlineData("GRANT SELECT ON T TO usr")]
    [InlineData("EXEC sp_who")]
    [InlineData("EXECUTE ('SELECT 1')")]
    [InlineData("MERGE T USING S ON T.Id = S.Id WHEN MATCHED THEN UPDATE SET T.A = S.A;")]
    [InlineData("SELECT 1; DROP TABLE T")]      // enjeksiyon: ikinci ifade
    [InlineData("SELECT 1; SELECT 2")]          // çoklu SELECT
    [InlineData("DECLARE @x INT; SELECT @x")]   // çoklu ifade
    [InlineData("SELECT * INTO Yeni FROM T")]   // SELECT INTO — tablo oluşturur
    [InlineData("SELECT * FROM OPENROWSET('SQLNCLI', 'Server=x;Trusted_Connection=yes;', 'SELECT 1')")]
    [InlineData("SELECT * FROM OPENQUERY(srv, 'SELECT 1')")]
    [InlineData("SELECT * FROM sys.fn_get_audit_file('\\\\srv\\p\\*', DEFAULT, DEFAULT)")]  // TVF: dosya/UNC okuma
    [InlineData("SELECT a.* FROM ::fn_trace_gettable('c:\\x.trc', -1) AS a")]               // yerleşik TVF
    [InlineData("SELECT * FROM (DELETE dbo.T OUTPUT deleted.*) AS x")]                       // iç içe DML
    [InlineData("SELECT * FROM")]               // ayrıştırma hatası
    public void select_disi_reddedilir(string sql)
    {
        var s = bekci.Inspect(sql);
        Assert.False(s.Gecerli, $"beklenen RED: {sql}");
        Assert.False(string.IsNullOrEmpty(s.Sebep));
    }
}
