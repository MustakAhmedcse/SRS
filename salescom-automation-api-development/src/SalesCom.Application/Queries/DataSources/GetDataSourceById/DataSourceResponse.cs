namespace SalesCom.Application.Queries.DataSources.GetDataSourceById;

using System.Text.Json.Serialization;

/// <summary>A registered data source.</summary>
public sealed record DataSourceResponse(
    long Id,
    string SourceTableName,
    string? TableDescription,
    bool IsActive,
    DateTimeOffset CreatedOn,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTimeOffset? UpdatedOn);
