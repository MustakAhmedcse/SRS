namespace SalesCom.Domain.Errors;

using SalesCom.Domain.Common;

/// <summary>Outcome errors for the approval-flow-level use cases. Field-level validation lives in the validators.</summary>
public static class ApprovalFlowLevelErrors
{
    public static readonly ErrorBase NotFound = ErrorBase.NotFound(
        "ApprovalFlowLevel.NotFound",
        "Approval flow level not found.");

    public static readonly ErrorBase FlowNotFound = ErrorBase.NotFound(
        "ApprovalFlowLevel.FlowNotFound",
        "The specified approval flow does not exist.");

    public static readonly ErrorBase DuplicateLevelName = ErrorBase.Conflict(
        "ApprovalFlowLevel.DuplicateLevelName",
        "A level with this name already exists in this flow.");

    public static readonly ErrorBase SetupReviewAfterReportRun = ErrorBase.Conflict(
        "ApprovalFlowLevel.SetupReviewAfterReportRun",
        "Setup Review cannot be added once a Report Run exists in this flow.");

    public static readonly ErrorBase ReportRunRequiresSetupReview = ErrorBase.Conflict(
        "ApprovalFlowLevel.ReportRunRequiresSetupReview",
        "A Report Run requires at least one Setup Review in the flow first.");
}
