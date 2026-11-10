using Kds.Common.Cqrs;

namespace Kds.Contracts.DataMaps;

/// <summary>Bir haritayı tam içeriğiyle getirir (editör için). Yoksa null.</summary>
public sealed record GetDataMapRequest(Guid Id) : IQuery<DataMapDetail?>;
