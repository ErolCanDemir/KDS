using Kds.Common.Cqrs;

namespace Kds.Contracts.Subscriptions;

public sealed record ListSubscriptionsRequest : IQuery<IReadOnlyList<SubscriptionSummary>>;
