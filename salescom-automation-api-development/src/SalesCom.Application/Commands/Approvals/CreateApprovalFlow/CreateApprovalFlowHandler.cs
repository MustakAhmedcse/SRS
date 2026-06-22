namespace SalesCom.Application.Commands.Approvals.CreateApprovalFlow;

using SalesCom.Application.Interfaces;
using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;
using SalesCom.Domain.Interfaces;

internal sealed class CreateApprovalFlowHandler(
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<CreateApprovalFlowCommand, Result<ApprovalFlowResponse>>
{
    public async Task<Result<ApprovalFlowResponse>> HandleAsync(
        CreateApprovalFlowCommand command,
        CancellationToken cancellationToken)
    {
        var actor = string.IsNullOrWhiteSpace(currentUser.UserName) ? "system" : currentUser.UserName;

        var flow = new ApprovalFlow
        {
            FlowName = command.FlowName.Trim(),
            Description = command.Description?.Trim(),
            CreatedBy = actor,
            CreatedAt = clock.UtcNow,
        };

        await unitOfWork.Repository<ApprovalFlow>().AddAsync(flow, cancellationToken);
        await unitOfWork.Commit(cancellationToken);

        return flow.ToResponse();
    }
}
