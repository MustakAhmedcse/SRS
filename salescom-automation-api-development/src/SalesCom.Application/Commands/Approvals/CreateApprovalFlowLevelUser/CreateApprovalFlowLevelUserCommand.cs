namespace SalesCom.Application.Commands.Approvals.CreateApprovalFlowLevelUser;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

/// <summary>Assigns a user as an approver on an approval-flow level.</summary>
public sealed record CreateApprovalFlowLevelUserCommand(
    long ApprovalFlowLevelId,
    long UserId) : ICommand<Result<ApprovalFlowLevelUserResponse>>;
