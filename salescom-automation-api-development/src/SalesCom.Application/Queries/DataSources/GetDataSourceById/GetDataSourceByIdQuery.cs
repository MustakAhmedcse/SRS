namespace SalesCom.Application.Queries.DataSources.GetDataSourceById;

using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;

public sealed record GetDataSourceByIdQuery(long Id) : IQuery<Result<DataSourceResponse>>;
