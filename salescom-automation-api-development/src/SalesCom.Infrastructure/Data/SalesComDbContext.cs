namespace SalesCom.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

/// <summary>
/// Single bounded-context DbContext, reached only through the unit of work. It declares no
/// <c>DbSet</c> properties — the model is built generically from the
/// <c>IEntityTypeConfiguration</c> classes in this assembly, and the generic repository resolves each
/// entity set dynamically via <see cref="DbContext.Set{TEntity}"/>. The default schema is taken from
/// the connection string's <c>Search Path</c>, so the schema lives in one place (the connection
/// string) rather than a hard-coded constant. The <see cref="AuditSaveChangesInterceptor"/> captures a
/// before/after change-audit trail in the same transaction as every save.
/// </summary>
public sealed class SalesComDbContext : DbContext
{
    private readonly string? _connectionString;

    public SalesComDbContext(DbContextOptions<SalesComDbContext> options) : base(options)
    {
        // Captured from the options (not via Database, which can't be touched inside OnModelCreating).
        _connectionString = options.Extensions.OfType<RelationalOptionsExtension>().FirstOrDefault()?.ConnectionString;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var schema = new NpgsqlConnectionStringBuilder(_connectionString ?? string.Empty).SearchPath;
        if (!string.IsNullOrWhiteSpace(schema))
        {
            modelBuilder.HasDefaultSchema(schema);
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesComDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
