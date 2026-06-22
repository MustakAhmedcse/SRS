namespace SalesCom.Application.Queries.DataSources.ListDataSources;

using SalesCom.Application.Common;
using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.DataSources;
using SalesCom.Domain.Interfaces;

internal sealed class ListDataSourcesHandler(IUnitOfWork unitOfWork)
    : IQueryHandler<ListDataSourcesQuery, Result<PagedResult<DataSourceSummary>>>
{
    public async Task<Result<PagedResult<DataSourceSummary>>> HandleAsync(
        ListDataSourcesQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var sources = unitOfWork.Repository<DataSource>();

        var items = await sources.PagedAsync(
            predicate: null,
            orderBy: q => q.OrderByDescending(d => d.CreatedAt),
            skip: (page - 1) * pageSize,
            take: pageSize,
            cancellationToken);

        var total = await sources.CountAsync(null, cancellationToken);

        return new PagedResult<DataSourceSummary>(
            items.Select(d => d.ToSummary()).ToList(),
            page,
            pageSize,
            total);
    }
}
