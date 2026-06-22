namespace SalesCom.Infrastructure.Repositories;

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SalesCom.Domain.Interfaces;
using SalesCom.Infrastructure.Data;

internal sealed partial class UnitOfWork
{
    /// <summary>
    /// EF Core implementation of <see cref="IGenericRepository{T}"/>, private to the unit of work so the
    /// DbContext is never exposed. Shares the unit of work's context — one change tracker, one transaction.
    /// </summary>
    private sealed class GenericRepository<TEntity>(SalesComDbContext context) : IGenericRepository<TEntity>
        where TEntity : class
    {
        private readonly DbSet<TEntity> _set = context.Set<TEntity>();

        public async Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken) =>
            await _set.FindAsync([id], cancellationToken);

        public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, bool track, CancellationToken cancellationToken) =>
            Base(track).FirstOrDefaultAsync(predicate, cancellationToken);

        public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken) =>
            _set.AnyAsync(predicate, cancellationToken);

        public Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate, CancellationToken cancellationToken) =>
            predicate is null
                ? _set.CountAsync(cancellationToken)
                : _set.CountAsync(predicate, cancellationToken);

        public async Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate, bool track, CancellationToken cancellationToken)
        {
            var query = Base(track);
            if (predicate is not null)
            {
                query = query.Where(predicate);
            }

            return await query.ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<TEntity>> PagedAsync(
            Expression<Func<TEntity, bool>>? predicate,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            IQueryable<TEntity> query = _set.AsNoTracking();
            if (predicate is not null)
            {
                query = query.Where(predicate);
            }

            if (orderBy is not null)
            {
                query = orderBy(query);
            }

            return await query.Skip(skip).Take(take).ToListAsync(cancellationToken);
        }

        public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken)
        {
            await _set.AddAsync(entity, cancellationToken);
            return entity;
        }

        public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken) =>
            _set.AddRangeAsync(entities, cancellationToken);

        public Task UpdateAsync(TEntity entity, CancellationToken cancellationToken)
        {
            _set.Update(entity);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(TEntity entity, CancellationToken cancellationToken)
        {
            _set.Remove(entity);
            return Task.CompletedTask;
        }

        private IQueryable<TEntity> Base(bool track) => track ? _set : _set.AsNoTracking();
    }
}
