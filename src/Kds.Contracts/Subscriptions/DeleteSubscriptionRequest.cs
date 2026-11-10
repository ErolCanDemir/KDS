using Kds.Common.Cqrs;

namespace Kds.Contracts.Subscriptions;

public sealed record DeleteSubscriptionRequest(Guid Id) : ICommand<bool>;
