namespace SalesCom.Application.Responses;

/// <summary>
/// An approver row for the details list: the flow and level it belongs to, plus the approver's login and
/// contact fields — joined from the flow, level and user tables.
/// </summary>
public sealed record ApprovalFlowLevelUserDetailResponse(
    long Id,
    long ApprovalFlowId,
    long ApprovalFlowLevelId,
    long UserId,
    string FlowName,
    string LevelName,
    string UserName,
    string FullName,
    string MobileNo,
    string Email);
