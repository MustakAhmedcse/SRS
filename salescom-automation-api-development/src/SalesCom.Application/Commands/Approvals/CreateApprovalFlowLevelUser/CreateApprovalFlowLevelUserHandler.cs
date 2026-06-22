namespace SalesCom.Application.Commands.Approvals.CreateApprovalFlowLevelUser;

using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

internal sealed class CreateApprovalFlowLevelUserHandler(IUnitOfWork unitOfWork)
    : ICommandHandler<CreateApprovalFlowLevelUserCommand, Result<ApprovalFlowLevelUserResponse>>
{
    public async Task<Result<ApprovalFlowLevelUserResponse>> HandleAsync(
        CreateApprovalFlowLevelUserCommand command,
        CancellationToken cancellationToken)
    {
        var levelExists = await unitOfWork.Repository<ApprovalFlowLevel>()
            .AnyAsync(l => l.Id == command.ApprovalFlowLevelId, cancellationToken);
        if (!levelExists)
        {
            return ApprovalFlowLevelUserErrors.LevelNotFound;
        }

        var levelUsers = unitOfWork.Repository<ApprovalFlowLevelUser>();

        var alreadyAssigned = await levelUsers.AnyAsync(
            u => u.ApprovalFlowLevelId == command.ApprovalFlowLevelId && u.UserId == command.UserId,
            cancellationToken);
        if (alreadyAssigned)
        {
            return ApprovalFlowLevelUserErrors.AlreadyAssigned;
        }

        var levelUser = new ApprovalFlowLevelUser
        {
            ApprovalFlowLevelId = command.ApprovalFlowLevelId,
            UserId = command.UserId,
        };

        await levelUsers.AddAsync(levelUser, cancellationToken);
        await unitOfWork.Commit(cancellationToken);

        return levelUser.ToResponse();
    }
}
