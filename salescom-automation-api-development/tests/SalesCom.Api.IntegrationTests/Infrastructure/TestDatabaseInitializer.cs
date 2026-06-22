namespace SalesCom.Api.IntegrationTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SalesCom.Infrastructure.Data;

/// <summary>
/// Applies EF migrations against the test container before <see cref="TestDataSeeder"/> runs. The
/// app's own <c>DatabaseInitializer</c> migrates only in the Development environment, and the
/// integration host runs as "Test", so the schema is created here instead. Registered ahead of the
/// seeder so the tables exist before any data is written.
/// </summary>
internal sealed class TestDatabaseInitializer(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SalesComDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
