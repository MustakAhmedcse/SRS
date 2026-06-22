namespace SalesCom.Infrastructure.Registrations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using SalesCom.Application.Interfaces;
using SalesCom.Domain.Interfaces;
using SalesCom.Infrastructure.Configurations;
using SalesCom.Infrastructure.Data;
using SalesCom.Infrastructure.Data.Seed;
using SalesCom.Infrastructure.Repositories;
using SalesCom.Infrastructure.Services;

/// <summary>
/// Persistence-layer composition: options binding, DbContext + Npgsql wiring, repositories,
/// the unit-of-work registration, and the seed/migration hosted service. The Npgsql connection
/// string is read from <see cref="DatabaseConfiguration"/> via <see cref="IOptions{TOptions}"/> inside
/// the DbContext factory — no direct <c>IConfiguration</c> reads after the initial bind.
/// </summary>
public static class PersistenceRegistration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseConfiguration>()
            .Bind(configuration.GetSection(DatabaseConfiguration.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<SalesComDbContext>((sp, options) =>
        {
            var db = sp.GetRequiredService<IOptions<DatabaseConfiguration>>().Value;
            // The schema is taken from the connection string's Search Path — single source of truth.
            var schema = new NpgsqlConnectionStringBuilder(db.ConnectionString).SearchPath;

            options.AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>());
            options.UseNpgsql(db.ConnectionString, npg =>
            {
                npg.MigrationsHistoryTable("__ef_migrations_history", schema);
                npg.CommandTimeout(db.CommandTimeoutSeconds);
                npg.EnableRetryOnFailure(
                    maxRetryCount: db.MaxRetryAttempts,
                    maxRetryDelay: TimeSpan.FromSeconds(db.MaxRetryDelaySeconds),
                    errorCodesToAdd: null);
            });

            if (db.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }

            if (db.EnableDetailedErrors)
            {
                options.EnableDetailedErrors();
            }
        });

        // Transient unit of work over the scoped DbContext: one instance per command handler, but it
        // shares the request's change tracker so a single SaveChangesAsync is all-or-none.
        services.AddTransient<IUnitOfWork, UnitOfWork>();

        services.AddScoped<ILoginLogger, LoginLogger>();

        services.AddHostedService<DatabaseInitializer>();

        return services;
    }
}
