using Kds.Common.Cqrs;

namespace Kds.Business.Connections;

/// <summary>Mevcut tenant'ın bağlantı profillerini listeler (global query filter uygular).</summary>
public sealed record ListConnectionsRequest : IQuery<IReadOnlyList<ConnectionSummary>>;
