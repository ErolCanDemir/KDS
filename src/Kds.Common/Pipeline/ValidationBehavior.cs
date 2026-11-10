using FluentValidation;
using MediatR;

namespace Kds.Common.Pipeline;

/// <summary>Runs every request through the registered FluentValidation validators; if any fail,
/// throws <see cref="ValidationException"/> before the handler runs.</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            var failures = results.SelectMany(r => r.Errors).ToList();
            if (failures.Count > 0)
                throw new ValidationException(failures);
        }

        return await next();
    }
}
