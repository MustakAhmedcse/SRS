namespace SalesCom.Application.Messaging;

public interface IQueryDispatcher
{
    Task<TResponse> DispatchAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken);
}
