namespace SalesCom.Application.Commands.DataSources.UpdateDataSource;

using SalesCom.Application.Interfaces;
using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Queries.DataSources.GetDataSourceById;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.DataSources;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

internal sealed class UpdateDataSourceHandler(
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<UpdateDataSourceCommand, Result<DataSourceResponse>>
{
    public async Task<Result<DataSourceResponse>> HandleAsync(
        UpdateDataSourceCommand command,
        CancellationToken cancellationToken)
    {
        var sources = unitOfWork.Repository<DataSource>();
        var dataSource = await sources.GetByIdAsync(command.Id, cancellationToken);
        if (dataSource is null)
        {
            return DataSourceErrors.NotFound;
        }

        dataSource.TableDescription = command.TableDescription?.Trim();
        dataSource.IsActive = command.IsActive;
        dataSource.UpdatedAt = clock.UtcNow;
        dataSource.UpdatedBy = currentUser.UserName;

        try
        {
            await sources.UpdateAsync(dataSource, cancellationToken);
            await unitOfWork.Commit(cancellationToken);
        }
        catch
        {
            await unitOfWork.Rollback();
            throw;
        }

        return dataSource.ToResponse();
    }
}
