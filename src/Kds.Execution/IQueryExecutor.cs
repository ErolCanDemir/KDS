namespace Kds.Execution;

/// <summary>Çalıştırma dünyasının tek kapısı: tasarlanan/yazılan sorguları hedef
/// DB'de ya da tenant deposunda koşturur. Gerçekleştirme S5'te (saf ADO.NET +
/// SELECT-only bekçisi + limitler + audit).</summary>
public interface IQueryExecutor
{
    Task<QueryResult> ExecuteAsync(QueryRequest request, CancellationToken cancellationToken);
}
