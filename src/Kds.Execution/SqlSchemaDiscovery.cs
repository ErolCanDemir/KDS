using Microsoft.Data.SqlClient;

namespace Kds.Execution;

/// <summary>MSSQL şema keşfi (S6): tablo arama + tembel kolon + FK ilişki önerisi. Sabit/güvenli
/// sistem sorguları (parametreli), salt-okuma; bekçiden geçmez. Kds.Data'a referans vermez.</summary>
public sealed class SqlSchemaDiscovery : ISchemaDiscovery
{
    public async Task<IReadOnlyList<SchemaTable>> SearchTablesAsync(
        string baglantiDizesi, string? arama, string? sema, int enCok, CancellationToken cancellationToken)
    {
        const string sorgu = """
            SELECT TOP (@n) s.name AS Sema, t.name AS Ad,
                   (SELECT COUNT(*) FROM sys.columns c WHERE c.object_id = t.object_id) AS KolonSayisi
            FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE (@arama IS NULL OR t.name LIKE '%' + @arama + '%')
              AND (@sema IS NULL OR s.name = @sema)
            ORDER BY t.name;
            """;

        await using var baglanti = new SqlConnection(baglantiDizesi);
        await baglanti.OpenAsync(cancellationToken);
        await using var komut = baglanti.CreateCommand();
        komut.CommandText = sorgu;
        komut.CommandTimeout = 20;
        komut.Parameters.AddWithValue("@n", Math.Clamp(enCok, 1, 200));
        komut.Parameters.AddWithValue("@arama", string.IsNullOrWhiteSpace(arama) ? DBNull.Value : arama);
        komut.Parameters.AddWithValue("@sema", string.IsNullOrWhiteSpace(sema) ? DBNull.Value : sema);

        var liste = new List<SchemaTable>();
        await using var okuyucu = await komut.ExecuteReaderAsync(cancellationToken);
        while (await okuyucu.ReadAsync(cancellationToken))
            liste.Add(new SchemaTable(okuyucu.GetString(0), okuyucu.GetString(1), okuyucu.GetInt32(2)));
        return liste;
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(
        string baglantiDizesi, string sema, string tablo, CancellationToken cancellationToken)
    {
        const string sorgu = """
            SELECT c.name AS Ad, ty.name AS Tip, c.is_nullable AS NullOlabilir,
                   CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS Pk
            FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            JOIN sys.columns c ON c.object_id = t.object_id
            JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            LEFT JOIN (
                SELECT ic.object_id, ic.column_id
                FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                WHERE i.is_primary_key = 1
            ) pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
            WHERE s.name = @sema AND t.name = @tablo
            ORDER BY c.column_id;
            """;

        await using var baglanti = new SqlConnection(baglantiDizesi);
        await baglanti.OpenAsync(cancellationToken);
        await using var komut = baglanti.CreateCommand();
        komut.CommandText = sorgu;
        komut.CommandTimeout = 20;
        komut.Parameters.AddWithValue("@sema", sema);
        komut.Parameters.AddWithValue("@tablo", tablo);

        var liste = new List<ColumnInfo>();
        await using var okuyucu = await komut.ExecuteReaderAsync(cancellationToken);
        while (await okuyucu.ReadAsync(cancellationToken))
            liste.Add(new ColumnInfo(okuyucu.GetString(0), okuyucu.GetString(1), okuyucu.GetBoolean(2), okuyucu.GetInt32(3) == 1));
        return liste;
    }

    public async Task<IReadOnlyList<ForeignKey>> GetRelationshipsAsync(
        string baglantiDizesi, string sema, string tablo, CancellationToken cancellationToken)
    {
        const string sorgu = """
            SELECT sp.name AS KaynakSema, tp.name AS KaynakTablo, cp.name AS KaynakKolon,
                   sr.name AS HedefSema, tr.name AS HedefTablo, cr.name AS HedefKolon
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.tables tp ON tp.object_id = fk.parent_object_id
            JOIN sys.schemas sp ON sp.schema_id = tp.schema_id
            JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
            JOIN sys.tables tr ON tr.object_id = fk.referenced_object_id
            JOIN sys.schemas sr ON sr.schema_id = tr.schema_id
            JOIN sys.columns cr ON cr.object_id = fkc.referenced_object_id AND cr.column_id = fkc.referenced_column_id
            WHERE (sp.name = @sema AND tp.name = @tablo) OR (sr.name = @sema AND tr.name = @tablo);
            """;

        await using var baglanti = new SqlConnection(baglantiDizesi);
        await baglanti.OpenAsync(cancellationToken);
        await using var komut = baglanti.CreateCommand();
        komut.CommandText = sorgu;
        komut.CommandTimeout = 20;
        komut.Parameters.AddWithValue("@sema", sema);
        komut.Parameters.AddWithValue("@tablo", tablo);

        var liste = new List<ForeignKey>();
        await using var okuyucu = await komut.ExecuteReaderAsync(cancellationToken);
        while (await okuyucu.ReadAsync(cancellationToken))
            liste.Add(new ForeignKey(
                okuyucu.GetString(0), okuyucu.GetString(1), okuyucu.GetString(2),
                okuyucu.GetString(3), okuyucu.GetString(4), okuyucu.GetString(5)));
        return liste;
    }
}
