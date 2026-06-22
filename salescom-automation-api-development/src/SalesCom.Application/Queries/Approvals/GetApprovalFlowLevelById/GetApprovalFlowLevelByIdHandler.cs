namespace SalesCom.Application.Queries.Approvals.GetApprovalFlowLevelById;

using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

internal sealed class GetApprovalFlowLevelByIdHandler(IUnitOfWork unitOfWork)
    : IQueryHandler<GetApprovalFlowLevelByIdQuery, Result<ApprovalFlowLevelResponse>>
{
    public async Task<Result<ApprovalFlowLevelResponse>> HandleAsync(
        GetApprovalFlowLevelByIdQuery query,
        CancellationToken cancellationToken)
    {
        var level = await unitOfWork.Repository<ApprovalFlowLevel>()
            .GetByIdAsync(query.Id, cancellationToken);
        if (level is null)
        {
            return ApprovalFlowLevelErrors.NotFound;
        }

        return level.ToResponse();
    }
}
