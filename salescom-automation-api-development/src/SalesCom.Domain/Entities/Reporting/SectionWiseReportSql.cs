namespace SalesCom.Domain.Entities.Reporting;

using SalesCom.Domain.Common;

/// <summary>An ordered SQL stage template defined on a <see cref="ReportSetup"/>, executed on each run.</summary>
public sealed class SectionWiseReportSql : EntityBase<long>
{
    public long ReportSetupId { get; set; }

    public int StageOrder { get; set; }

    public string? SqlText { get; set; }

    // Navigation Properties

    public ReportSetup? ReportSetup { get; set; }
}
