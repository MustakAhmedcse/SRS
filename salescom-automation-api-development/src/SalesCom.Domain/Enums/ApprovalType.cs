namespace SalesCom.Domain.Enums;

/// <summary>
/// Fixed catalog of approver roles assignable to an <see cref="Entities.Approvals.ApprovalFlowLevel"/>;
/// persisted as its stable int id. Members are placeholders — refine to the real catalog when known.
/// </summary>
public enum ApprovalType
{
    SetupReview = 1,
    ReportRun = 2,
}
