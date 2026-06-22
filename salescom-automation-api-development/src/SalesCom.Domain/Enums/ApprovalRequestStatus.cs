namespace SalesCom.Domain.Enums;

using SalesCom.Domain.Entities.Approvals;

/// <summary>Overall state of a multi-level <see cref="ReportApproval"/>.</summary>
public enum ApprovalRequestStatus
{
    Draft = 0,
    PreApprovalPending = 1,
    PreApproval = 2,
    PostApprovaPending = 3,
    PostApprova = 4,
}
