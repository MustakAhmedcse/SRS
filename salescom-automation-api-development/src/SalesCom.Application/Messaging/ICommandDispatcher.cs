namespace SalesCom.Application.Messaging;

using SalesCom.Domain.Common;

public interface ICommandDispatcher
{
    Task<Result> DispatchAsync(ICommand command, CancellationToken cancellationToken);

    Task<TResponse> DispatchAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken);
}
