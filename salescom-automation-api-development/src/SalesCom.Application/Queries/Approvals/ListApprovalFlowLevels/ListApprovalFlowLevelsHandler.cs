namespace SalesCom.Application.Queries.Approvals.ListApprovalFlowLevels;

using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;
using SalesCom.Domain.Interfaces;

internal sealed class ListApprovalFlowLevelsHandler(IUnitOfWork unitOfWork)
    : IQueryHandler<ListApprovalFlowLevelsQuery, Result<IReadOnlyList<ApprovalFlowLevelResponse>>>
{
    public async Task<Result<IReadOnlyList<ApprovalFlowLevelResponse>>> HandleAsync(
        ListApprovalFlowLevelsQuery query,
        CancellationToken cancellationToken)
    {
        var levels = await unitOfWork.Repository<ApprovalFlowLevel>()
            .ListAsync(predicate: null, track: false, cancellationToken);

        return levels.Select(l => l.ToResponse()).ToList();
    }
}
