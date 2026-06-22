namespace SalesCom.Application.Mappings;

using SalesCom.Application.Queries.DataSources.GetDataSourceById;
using SalesCom.Application.Queries.DataSources.ListDataSources;
using SalesCom.Domain.Entities.DataSources;

/// <summary>Entity-to-response mapping for the data-source feature, kept out of the plain entities.</summary>
internal static class DataSourceMappingExtensions
{
    public static DataSourceResponse ToResponse(this DataSource source) =>
        new(source.Id, source.SourceTableName, source.TableDescription, source.IsActive, source.CreatedAt, source.UpdatedAt);

    public static DataSourceSummary ToSummary(this DataSource source) =>
        new(source.Id, source.SourceTableName, source.TableDescription, source.IsActive, source.CreatedAt, source.UpdatedAt);
}
