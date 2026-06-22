namespace SalesCom.Application.Messaging;

using SalesCom.Domain.Common;

public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Result>
    where TCommand : ICommand
{ }

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
