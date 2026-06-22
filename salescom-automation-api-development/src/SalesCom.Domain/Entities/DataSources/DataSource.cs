namespace SalesCom.Domain.Entities.DataSources;

using SalesCom.Domain.Common;

/// <summary>A source table from the application's Postgres database registered for commission processing.</summary>
public sealed class DataSource : EntityBase<long>
{
    public string SourceTableName { get; set; } = string.Empty;

    public string? TableDescription { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public string? UpdatedBy { get; set; }
}
