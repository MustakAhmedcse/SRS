namespace SalesCom.Domain.Errors;

using SalesCom.Domain.Common;

/// <summary>Outcome errors for the approval-flow use cases. Field-level validation lives in the validators.</summary>
public static class ApprovalFlowErrors
{
    public static readonly ErrorBase NotFound = ErrorBase.NotFound(
        "ApprovalFlow.NotFound",
        "Approval flow not found.");
}
