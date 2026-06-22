namespace SalesCom.Application.Queries.Approvals.GetApprovalFlowById;

using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

internal sealed class GetApprovalFlowByIdHandler(IUnitOfWork unitOfWork)
    : IQueryHandler<GetApprovalFlowByIdQuery, Result<ApprovalFlowResponse>>
{
    public async Task<Result<ApprovalFlowResponse>> HandleAsync(
        GetApprovalFlowByIdQuery query,
        CancellationToken cancellationToken)
    {
        var flow = await unitOfWork.Repository<ApprovalFlow>()
            .GetByIdAsync(query.Id, cancellationToken);
        if (flow is null)
        {
            return ApprovalFlowErrors.NotFound;
        }

        return flow.ToResponse();
    }
}
