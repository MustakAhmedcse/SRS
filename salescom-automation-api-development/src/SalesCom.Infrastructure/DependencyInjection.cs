namespace SalesCom.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SalesCom.Domain.Interfaces;
using SalesCom.Infrastructure.Configurations;
using SalesCom.Infrastructure.Registrations;
using SalesCom.Infrastructure.Services;

/// <summary>
/// Infrastructure composition root. Delegates to focused, single-responsibility registration
/// methods — persistence, authentication (Central Login + JWT), time — so each concern can be
/// tested, reasoned about, and replaced independently.
/// <para>
/// Observability: <see cref="ObservabilityConfiguration"/> is bound here and consumed by the
/// Serilog UseSerilog callback at host build — rolling daily file + Grafana Loki sinks, plus a
/// console sink in Development. Per-request correlation is via the <c>CorrelationId</c> stamped
/// on every log line.
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ObservabilityConfiguration>()
            .Bind(configuration.GetSection(ObservabilityConfiguration.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IClock, SystemClock>();

        services
            .AddPersistence(configuration)
            .AddAuthenticationServices(configuration);

        return services;
    }
}
