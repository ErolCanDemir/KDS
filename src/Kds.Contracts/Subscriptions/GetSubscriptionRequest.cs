using Kds.Common.Cqrs;

namespace Kds.Contracts.Subscriptions;

public sealed record GetSubscriptionRequest(Guid Id) : IQuery<SubscriptionDetail?>;
