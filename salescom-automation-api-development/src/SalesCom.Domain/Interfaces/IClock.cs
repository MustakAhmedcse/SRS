namespace SalesCom.Domain.Interfaces;

/// <summary>
/// Abstraction over the system clock so domain and application logic remain deterministic
/// under test. Implementations should return UTC.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
