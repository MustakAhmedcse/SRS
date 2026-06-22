namespace SalesCom.Application.Commands.Approvals.UpdateApprovalFlowLevel;

using SalesCom.Application.Interfaces;
using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

internal sealed class UpdateApprovalFlowLevelHandler(
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<UpdateApprovalFlowLevelCommand, Result<ApprovalFlowLevelResponse>>
{
    public async Task<Result<ApprovalFlowLevelResponse>> HandleAsync(
        UpdateApprovalFlowLevelCommand command,
        CancellationToken cancellationToken)
    {
        var levels = unitOfWork.Repository<ApprovalFlowLevel>();

        var level = await levels.GetByIdAsync(command.Id, cancellationToken);
        if (level is null)
        {
            return ApprovalFlowLevelErrors.NotFound;
        }

        // The new name must stay unique within the flow (case-insensitive), ignoring this level itself.
        var levelName = command.LevelName.Trim();
        var nameTaken = await levels.AnyAsync(
            l => l.ApprovalFlowId == level.ApprovalFlowId
                && l.Id != level.Id
                && l.LevelName.ToLower() == levelName.ToLower(),
            cancellationToken);
        if (nameTaken)
        {
            return ApprovalFlowLevelErrors.DuplicateLevelName;
        }

        level.LevelName = levelName;
        level.UpdatedBy = string.IsNullOrWhiteSpace(currentUser.UserName) ? "system" : currentUser.UserName;
        level.UpdatedAt = clock.UtcNow;

        await unitOfWork.Commit(cancellationToken);

        return level.ToResponse();
    }
}
