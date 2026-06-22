namespace SalesCom.Domain.Errors;

using SalesCom.Domain.Common;

/// <summary>Outcome errors for the data-source use cases. Field-level validation lives in the validators.</summary>
public static class DataSourceErrors
{
    public static readonly ErrorBase NotFound = ErrorBase.NotFound(
        "DataSource.NotFound",
        "Data source not found.");

    public static readonly ErrorBase SourceTableNotFound = ErrorBase.NotFound(
        "DataSource.SourceTableNotFound",
        "The requested source table does not exist or has no columns.");

    public static readonly ErrorBase AlreadyRegistered = ErrorBase.Conflict(
        "DataSource.AlreadyRegistered",
        "A data source for this table is already registered.");
}
