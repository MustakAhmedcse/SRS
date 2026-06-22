namespace SalesCom.Application.Queries.DataSources.GetAvailableTableColumns;

/// <summary>One column of an available source table, read from <c>information_schema</c>.</summary>
public sealed record AvailableColumnResponse(string ColumnName, string DataType);
