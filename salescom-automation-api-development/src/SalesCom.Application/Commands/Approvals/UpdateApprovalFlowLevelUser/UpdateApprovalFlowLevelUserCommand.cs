namespace SalesCom.Application.Commands.Approvals.UpdateApprovalFlowLevelUser;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

/// <summary>Reassigns an approver slot to a different user.</summary>
public sealed record UpdateApprovalFlowLevelUserCommand(
    long Id,
    long UserId) : ICommand<Result<ApprovalFlowLevelUserResponse>>;
