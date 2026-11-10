using MediatR;

namespace Kds.Common.Cqrs;

/// <summary>Non-mutating read request (CQRS query).</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
