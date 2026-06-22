namespace SalesCom.Domain.Interfaces;

using System.Linq.Expressions;

/// <summary>
/// Generic persistence contract for an aggregate of type <typeparamref name="T"/>. Reached through
/// <see cref="IUnitOfWork.Repository{T}"/> rather than a hand-written per-entity repository. Reads
/// materialize inside Infrastructure so the Application layer never touches EF Core directly — it
/// passes only BCL <see cref="Expression{TDelegate}"/> predicates and ordering lambdas.
/// <para>
/// Writes only <em>stage</em> the change on the unit of work's change tracker; nothing reaches the
/// database until <see cref="IUnitOfWork.Commit"/> flushes every staged change in one transaction
/// (or <see cref="IUnitOfWork.Rollback"/> discards them). <see cref="AddAsync"/> returns the staged
/// entity — its DB-generated key is populated only after the commit.
/// </para>
/// </summary>
public interface IGenericRepository<T> where T : class
{
    /// <summary>Primary-key lookup. Returns a tracked entity (EF <c>FindAsync</c>), suitable for updates.</summary>
    Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken);

    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, bool track, CancellationToken cancellationToken);

    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);

    Task<int> CountAsync(Expression<Func<T, bool>>? predicate, CancellationToken cancellationToken);

    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate, bool track, CancellationToken cancellationToken);

    Task<IReadOnlyList<T>> PagedAsync(
        Expression<Func<T, bool>>? predicate,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy,
        int skip,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Stages an insert; returns the same entity. Its DB-generated key is set after the commit.</summary>
    Task<T> AddAsync(T entity, CancellationToken cancellationToken);

    /// <summary>Stages an insert for each entity.</summary>
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken);

    /// <summary>Stages an update to the entity.</summary>
    Task UpdateAsync(T entity, CancellationToken cancellationToken);

    /// <summary>Stages a delete of the entity.</summary>
    Task RemoveAsync(T entity, CancellationToken cancellationToken);
}
