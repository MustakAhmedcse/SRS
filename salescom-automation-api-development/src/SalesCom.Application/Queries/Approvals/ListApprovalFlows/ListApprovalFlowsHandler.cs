namespace SalesCom.Application.Queries.Approvals.ListApprovalFlows;

using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;
using SalesCom.Domain.Interfaces;

internal sealed class ListApprovalFlowsHandler(IUnitOfWork unitOfWork)
    : IQueryHandler<ListApprovalFlowsQuery, Result<IReadOnlyList<ApprovalFlowResponse>>>
{
    public async Task<Result<IReadOnlyList<ApprovalFlowResponse>>> HandleAsync(
        ListApprovalFlowsQuery query,
        CancellationToken cancellationToken)
    {
        var flows = await unitOfWork.Repository<ApprovalFlow>()
            .ListAsync(predicate: null, track: false, cancellationToken);

        return flows.Select(f => f.ToResponse()).ToList();
    }
}
