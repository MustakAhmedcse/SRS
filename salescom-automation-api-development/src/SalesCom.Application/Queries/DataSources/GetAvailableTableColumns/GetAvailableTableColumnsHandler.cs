namespace SalesCom.Application.Queries.DataSources.GetAvailableTableColumns;

using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

/// <summary>
/// Lists a source table's columns from <c>information_schema</c> (read-only discovery), queried through
/// the unit of work's raw-SQL surface. The columns are aliased to match <see cref="AvailableColumnResponse"/>.
/// </summary>
internal sealed class GetAvailableTableColumnsHandler(IUnitOfWork unitOfWork)
    : IQueryHandler<GetAvailableTableColumnsQuery, Result<IReadOnlyList<AvailableColumnResponse>>>
{
    private const string ColumnsSql = """
        SELECT column_name AS "ColumnName", data_type AS "DataType"
        FROM information_schema.columns
        WHERE LOWER(table_name) = LOWER({0})
          AND table_schema NOT IN ('pg_catalog', 'information_schema')
        ORDER BY ordinal_position
        """;

    public async Task<Result<IReadOnlyList<AvailableColumnResponse>>> HandleAsync(
        GetAvailableTableColumnsQuery query,
        CancellationToken cancellationToken)
    {
        var columns = await unitOfWork.QueryAsync<AvailableColumnResponse>(ColumnsSql, cancellationToken, query.TableName);

        return columns.Count == 0
            ? DataSourceErrors.SourceTableNotFound
            : Result.Success(columns);
    }
}
