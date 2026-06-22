namespace SalesCom.Domain.Entities.Approvals;

using SalesCom.Domain.Common;
using SalesCom.Domain.Enums;

/// <summary>One ordered level (step) of an <see cref="ApprovalFlow"/>, with its assigned approvers.</summary>
public sealed class ApprovalFlowLevel : EntityBase<long>
{
    public long ApprovalFlowId { get; set; }

    /// <summary>Reference into the fixed <c>ApprovalTypeCatalog</c>; persisted as its stable int id.</summary>
    public ApprovalType ApprovalType { get; set; }

    public int LevelOrder { get; set; }

    public string LevelName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public string? UpdatedBy { get; set; }

    //navigation Properties
    public ApprovalFlow? ApprovalFlow { get; set; }

    public List<ApprovalFlowLevelUser> ApprovalLevelUsers { get; set; } = [];
}
