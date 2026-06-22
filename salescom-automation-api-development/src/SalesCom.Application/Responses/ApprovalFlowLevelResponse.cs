namespace SalesCom.Application.Responses;

/// <summary>One approval-flow level — scalar fields only. Approvers are served by the level-user endpoints.</summary>
public sealed record ApprovalFlowLevelResponse(
    long Id,
    long ApprovalFlowId,
    int ApprovalTypeId,
    string LevelName,
    int LevelOrder,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn,
    string CreatedBy,
    string? UpdatedBy);

/// <summary>One approval type — used to populate the type dropdown in the frontend.</summary>
public sealed record ApprovalTypeResponse(
    int Id,
    string TypeName,
    int SortOrder,
    string Phase);
