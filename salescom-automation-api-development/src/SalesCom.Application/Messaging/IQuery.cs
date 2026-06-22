namespace SalesCom.Application.Messaging;

/// <summary>
/// Marker for a query that returns <typeparamref name="TResponse"/>. Queries must not mutate state.
/// </summary>
public interface IQuery<TResponse> { }
