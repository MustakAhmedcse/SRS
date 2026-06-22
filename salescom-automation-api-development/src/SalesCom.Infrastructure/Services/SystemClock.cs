namespace SalesCom.Infrastructure.Services;

using SalesCom.Domain.Interfaces;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
