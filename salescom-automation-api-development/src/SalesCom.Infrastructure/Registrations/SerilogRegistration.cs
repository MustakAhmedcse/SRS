namespace SalesCom.Infrastructure.Registrations;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Exceptions;
using Serilog.Sinks.Grafana.Loki;
using SalesCom.Infrastructure.Configurations;

/// <summary>
/// Wires Serilog as the logging provider. Logs always go to a rolling daily file under
/// <see cref="ObservabilityConfiguration.LogDirectory"/>; in Development the console is added, and
/// when <see cref="ObservabilityConfiguration.LokiEnabled"/> is set they are also shipped to Grafana
/// Loki for fleet-wide querying. Every entry carries the request <c>CorrelationId</c> (pushed by
/// <c>CorrelationIdMiddleware</c>) plus machine / process / thread context, so a single request can
/// be followed end-to-end across components by its correlation ID.
/// </summary>
public static class SerilogRegistration
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{CorrelationId}] {SourceContext} {Message:lj}{NewLine}{Exception}";

    public static IHostBuilder UseSalesComSerilog(this IHostBuilder builder) =>
        builder.UseSerilog(Configure);

    public static WebApplicationBuilder UseSalesComSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog(Configure);
        return builder;
    }

    private static void Configure(
        HostBuilderContext context,
        IServiceProvider services,
        LoggerConfiguration loggerConfiguration)
    {
        var options = services.GetRequiredService<IOptions<ObservabilityConfiguration>>().Value;
        var logFile = Path.Combine(options.LogDirectory, $"{options.ServiceName.ToLowerInvariant()}-.log");

        // Per-namespace minimum levels come from the "Serilog" section in appsettings.
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithExceptionDetails()
            .WriteTo.File(
                path: logFile,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: options.RetainedFileCountLimit,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 50L * 1024 * 1024,
                shared: true,
                outputTemplate: OutputTemplate);

        // Development also writes to the console for an at-a-glance view while running locally.
        if (context.HostingEnvironment.IsDevelopment())
        {
            loggerConfiguration.WriteTo.Console(outputTemplate: OutputTemplate);
        }

        // Ship structured logs to Grafana Loki for fleet-wide querying. The sink batches and posts
        // out-of-band, so an unreachable Loki never blocks or crashes the app — delivery failures
        // surface only in Serilog's SelfLog. Labels stay low-cardinality (the sink also adds a
        // `level` label automatically); everything else — CorrelationId, SourceContext, UserId, and
        // the full Exception (type + message + stack trace) — is emitted as structured JSON via
        // LokiJsonTextFormatter, so an error line tells you the exact class, method and failure.
        if (options.LokiEnabled)
        {
            var labels = new[]
            {
                new LokiLabel { Key = "app", Value = options.ApplicationName },
                new LokiLabel { Key = "environment", Value = context.HostingEnvironment.EnvironmentName },
            };

            LokiCredentials? credentials = string.IsNullOrWhiteSpace(options.LokiUsername)
                ? null
                : new LokiCredentials { Login = options.LokiUsername, Password = options.LokiPassword ?? string.Empty };

            loggerConfiguration.WriteTo.GrafanaLoki(
                uri: options.LokiUrl,
                labels: labels,
                credentials: credentials,
                textFormatter: new LokiJsonTextFormatter(),
                queueLimit: 10_000);
        }
    }
}
