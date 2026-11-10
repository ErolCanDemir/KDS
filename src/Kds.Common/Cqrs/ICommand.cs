using MediatR;

namespace Kds.Common.Cqrs;

/// <summary>State-changing request (CQRS command). Has exactly one handler in Business.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;
