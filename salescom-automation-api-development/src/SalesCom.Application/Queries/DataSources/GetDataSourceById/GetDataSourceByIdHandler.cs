namespace SalesCom.Application.Queries.DataSources.GetDataSourceById;

using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.DataSources;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

internal sealed class GetDataSourceByIdHandler(IUnitOfWork unitOfWork)
    : IQueryHandler<GetDataSourceByIdQuery, Result<DataSourceResponse>>
{
    public async Task<Result<DataSourceResponse>> HandleAsync(
        GetDataSourceByIdQuery query,
        CancellationToken cancellationToken)
    {
        var dataSource = await unitOfWork.Repository<DataSource>().GetByIdAsync(query.Id, cancellationToken);
        return dataSource is null
            ? DataSourceErrors.NotFound
            : dataSource.ToResponse();
    }
}
