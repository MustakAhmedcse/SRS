namespace SalesCom.Application.Commands.Approvals.CreateApprovalFlowLevel;

using SalesCom.Application.Interfaces;
using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;
using SalesCom.Domain.Enums;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

internal sealed class CreateApprovalFlowLevelHandler(
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<CreateApprovalFlowLevelCommand, Result<ApprovalFlowLevelResponse>>
{
    public async Task<Result<ApprovalFlowLevelResponse>> HandleAsync(
        CreateApprovalFlowLevelCommand command,
        CancellationToken cancellationToken)
    {
        var flowExists = await unitOfWork.Repository<ApprovalFlow>()
            .AnyAsync(f => f.Id == command.ApprovalFlowId, cancellationToken);
        if (!flowExists)
        {
            return ApprovalFlowLevelErrors.FlowNotFound;
        }

        var levels = unitOfWork.Repository<ApprovalFlowLevel>();

        // Level name is unique within a flow, compared case-insensitively.
        var levelName = command.LevelName.Trim();
        var nameTaken = await levels.AnyAsync(
            l => l.ApprovalFlowId == command.ApprovalFlowId
                && l.LevelName.ToLower() == levelName.ToLower(),
            cancellationToken);
        if (nameTaken)
        {
            return ApprovalFlowLevelErrors.DuplicateLevelName;
        }

        // Type rule (flow-wide): Setup Review is blocked once a Report Run exists; a Report Run needs
        // at least one Setup Review first — so the first level is always a Setup Review.
        if (command.ApprovalType == ApprovalType.SetupReview)
        {
            var hasReportRun = await levels.AnyAsync(
                l => l.ApprovalFlowId == command.ApprovalFlowId && l.ApprovalType == ApprovalType.ReportRun,
                cancellationToken);
            if (hasReportRun)
            {
                return ApprovalFlowLevelErrors.SetupReviewAfterReportRun;
            }
        }
        else
        {
            var hasSetupReview = await levels.AnyAsync(
                l => l.ApprovalFlowId == command.ApprovalFlowId && l.ApprovalType == ApprovalType.SetupReview,
                cancellationToken);
            if (!hasSetupReview)
            {
                return ApprovalFlowLevelErrors.ReportRunRequiresSetupReview;
            }
        }

        // Order is assigned by the backend: the next slot in the flow.
        var levelOrder = await levels.CountAsync(
            l => l.ApprovalFlowId == command.ApprovalFlowId, cancellationToken) + 1;

        var actor = string.IsNullOrWhiteSpace(currentUser.UserName) ? "system" : currentUser.UserName;

        var level = new ApprovalFlowLevel
        {
            ApprovalFlowId = command.ApprovalFlowId,
            ApprovalType = command.ApprovalType,
            LevelName = levelName,
            LevelOrder = levelOrder,
            CreatedBy = actor,
            CreatedAt = clock.UtcNow,
        };

        await levels.AddAsync(level, cancellationToken);
        await unitOfWork.Commit(cancellationToken);

        return level.ToResponse();
    }
}
