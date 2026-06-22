namespace SalesCom.Infrastructure.Configurations;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Logging and audit identity settings. Serilog writes to rolling daily files under
/// <see cref="LogDirectory"/> (plus the console in Development) and, when <see cref="LokiEnabled"/>
/// is set, ships structured logs to Grafana Loki at <see cref="LokiUrl"/>. <see cref="ApplicationName"/>
/// tags every log entry and audit row so the SalesCom fleet's components share one queryable schema.
/// </summary>
public sealed record ObservabilityConfiguration
{
    public const string SectionName = "Observability";

    /// <summary>Component identity stamped on log labels and audit rows. Unique per fleet component.</summary>
    [Required, MinLength(1)]
    public string ApplicationName { get; init; } = "SalesCom";

    /// <summary>Used as the log-file name prefix, e.g. <c>salescom-20250101.log</c>.</summary>
    [Required, MinLength(1)]
    public string ServiceName { get; init; } = "SalesCom";

    /// <summary>Directory for rolling log files — relative to the content root, or absolute.</summary>
    [Required, MinLength(1)]
    public string LogDirectory { get; init; } = "logs";

    /// <summary>How many daily log files to keep before the oldest is deleted.</summary>
    [Range(1, 3650)]
    public int RetainedFileCountLimit { get; init; } = 31;

    /// <summary>When true, ships logs to Grafana Loki at <see cref="LokiUrl"/> in addition to the file sink.</summary>
    public bool LokiEnabled { get; init; } = true;

    /// <summary>Grafana Loki push endpoint. The sink is batched and non-blocking — an unreachable Loki never blocks the app.</summary>
    [Required, MinLength(1)]
    public string LokiUrl { get; init; } = "http://localhost:3100";

    /// <summary>
    /// HTTP Basic-Auth username for the Loki push endpoint (the nginx proxy in front of Loki). When
    /// set together with <see cref="LokiPassword"/>, every push is authenticated.
    /// </summary>
    public string? LokiUsername { get; init; }

    /// <summary>HTTP Basic-Auth password for the Loki push endpoint. See <see cref="LokiUsername"/>.</summary>
    public string? LokiPassword { get; init; }
}
