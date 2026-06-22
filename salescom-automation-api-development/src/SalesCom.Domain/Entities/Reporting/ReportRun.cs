namespace SalesCom.Domain.Entities.Reporting;

using SalesCom.Domain.Common;
using SalesCom.Domain.Enums;

/// <summary>One execution of a <see cref="ReportSetup"/> — a demo or final run with its disbursement state.</summary>
public sealed class ReportRun : EntityBase<long>
{
    public long ReportSetupId { get; set; }

    public DateTimeOffset RunDate { get; set; }

    public ReportRunType RunType { get; set; }

    /// <summary>
    /// User name who triggered or system
    /// </summary>
    public string? TriggeredBy { get; set; }

    public RunStatus RunStatus { get; set; } = RunStatus.Pending;

    public string DisburseStatus { get; set; } = string.Empty;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    //Navigation Properties
    public ReportSetup? ReportSetup { get; set; }
}
