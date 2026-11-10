using Kds.Common.Cqrs;

namespace Kds.Business.Connections;

/// <summary>Bağlantıyı siler. Yoksa false döner.</summary>
public sealed record DeleteConnectionRequest(Guid Id) : ICommand<bool>;
