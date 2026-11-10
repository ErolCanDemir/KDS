using Kds.Common.Cqrs;

namespace Kds.Contracts.DataMaps;

/// <summary>Mevcut tenant'ın veri haritalarını listeler.</summary>
public sealed record ListDataMapsRequest : IQuery<IReadOnlyList<DataMapSummary>>;
