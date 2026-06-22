namespace SalesCom.Application.Responses;

/// <summary>One approver assigned to an approval-flow level.</summary>
public sealed record ApprovalFlowLevelUserResponse(
    long Id,
    long ApprovalFlowLevelId,
    long UserId);
