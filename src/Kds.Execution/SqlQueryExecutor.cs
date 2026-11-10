using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace Kds.Execution;

/// <summary>Çalıştırma dünyasının tek kapısı (00-analiz §6–§7): her icra önce SELECT-only
/// bekçisinden geçer; sonra saf ADO.NET ile parametreli, zaman-aşımlı, satır-sınırlı ve
/// cancellationToken edilebilir biçimde koşar. Kds.Data'a referans vermez.</summary>
public sealed class SqlQueryExecutor(IQueryGuard bekci) : IQueryExecutor
{
    public async Task<QueryResult> ExecuteAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        // TEK KAPI: bekçiyi geçemeyen SQL hiç çalışmaz (başarı ölçütü #4).
        var karar = bekci.Inspect(request.Sql);
        if (!karar.Gecerli)
            throw new QuerySecurityException(karar.Sebep!);

        var kron = Stopwatch.StartNew();
        await using var baglanti = new SqlConnection(request.BaglantiDizesi);
        await baglanti.OpenAsync(cancellationToken);

        await using var komut = baglanti.CreateCommand();
        komut.CommandText = request.Sql;
        komut.CommandTimeout = Math.Max(1, (int)Math.Ceiling(request.ZamanAsimi.TotalSeconds));
        foreach (var (ad, deger) in request.Parametreler)
            komut.Parameters.AddWithValue(ad, deger ?? DBNull.Value);

        await using var okuyucu = await komut.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken);

        var kolonlar = new List<QueryColumn>(okuyucu.FieldCount);
        for (var i = 0; i < okuyucu.FieldCount; i++)
            kolonlar.Add(new QueryColumn(okuyucu.GetName(i), okuyucu.GetDataTypeName(i)));

        var satirlar = new List<object?[]>();
        var sinirUygulandi = false;
        while (await okuyucu.ReadAsync(cancellationToken))
        {
            if (satirlar.Count >= request.SatirSiniri)
            {
                sinirUygulandi = true; // sınırdan fazlası var; fazlasını okumadan dur
                break;
            }
            var satir = new object?[okuyucu.FieldCount];
            okuyucu.GetValues(satir!);
            for (var i = 0; i < satir.Length; i++)
                if (satir[i] is DBNull) satir[i] = null;
            satirlar.Add(satir);
        }

        kron.Stop();
        return new QueryResult(kolonlar, satirlar, sinirUygulandi, kron.Elapsed);
    }
}
