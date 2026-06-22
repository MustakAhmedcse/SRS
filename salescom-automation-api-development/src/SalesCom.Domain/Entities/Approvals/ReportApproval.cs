namespace SalesCom.Domain.Entities.Approvals;

using SalesCom.Domain.Common;
using SalesCom.Domain.Enums;
using SalesCom.Domain.Entities.Reporting;

/// <summary>
/// A live approval in progress: a <see cref="ReportSetup"/> submitted against an
/// <see cref="ApprovalFlow"/>, tracking the current level and overall status as approvers act.
/// </summary>
public sealed class ReportApproval : EntityBase<long>
{
    public long ReportSetupId { get; set; }

    public long ApprovalFlowId { get; set; }

    /// <summary>1-based level currently awaiting a decision.</summary>
    public int CurrentLevelOrder { get; set; }

    public ApprovalRequestStatus OverallStatus { get; set; } = ApprovalRequestStatus.Draft;

    /// <summary>Login name of the user who initiated the request.</summary>
    public string InitiatedBy { get; set; } = string.Empty;

    public DateTimeOffset InitiatedAt { get; set; }

    //Naviagtion Properties
    public List<ReportApprovalDetail> ApprovalDecisions { get; set; } = [];

    public ReportSetup? ReportSetup { get; set; }
}
