namespace SalesCom.Domain.Common;

/// <summary>
/// Base class for persisted entities: the primary key plus the EF Core concurrency token. Entities
/// that need audit columns carry their own <c>CreatedOn</c>/<c>UpdatedOn</c>/<c>CreatedBy</c>/<c>UpdatedBy</c>
/// (set explicitly by handlers). <typeparamref name="TId"/> is the primitive key type.
/// </summary>
public abstract class EntityBase<TId>
    where TId : notnull
{
    public TId Id { get; set; } = default!;

    /// <summary>EF Core concurrency token (Postgres <c>xmin</c>).</summary>
    public uint Version { get; set; }
}
