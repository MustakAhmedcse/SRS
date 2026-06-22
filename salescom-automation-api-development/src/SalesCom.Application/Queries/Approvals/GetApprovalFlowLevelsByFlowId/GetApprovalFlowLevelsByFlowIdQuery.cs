namespace SalesCom.Application.Queries.Approvals.GetApprovalFlowLevelsByFlowId;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

/// <summary>Levels of one approval flow, ordered by <c>LevelOrder</c>.</summary>
public sealed record GetApprovalFlowLevelsByFlowIdQuery(long FlowId)
    : IQuery<Result<IReadOnlyList<ApprovalFlowLevelResponse>>>;
