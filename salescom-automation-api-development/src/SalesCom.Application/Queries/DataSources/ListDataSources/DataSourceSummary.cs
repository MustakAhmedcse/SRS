namespace SalesCom.Application.Queries.DataSources.ListDataSources;

using System.Text.Json.Serialization;

/// <summary>Lightweight row for the registered-data-source list.</summary>
public sealed record DataSourceSummary(
    long Id,
    string SourceTableName,
    string? TableDescription,
    bool IsActive,
    DateTimeOffset CreatedOn,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTimeOffset? UpdatedOn);
