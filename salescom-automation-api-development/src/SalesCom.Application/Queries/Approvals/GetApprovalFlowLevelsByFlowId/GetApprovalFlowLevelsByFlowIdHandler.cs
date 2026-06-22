namespace SalesCom.Application.Queries.Approvals.GetApprovalFlowLevelsByFlowId;

using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;
using SalesCom.Domain.Interfaces;

internal sealed class GetApprovalFlowLevelsByFlowIdHandler(IUnitOfWork unitOfWork)
    : IQueryHandler<GetApprovalFlowLevelsByFlowIdQuery, Result<IReadOnlyList<ApprovalFlowLevelResponse>>>
{
    public async Task<Result<IReadOnlyList<ApprovalFlowLevelResponse>>> HandleAsync(
        GetApprovalFlowLevelsByFlowIdQuery query,
        CancellationToken cancellationToken)
    {
        var levels = await unitOfWork.Repository<ApprovalFlowLevel>()
            .ListAsync(l => l.ApprovalFlowId == query.FlowId, track: false, cancellationToken);

        return levels
            .OrderBy(l => l.LevelOrder)
            .Select(l => l.ToResponse())
            .ToList();
    }
}
