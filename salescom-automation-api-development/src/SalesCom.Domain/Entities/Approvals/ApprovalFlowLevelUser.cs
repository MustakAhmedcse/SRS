namespace SalesCom.Domain.Entities.Approvals;

using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Identity;

/// <summary>An approver assigned to one <see cref="ApprovalFlowLevel"/>, referenced by central user id.</summary>
public sealed class ApprovalFlowLevelUser : EntityBase<long>
{
    public long ApprovalFlowLevelId { get; set; }

    public long UserId { get; set; }

    //Navigation Properties
    public ApprovalFlowLevel? ApprovalFlowLevel { get; set; }

    public User? User { get; set; }
}
