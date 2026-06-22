namespace SalesCom.Application.Commands.Approvals.CreateApprovalFlow;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

/// <summary>Creates an approval flow. The actor is taken from the authenticated caller.</summary>
public sealed record CreateApprovalFlowCommand(
    string FlowName,
    string? Description) : ICommand<Result<ApprovalFlowResponse>>;
