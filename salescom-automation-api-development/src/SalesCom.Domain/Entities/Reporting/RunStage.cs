namespace SalesCom.Domain.Entities.Reporting;

using SalesCom.Domain.Common;
using SalesCom.Domain.Enums;

/// <summary>One ordered SQL stage executed within a <see cref="ReportRun"/>, with its generated output artifact.</summary>
public sealed class RunStage : EntityBase<long>
{
    public long RunId { get; set; }

    public string SqlText { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public RunStatus RunStatus { get; set; } = RunStatus.Pending;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public string? DocumentType { get; set; }

    public string? Bucket { get; set; }

    public string? ObjectUrl { get; set; }

    public string? FileName { get; set; }

    public DateTimeOffset FileGeneratedAt { get; set; }

    public string? OutputTableName { get; set; }

    public CleanupStatus CleanupStatus { get; set; } = CleanupStatus.Pending;

    //Navigation Properties

    public ReportRun? ReportRun { get; set; }
}