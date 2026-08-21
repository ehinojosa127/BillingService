using Billing.Application.Exceptions;
using FluentValidation;
using MediatR;
using ValidationException = Billing.Application.Exceptions.ValidationException;

namespace Billing.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            var errors = results.SelectMany(r => r.Errors).Where(e => e is not null).Select(e => e.ErrorMessage).ToArray();
            if (errors.Length > 0)
            {
                throw new ValidationException(errors);
            }
        }

        return await next(cancellationToken);
    }
}
