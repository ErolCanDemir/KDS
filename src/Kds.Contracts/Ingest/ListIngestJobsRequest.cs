using Kds.Common.Cqrs;

namespace Kds.Contracts.Ingest;

/// <summary>Tenant'ın içe alım işlerini listeler (son senkron durumuyla).</summary>
public sealed record ListIngestJobsRequest : IQuery<IReadOnlyList<IngestJobSummary>>;
