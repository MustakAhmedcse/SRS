namespace SalesCom.Application.Commands.Approvals.CreateApprovalFlowLevel;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Enums;

/// <summary>
/// Creates a level within an approval flow. The level order is assigned by the backend (next in the
/// flow), never supplied by the caller. The actor is taken from the authenticated caller.
/// </summary>
public sealed record CreateApprovalFlowLevelCommand(
    long ApprovalFlowId,
    ApprovalType ApprovalType,
    string LevelName) : ICommand<Result<ApprovalFlowLevelResponse>>;
