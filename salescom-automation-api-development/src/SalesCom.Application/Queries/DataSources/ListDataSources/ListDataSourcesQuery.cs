namespace SalesCom.Application.Queries.DataSources.ListDataSources;

using SalesCom.Application.Common;
using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;

/// <summary>Paged list of every registered data source (no column payload).</summary>
public sealed record ListDataSourcesQuery(int Page, int PageSize)
    : IQuery<Result<PagedResult<DataSourceSummary>>>;
