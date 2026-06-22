namespace SalesCom.Application.Queries.DataSources.ListAvailableTables;

/// <summary>One available-source table name, returned by <c>GET /api/data-sources/available-tables</c>.</summary>
public sealed record AvailableTableResponse(string TableName);
