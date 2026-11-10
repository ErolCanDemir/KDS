using Kds.Common.Cqrs;

namespace Kds.Contracts.Users;

/// <summary>Mevcut tenant'ın kullanıcılarını listeler (global query filter otomatik uygular).</summary>
public sealed record ListUsersRequest : IQuery<IReadOnlyList<UserSummary>>;
