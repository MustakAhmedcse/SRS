namespace SalesCom.Domain.Entities.Disbursement;

using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Reporting;
using SalesCom.Domain.Enums;

/// <summary>A POS disbursement dump produced for a <see cref="Reporting.ReportRun"/>.</summary>
public sealed class PosDisbursement : EntityBase<long>
{
    public long ReportRunId { get; set; }

    public DisburseStatus DumpStatus { get; set; }

    public DateTimeOffset? DisburseAt { get; set; }

    //Navigation Properties

    public ReportRun? ReportRun { get; set; }
}
