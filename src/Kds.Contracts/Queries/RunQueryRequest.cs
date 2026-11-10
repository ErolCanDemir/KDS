using Kds.Common.Cqrs;

namespace Kds.Contracts.Queries;

/// <summary>Bir bağlantı üzerinde bir SELECT yürütür: SELECT-only bekçisinden geçer, sonucu
/// döndürür ve denetim kaydı bırakır. Ham SQL ekranı (S9) bunu doğrudan; sorgu tasarımcısı (S8)
/// modeli SQL'e çevirdikten sonra <see cref="Parametreler"/> ile çağırır (üretilen @p'ler bağlanır).</summary>
public sealed record RunQueryRequest(
    Guid BaglantiId,
    string Sql,
    int? SatirSiniri = null,
    IReadOnlyDictionary<string, object?>? Parametreler = null,
    // M26: koşum kayıtlı bir RAPOR üzerinden geldiyse kimliği — denetim kaydına yazılır.
    Guid? RaporId = null)
    : ICommand<QueryRunResult>;
