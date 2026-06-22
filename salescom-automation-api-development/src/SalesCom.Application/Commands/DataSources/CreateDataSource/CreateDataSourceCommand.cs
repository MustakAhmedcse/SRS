namespace SalesCom.Application.Commands.DataSources.CreateDataSource;

using SalesCom.Application.Messaging;
using SalesCom.Application.Queries.DataSources.GetDataSourceById;
using SalesCom.Domain.Common;

/// <summary>Registers a source table (typically one ending in <c>_COM</c>) for commission processing.</summary>
/// <c>GET /api/data-sources/available-tables/{tableName}/columns</c>.
/// </summary>
public sealed record CreateDataSourceCommand(
    string SourceTableName,
    string? TableDescription,
    bool IsActive) : ICommand<Result<DataSourceResponse>>;
