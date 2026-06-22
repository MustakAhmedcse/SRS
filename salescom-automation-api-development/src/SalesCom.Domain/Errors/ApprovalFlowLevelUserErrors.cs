namespace SalesCom.Domain.Errors;

using SalesCom.Domain.Common;

/// <summary>Outcome errors for the approval-flow-level-user use cases. Field-level validation lives in the validators.</summary>
public static class ApprovalFlowLevelUserErrors
{
    public static readonly ErrorBase NotFound = ErrorBase.NotFound(
        "ApprovalFlowLevelUser.NotFound",
        "Approval flow level user not found.");

    public static readonly ErrorBase LevelNotFound = ErrorBase.NotFound(
        "ApprovalFlowLevelUser.LevelNotFound",
        "The specified approval flow level does not exist.");

    public static readonly ErrorBase AlreadyAssigned = ErrorBase.Conflict(
        "ApprovalFlowLevelUser.AlreadyAssigned",
        "This user is already assigned to the approval level.");
}
