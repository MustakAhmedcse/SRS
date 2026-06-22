namespace SalesCom.Domain.Entities.Reporting;

using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;
using SalesCom.Domain.Entities.Channels;
using SalesCom.Domain.Enums;
using System.Text.Json;

/// <summary>
/// A configured commission report build. The <see cref="Definition"/> section lives in a <c>jsonb</c>
/// column so its shape can evolve without migrations.
/// </summary>
public sealed class ReportSetup : EntityBase<long>
{
    public string ReportName { get; set; } = string.Empty;

    public string ReportType { get; set; } = string.Empty;

    public long ChannelTypeId { get; set; }

    public string CommissionCycle { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsSetupComplete { get; set; }

    public bool IsRecurrent { get; set; }

    public ReportRecurrentType RecurrentType { get; set; } = ReportRecurrentType.None;

    public bool IsEvDisbursement { get; set; }

    public TimeOnly? EvDisbursementTime { get; set; }

    public bool IsPosDisbursement { get; set; }

    public JsonDocument? Definition { get; set; }

    public DateTimeOffset? RunStartDate { get; set; }

    public DateTimeOffset? RunEndDate { get; set; }

    public bool IsReportStop { get; set; }

    public string? SmsContent { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public string? UpdatedBy { get; set; }

    public long ApprovalFlowId { get; set; }

    //Navigation Properties
    public Channel? Channel { get; set; }

    public ApprovalFlow? ApprovalFlow { get; set; }
}
