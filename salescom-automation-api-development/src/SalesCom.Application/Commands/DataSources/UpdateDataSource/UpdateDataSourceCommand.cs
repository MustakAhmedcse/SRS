namespace SalesCom.Application.Commands.DataSources.UpdateDataSource;

using SalesCom.Application.Messaging;
using SalesCom.Application.Queries.DataSources.GetDataSourceById;
using SalesCom.Domain.Common;

/// <summary>Updates a registered data source's description / active flag.</summary>
public sealed record UpdateDataSourceCommand(
    long Id,
    string? TableDescription,
    bool IsActive) : ICommand<Result<DataSourceResponse>>;
