namespace SalesCom.Application.Messaging;

using Microsoft.Extensions.DependencyInjection;
using SalesCom.Domain.Common;

/// <summary>
/// Resolves the registered <see cref="ICommandHandler{TCommand,TResponse}"/> for a command's runtime
/// type and invokes it. The handler resolved here is already wrapped by the decorator chain
/// (Logging → Validation → real handler), so the dispatcher itself stays trivial.
/// </summary>
internal sealed class CommandDispatcher(IServiceProvider serviceProvider) : ICommandDispatcher
{
    public Task<Result> DispatchAsync(ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return DispatchAsync<Result>(command, cancellationToken);
    }

    public Task<TResponse> DispatchAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResponse));
        var handler = serviceProvider.GetRequiredService(handlerType);
        var method = handlerType.GetMethod(nameof(ICommandHandler<ICommand<TResponse>, TResponse>.HandleAsync))!;
        return (Task<TResponse>)method.Invoke(handler, [command, cancellationToken])!;
    }
}

