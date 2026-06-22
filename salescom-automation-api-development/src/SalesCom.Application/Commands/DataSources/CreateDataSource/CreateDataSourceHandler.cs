namespace SalesCom.Application.Commands.DataSources.CreateDataSource;

using SalesCom.Application.Interfaces;
using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Queries.DataSources.GetDataSourceById;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.DataSources;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

internal sealed class CreateDataSourceHandler(
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<CreateDataSourceCommand, Result<DataSourceResponse>>
{
    public async Task<Result<DataSourceResponse>> HandleAsync(
    CreateDataSourceCommand command,
    CancellationToken cancellationToken)
    {
        var sourceTable = command.SourceTableName.Trim();
        var sources = unitOfWork.Repository<DataSource>();

        var lowered = sourceTable.ToLower();
        if (await sources.AnyAsync(d => d.SourceTableName.ToLower() == lowered, cancellationToken))
        {
            return DataSourceErrors.AlreadyRegistered;
        }

        var dataSource = new DataSource
        {
            SourceTableName = sourceTable,
            TableDescription = command.TableDescription?.Trim(),
            IsActive = command.IsActive,
            CreatedAt = clock.UtcNow,
            CreatedBy = currentUser.UserName,
        };

        try
        {
            await sources.AddAsync(dataSource, cancellationToken);
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
