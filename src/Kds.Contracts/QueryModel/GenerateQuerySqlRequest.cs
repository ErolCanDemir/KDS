using Kds.Common.Cqrs;

namespace Kds.Contracts.QueryModel;

/// <summary>Bir sorgu modelini (existing tenant'ın haritasına göre) doğrular ve parametreli SQL
/// üretir — çalıştırmadan ("SQL'i Göster"). Tasarımcı UI (S8) bunu çağırır. <see cref="ParametreDegerleri"/>
/// verilirse önizleme o değerlerle (yoksa parametre varsayılanlarıyla) üretilir.</summary>
public sealed record GenerateQuerySqlRequest(
    QueryModel Model,
    IReadOnlyDictionary<string, object?>? ParametreDegerleri = null) : IQuery<GeneratedQuery>;
