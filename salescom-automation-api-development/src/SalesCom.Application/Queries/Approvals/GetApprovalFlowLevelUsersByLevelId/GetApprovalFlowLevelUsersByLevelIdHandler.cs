namespace SalesCom.Application.Queries.Approvals.GetApprovalFlowLevelUsersByLevelId;

using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;
using SalesCom.Domain.Interfaces;

internal sealed class GetApprovalFlowLevelUsersByLevelIdHandler(IUnitOfWork unitOfWork)
    : IQueryHandler<GetApprovalFlowLevelUsersByLevelIdQuery, Result<IReadOnlyList<ApprovalFlowLevelUserResponse>>>
{
    public async Task<Result<IReadOnlyList<ApprovalFlowLevelUserResponse>>> HandleAsync(
        GetApprovalFlowLevelUsersByLevelIdQuery query,
        CancellationToken cancellationToken)
    {
        var levelUsers = await unitOfWork.Repository<ApprovalFlowLevelUser>()
            .ListAsync(u => u.ApprovalFlowLevelId == query.LevelId, track: false, cancellationToken);

        return levelUsers.Select(u => u.ToResponse()).ToList();
    }
}
