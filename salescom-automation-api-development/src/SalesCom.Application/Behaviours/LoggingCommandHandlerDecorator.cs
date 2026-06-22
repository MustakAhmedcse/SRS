namespace SalesCom.Application.Behaviours;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SalesCom.Application.Messaging;

/// <summary>
/// Outermost decorator. Logs entry, exit, duration, and exception path for every command. Uses
/// scoped logging so structured properties (command name, correlation id) attach to every log
/// produced inside the handler.
/// </summary>
internal sealed class LoggingCommandHandlerDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    ILogger<LoggingCommandHandlerDecorator<TCommand, TResponse>> logger)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var commandName = typeof(TCommand).Name;
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CommandName"] = commandName,
            ["CommandType"] = typeof(TCommand).FullName ?? commandName,
        });

        logger.LogInformation("Handling command {CommandName}", commandName);
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await inner.HandleAsync(command, cancellationToken);
            sw.Stop();
            logger.LogInformation("Handled command {CommandName} in {ElapsedMs} ms", commandName, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Command {CommandName} failed after {ElapsedMs} ms", commandName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
