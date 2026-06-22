namespace SalesCom.Infrastructure.Data.Seed;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SalesCom.Infrastructure.Data;

/// <summary>
/// Hosted service that applies pending EF migrations on startup — but only in the Development
/// environment. Everywhere else (Production included) migrations are applied out of band (e.g.
/// <c>dotnet ef database update</c> in the deployment pipeline) and this service is a no-op, so the
/// running app never silently mutates a production schema. There is no data seeding: users are
/// provisioned from Central Login on first sign-in, and their rights are granted directly in the
/// database (there is no rights-management API).
/// </summary>
internal sealed class DatabaseInitializer(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            logger.LogInformation(
                "Skipping automatic migration in the {Environment} environment; migrations are applied out of band outside Development.",
                environment.EnvironmentName);
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SalesComDbContext>();

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying {Count} pending EF migration(s): {Names}",
                pending.Count, string.Join(", ", pending));
            await context.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Migrations applied.");
        }
        else
        {
            logger.LogInformation("Database schema is up to date.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
