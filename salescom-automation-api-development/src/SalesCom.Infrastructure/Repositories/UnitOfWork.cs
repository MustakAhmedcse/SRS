namespace SalesCom.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using SalesCom.Domain.Interfaces;
using SalesCom.Infrastructure.Data;

/// <summary>
/// The single component that holds the <see cref="SalesComDbContext"/>. Everything reaches the database
/// only through this unit of work: <see cref="Repository{T}"/> for entity access and <see cref="QueryAsync"/>
/// for raw SQL that doesn't map to an entity. Repository writes only stage changes on the shared change
/// tracker; <see cref="Commit"/> flushes them in one transaction and <see cref="Rollback"/> discards them
/// by reloading the tracked entities. Registered transient, but every instance wraps the same
/// request-scoped context — one change tracker, one connection.
/// </summary>
internal sealed partial class UnitOfWork(SalesComDbContext context) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = [];

    public IGenericRepository<T> Repository<T>() where T : class
    {
        if (_repositories.TryGetValue(typeof(T), out var existing))
        {
            return (IGenericRepository<T>)existing;
        }

        var repository = new GenericRepository<T>(context);
        _repositories[typeof(T)] = repository;
        return repository;
    }

    public Task<int> Commit(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);

    public Task Rollback()
    {
        context.ChangeTracker.Entries().ToList().ForEach(entry => entry.Reload());
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TResult>> QueryAsync<TResult>(
        string sql, CancellationToken cancellationToken, params object[] parameters) =>
        await context.Database.SqlQueryRaw<TResult>(sql, parameters).ToListAsync(cancellationToken);
}
