namespace SalesCom.Application.Commands.Approvals.UpdateApprovalFlow;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

/// <summary>Updates an approval flow's editable fields. The actor is taken from the authenticated caller.</summary>
public sealed record UpdateApprovalFlowCommand(
    long Id,
    string FlowName,
    string? Description) : ICommand<Result<ApprovalFlowResponse>>;
