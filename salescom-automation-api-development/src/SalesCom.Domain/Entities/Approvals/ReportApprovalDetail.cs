namespace SalesCom.Domain.Entities.Approvals;

using SalesCom.Domain.Common;
using SalesCom.Domain.Enums;

/// <summary>One approver's recorded decision at one level of an <see cref="ApprovalRequest"/>.</summary>
public sealed class ReportApprovalDetail : EntityBase<long>
{
    public long ApprovalRequestId { get; set; }

    /// <summary>The level (1-based) this decision was made at.</summary>
    public int LevelOrder { get; set; }

    public ApprovalDecisionType ApprovalStatus { get; set; }

    public string? Remarks { get; set; }

    /// <summary>Login name of the approver who made the decision.</summary>
    public string ApprovalBy { get; set; } = string.Empty;

    public DateTimeOffset ApprovalAt { get; set; }

    // Navigation properties
    public ReportApproval? ApprovalRequest { get; set; }
}
