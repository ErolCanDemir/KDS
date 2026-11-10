using Kds.Common.Cqrs;

namespace Kds.Contracts.Ingest;

/// <summary>İçe alım işini siler (depo tablolarına dokunmaz). Bulunmayan → false.</summary>
public sealed record DeleteIngestJobRequest(Guid Id) : ICommand<bool>;
