namespace SalesCom.Domain.Entities.Approvals;

using SalesCom.Domain.Common;

/// <summary>An ordered multi-level approval chain.</summary>
public sealed class ApprovalFlow : EntityBase<long>
{
    public string FlowName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public string? UpdatedBy { get; set; }

    //Navigation properties

    public List<ApprovalFlowLevel> ApprovalFlowLevels { get; set; } = [];
}
