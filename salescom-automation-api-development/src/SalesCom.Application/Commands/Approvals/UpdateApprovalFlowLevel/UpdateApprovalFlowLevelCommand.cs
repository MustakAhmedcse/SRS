namespace SalesCom.Application.Commands.Approvals.UpdateApprovalFlowLevel;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

/// <summary>
/// Renames a level. The type and order are fixed at creation (order is backend-assigned and the type
/// rule is flow-wide), so only the name is editable. The actor is taken from the authenticated caller.
/// </summary>
public sealed record UpdateApprovalFlowLevelCommand(
    long Id,
    string LevelName) : ICommand<Result<ApprovalFlowLevelResponse>>;
