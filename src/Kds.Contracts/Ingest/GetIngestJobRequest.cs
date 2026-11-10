using Kds.Common.Cqrs;

namespace Kds.Contracts.Ingest;

/// <summary>İçe alım işini tablolarıyla getirir; yok → null (404).</summary>
public sealed record GetIngestJobRequest(Guid Id) : IQuery<IngestJobDetail?>;
