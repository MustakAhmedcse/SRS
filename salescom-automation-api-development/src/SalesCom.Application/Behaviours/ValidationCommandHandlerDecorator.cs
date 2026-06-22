namespace SalesCom.Application.Behaviours;

using System.Linq;
using FluentValidation;
using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;

/// <summary>
/// Runs every registered <see cref="IValidator{T}"/> for the command. On failure, returns a
/// <see cref="Result"/> carrying the first violation rather than throwing — keeps the failure mode
/// in the explicit Result channel.
/// </summary>
internal sealed class ValidationCommandHandlerDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IEnumerable<IValidator<TCommand>> validators)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TCommand>(command);
            var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken))))
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToList();

            if (failures.Count > 0)
            {
                var first = failures[0];
                var error = ErrorBase.Validation(
                    code: $"Validation.{first.PropertyName}",
                    message: first.ErrorMessage);

                // The dispatcher's TResponse is either Result or Result<T>; bridge via implicit conversion.
                if (typeof(TResponse) == typeof(Result))
                {
                    return (TResponse)(object)Result.Failure(error);
                }

                if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
                {
                    var valueType = typeof(TResponse).GetGenericArguments()[0];
                    var failure = typeof(Result)
                        .GetMethod(nameof(Result.Failure), 1, [typeof(ErrorBase)])!
                        .MakeGenericMethod(valueType)
                        .Invoke(null, [error])!;
                    return (TResponse)failure;
                }

                throw new ValidationException(failures);
            }
        }

        return await inner.HandleAsync(command, cancellationToken);
    }
}
