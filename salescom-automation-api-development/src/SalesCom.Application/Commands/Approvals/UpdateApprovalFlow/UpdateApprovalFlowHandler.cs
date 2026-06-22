namespace SalesCom.Application.Commands.Approvals.UpdateApprovalFlow;

using SalesCom.Application.Interfaces;
using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

internal sealed class UpdateApprovalFlowHandler(
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<UpdateApprovalFlowCommand, Result<ApprovalFlowResponse>>
{
    public async Task<Result<ApprovalFlowResponse>> HandleAsync(
        UpdateApprovalFlowCommand command,
        CancellationToken cancellationToken)
    {
        var flow = await unitOfWork.Repository<ApprovalFlow>()
            .GetByIdAsync(command.Id, cancellationToken);
        if (flow is null)
        {
            return ApprovalFlowErrors.NotFound;
        }

        flow.FlowName = command.FlowName.Trim();
        flow.Description = command.Description?.Trim();
        flow.UpdatedBy = string.IsNullOrWhiteSpace(currentUser.UserName) ? "system" : currentUser.UserName;
        flow.UpdatedAt = clock.UtcNow;

        await unitOfWork.Commit(cancellationToken);

        return flow.ToResponse();
    }
}
