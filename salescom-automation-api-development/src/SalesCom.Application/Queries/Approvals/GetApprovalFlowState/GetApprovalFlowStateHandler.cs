namespace SalesCom.Application.Queries.Approvals.GetApprovalFlowState;

using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;
using SalesCom.Domain.Enums;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

internal sealed class GetApprovalFlowStateHandler(IUnitOfWork unitOfWork)
    : IQueryHandler<GetApprovalFlowStateQuery, Result<ApprovalFlowStateResponse>>
{
    public async Task<Result<ApprovalFlowStateResponse>> HandleAsync(
        GetApprovalFlowStateQuery query,
        CancellationToken cancellationToken)
    {
        var flowExists = await unitOfWork.Repository<ApprovalFlow>()
            .AnyAsync(f => f.Id == query.FlowId, cancellationToken);
        if (!flowExists)
        {
            return ApprovalFlowLevelErrors.FlowNotFound;
        }

        var levels = await unitOfWork.Repository<ApprovalFlowLevel>()
            .ListAsync(l => l.ApprovalFlowId == query.FlowId, track: false, cancellationToken);

        var hasReportRun = levels.Any(l => l.ApprovalType == ApprovalType.ReportRun);
        var hasSetupReview = levels.Any(l => l.ApprovalType == ApprovalType.SetupReview);

        // Same flow-wide type rule the create handler enforces: Setup Review until a Report Run exists;
        // Report Run only after at least one Setup Review.
        IReadOnlyList<ApprovalTypeResponse> allowedTypes =
        [
            .. ApprovalTypeCatalog.All
                .Where(definition => definition.Type switch
                {
                    ApprovalType.SetupReview => !hasReportRun,
                    ApprovalType.ReportRun => hasSetupReview,
                    _ => false,
                })
                .Select(definition => definition.ToResponse())
        ];

        return new ApprovalFlowStateResponse(levels.Count + 1, allowedTypes);
    }
}
