using Kds.Common.Cqrs;

namespace Kds.Contracts.DataMaps;

/// <summary>Haritayı siler (alt varlıklar cascade). Yoksa false.</summary>
public sealed record DeleteDataMapRequest(Guid Id) : ICommand<bool>;
