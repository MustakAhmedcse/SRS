namespace SalesCom.Application.Messaging;

using SalesCom.Domain.Common;

/// <summary>
/// Marker for a command that returns no value other than success/failure.
/// </summary>
public interface ICommand : ICommand<Result> { }

/// <summary>
/// Marker for a command that returns <typeparamref name="TResponse"/>. The contract is intentionally
/// minimal — handlers, dispatcher, and decorators discover commands by closed-generic type.
/// </summary>
public interface ICommand<TResponse> { }
