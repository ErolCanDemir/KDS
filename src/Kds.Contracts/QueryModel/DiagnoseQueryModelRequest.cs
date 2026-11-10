using Kds.Common.Cqrs;
using Kds.Contracts.Queries;

namespace Kds.Contracts.QueryModel;

/// <summary>M33 "Sorun Ne?" (tasarımcı yolu): modeli SQL'e çevirir ve haritanın bağlantısında
/// TANILAR (çalıştırmaz) — <see cref="DiagnoseQueryRequest"/>'e devreder.</summary>
public sealed record DiagnoseQueryModelRequest(
    QueryModel Model,
    IReadOnlyDictionary<string, object?>? ParametreDegerleri = null) : ICommand<QueryDiagnosis>;
