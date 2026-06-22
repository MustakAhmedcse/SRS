namespace SalesCom.Api.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SalesCom.Application.Interfaces;
using SalesCom.Infrastructure.Data;
using Testcontainers.PostgreSql;

/// <summary>
/// Bootstraps the full host against a real Postgres container. Each fixture instance has its own
/// database; Serilog writes to rolling files as in any other environment.
/// </summary>
public sealed class SalesComFactory : WebApplicationFactory<SalesCom.Api.Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("salescom_test")
        .WithUsername("salescom")
        .WithPassword("salescom")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = _postgres.GetConnectionString() + ";Search Path=salescomdbtst",
                ["Jwt:SigningKey"] = "integration-tests-signing-key-must-be-at-least-32-bytes",
                ["Jwt:EncryptionKey"] = "test-16byte-jwekw",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "salescom-api",
                ["Jwt:RequireHttpsMetadata"] = "false",
                ["Jwt:ValidateIssuer"] = "false",
                ["Jwt:ValidateAudience"] = "false",
                ["CentralLogin:BaseUrl"] = "https://salesapptest.banglalink.net/BLAuthentication/",
                ["CentralLogin:ApplicationName"] = "TEST",
                ["CentralLogin:ApplicationKey"] = "test",
            });
        });

        builder.ConfigureServices((_, services) =>
        {
            // Replace test-time auth scheme with a stub that accepts any request and applies
            // a configurable set of permission claims via the Authorization header.
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });
            services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(opt =>
            {
                opt.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                opt.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });

            // Stub the Central Login dependency — integration tests never call the real service.
            // Default behavior: reject every credential/auth-token.
            services.RemoveAll<ICentralLoginClient>();
            services.AddSingleton<ICentralLoginClient, StubCentralLoginClient>();

            // The app's DatabaseInitializer migrates only in Development; this host runs as "Test",
            // so apply migrations here (before the seeder) to create the schema.
            services.AddHostedService<TestDatabaseInitializer>();

            // Seed the test user + its data-source rights after migrations have been applied,
            // so [HasRight]-protected endpoints and GET /me work under the stub auth scheme.
            services.AddHostedService<TestDataSeeder>();
        });
    }

    public async Task EnsureDatabaseCreatedAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SalesComDbContext>();
        await ctx.Database.EnsureCreatedAsync();
    }
}
