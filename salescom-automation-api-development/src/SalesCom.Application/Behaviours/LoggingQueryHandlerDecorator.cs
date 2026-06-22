namespace SalesCom.Application.Behaviours;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SalesCom.Application.Messaging;

internal sealed class LoggingQueryHandlerDecorator<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner,
    ILogger<LoggingQueryHandlerDecorator<TQuery, TResponse>> logger)
    : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken)
    {
        var queryName = typeof(TQuery).Name;
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["QueryName"] = queryName,
            ["QueryType"] = typeof(TQuery).FullName ?? queryName,
        });

        logger.LogDebug("Handling query {QueryName}", queryName);
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await inner.HandleAsync(query, cancellationToken);
            sw.Stop();
            logger.LogDebug("Handled query {QueryName} in {ElapsedMs} ms", queryName, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Query {QueryName} failed after {ElapsedMs} ms", queryName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
