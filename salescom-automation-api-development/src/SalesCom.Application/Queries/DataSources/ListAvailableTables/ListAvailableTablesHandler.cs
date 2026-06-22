namespace SalesCom.Application.Queries.DataSources.ListAvailableTables;

using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.DataSources;
using SalesCom.Domain.Interfaces;

/// <summary>
/// Lists the available source tables (names ending in <c>_com</c>) from <c>information_schema</c>, queried
/// through the unit of work, minus those already registered as data sources.
/// </summary>
internal sealed class ListAvailableTablesHandler(IUnitOfWork unitOfWork)
    : IQueryHandler<ListAvailableTablesQuery, Result<IReadOnlyList<AvailableTableResponse>>>
{
    private const string AvailableTablesSql = """
        SELECT table_name AS "Value"
        FROM information_schema.tables
        WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
          AND table_type = 'BASE TABLE'
          AND (LOWER(table_name) LIKE '%\_com' ESCAPE '\' OR LOWER(table_name) LIKE '%\_arc' ESCAPE '\')
        ORDER BY table_name
        """;

    public async Task<Result<IReadOnlyList<AvailableTableResponse>>> HandleAsync(
        ListAvailableTablesQuery query,
        CancellationToken cancellationToken)
    {
        var tables = await unitOfWork.QueryAsync<string>(AvailableTablesSql, cancellationToken);

        var registered = await unitOfWork.Repository<DataSource>()
            .ListAsync(predicate: null, track: false, cancellationToken);
        var alreadyTaken = new HashSet<string>(registered.Select(d => d.SourceTableName), StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<AvailableTableResponse> response = tables
            .Where(t => !alreadyTaken.Contains(t))
            .Select(t => new AvailableTableResponse(t))
            .ToList();

        return Result.Success(response);
    }
}
