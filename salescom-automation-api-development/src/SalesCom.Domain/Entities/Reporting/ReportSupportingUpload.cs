namespace SalesCom.Domain.Entities.Reporting;

using SalesCom.Domain.Common;

/// <summary>A supporting data file uploaded for a <see cref="ReportSetup"/>, landed into a DB table and object store.</summary>
public sealed class ReportSupportingUpload : EntityBase<long>
{
    public long ReportSetupId { get; set; }

    public string DbTableName { get; set; } = string.Empty;

    public string DbSchema { get; set; } = string.Empty;

    public string ObjectBucket { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public int? RowCount { get; set; }

    public DateTimeOffset UploadedAt { get; set; }

    public string UploadedBy { get; set; } = string.Empty;

    // Navigation Properties

    public ReportSetup? ReportSetup { get; set; }
}
