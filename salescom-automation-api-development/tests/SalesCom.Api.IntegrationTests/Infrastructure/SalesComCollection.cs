namespace SalesCom.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Shares a single <see cref="SalesComFactory"/> (one Postgres container + one host) across every
/// integration test class. Besides being faster, this builds the host — and therefore configures
/// Serilog's bootstrap logger — exactly once per test run, avoiding the "logger is already frozen"
/// error that a per-class host would hit on the second build.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SalesComCollection : ICollectionFixture<SalesComFactory>
{
    public const string Name = "SalesCom integration";
}
