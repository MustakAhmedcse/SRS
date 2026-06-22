namespace SalesCom.Application.Commands.Approvals.UpdateApprovalFlowLevelUser;

using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

internal sealed class UpdateApprovalFlowLevelUserHandler(IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateApprovalFlowLevelUserCommand, Result<ApprovalFlowLevelUserResponse>>
{
    public async Task<Result<ApprovalFlowLevelUserResponse>> HandleAsync(
        UpdateApprovalFlowLevelUserCommand command,
        CancellationToken cancellationToken)
    {
        var levelUsers = unitOfWork.Repository<ApprovalFlowLevelUser>();

        var levelUser = await levelUsers.GetByIdAsync(command.Id, cancellationToken);
        if (levelUser is null)
        {
            return ApprovalFlowLevelUserErrors.NotFound;
        }

        if (levelUser.UserId != command.UserId)
        {
            var alreadyAssigned = await levelUsers.AnyAsync(
                u => u.ApprovalFlowLevelId == levelUser.ApprovalFlowLevelId
                    && u.UserId == command.UserId
                    && u.Id != command.Id,
                cancellationToken);
            if (alreadyAssigned)
            {
                return ApprovalFlowLevelUserErrors.AlreadyAssigned;
            }
        }

        levelUser.UserId = command.UserId;

        await unitOfWork.Commit(cancellationToken);

        return levelUser.ToResponse();
    }
}
