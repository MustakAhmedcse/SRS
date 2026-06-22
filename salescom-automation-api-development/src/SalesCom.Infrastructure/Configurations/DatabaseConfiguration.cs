namespace SalesCom.Infrastructure.Configurations;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Bound from configuration section <see cref="SectionName"/>. Validated on startup so a missing
/// connection string fails fast at boot rather than at first request.
/// </summary>
public sealed record DatabaseConfiguration
{
    public const string SectionName = "Database";

    [Required, MinLength(10)]
    public string ConnectionString { get; init; } = string.Empty;

    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; init; } = 30;

    public bool EnableSensitiveDataLogging { get; init; }

    public bool EnableDetailedErrors { get; init; }

    [Range(1, 10)]
    public int MaxRetryAttempts { get; init; } = 3;

    [Range(1, 60)]
    public int MaxRetryDelaySeconds { get; init; } = 10;
}
