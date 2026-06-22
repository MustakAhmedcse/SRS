namespace SalesCom.Application.Queries.DataSources.GetAvailableTableColumns;

using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;

/// <summary>
/// Returns every column of <paramref name="TableName"/> introspected from
/// <c>information_schema.columns</c> on the live source database — a read-only preview.
/// </summary>
public sealed record GetAvailableTableColumnsQuery(string TableName)
    : IQuery<Result<IReadOnlyList<AvailableColumnResponse>>>;
