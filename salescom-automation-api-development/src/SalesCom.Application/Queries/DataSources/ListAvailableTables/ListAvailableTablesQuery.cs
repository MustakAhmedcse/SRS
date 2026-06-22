namespace SalesCom.Application.Queries.DataSources.ListAvailableTables;

using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;

/// <summary>
/// Every <c>_COM</c> table (case-insensitive) in the application's Postgres database that has not
/// yet been registered as a data source. Returned as a flat list — drives the "pick a table to
/// register" picker, which needs the full set up-front.
/// </summary>
public sealed record ListAvailableTablesQuery()
    : IQuery<Result<IReadOnlyList<AvailableTableResponse>>>;
