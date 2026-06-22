namespace SalesCom.Application.Messaging;

using Microsoft.Extensions.DependencyInjection;

internal sealed class QueryDispatcher(IServiceProvider serviceProvider) : IQueryDispatcher
{
    public Task<TResponse> DispatchAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResponse));
        var handler = serviceProvider.GetRequiredService(handlerType);
        var method = handlerType.GetMethod(nameof(IQueryHandler<IQuery<TResponse>, TResponse>.HandleAsync))!;
        return (Task<TResponse>)method.Invoke(handler, [query, cancellationToken])!;
    }
}
