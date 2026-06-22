namespace SalesCom.Domain.Entities.Disbursement;

using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Reporting;
using SalesCom.Domain.Enums;

/// <summary>A per-channel electronic-value disbursement produced for a <see cref="Reporting.ReportRun"/>.</summary>
public sealed class EvDisburse : EntityBase<long>
{
    public long ReportRunId { get; set; }

    public string ChannelCode { get; set; } = string.Empty;

    public string EvMsisdn { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DisburseStatus DisburseStatus { get; set; }

    public DateTimeOffset? DisburseAt { get; set; }

    // Navigation Properties

    public ReportRun? ReportRun { get; set; }
}
