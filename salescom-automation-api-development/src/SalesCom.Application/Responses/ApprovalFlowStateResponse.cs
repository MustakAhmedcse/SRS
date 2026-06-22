namespace SalesCom.Application.Responses;

/// <summary>
/// The current state of an approval flow for the "add level" form: the order the next level will be
/// given, and which approval types may still be selected (per the flow-wide type rule).
/// </summary>
public sealed record ApprovalFlowStateResponse(
    int NextOrder,
    IReadOnlyList<ApprovalTypeResponse> AllowedTypes);
