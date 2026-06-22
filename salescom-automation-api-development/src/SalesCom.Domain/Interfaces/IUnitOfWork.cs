namespace SalesCom.Domain.Interfaces;

/// <summary>
/// The single persistence entry point for a command. Resolve the generic <see cref="Repository{T}"/>
/// for any aggregate, or run <see cref="QueryAsync{TResult}"/> for raw SQL that doesn't map to an
/// entity — no handler or service ever touches the DbContext directly. Repository writes only stage
/// changes; a handler flushes them with a single <see cref="Commit"/> (all-or-none) and discards them
/// with <see cref="Rollback"/> if anything fails.
/// </summary>
public interface IUnitOfWork
{
    IGenericRepository<T> Repository<T>() where T : class;

    /// <summary>Runs a raw SQL query and materializes the rows as <typeparamref name="TResult"/>.</summary>
    Task<IReadOnlyList<TResult>> QueryAsync<TResult>(string sql, CancellationToken cancellationToken, params object[] parameters);

    /// <summary>Flushes every staged change to the database in one transaction; returns the affected row count.</summary>
    Task<int> Commit(CancellationToken cancellationToken);

    /// <summary>Discards staged changes by reloading every tracked entity from the database.</summary>
    Task Rollback();
}
