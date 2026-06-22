namespace SalesCom.Application.Queries.Approvals.GetApprovalFlowLevelUsersByLevelId;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

/// <summary>Approvers assigned to one approval-flow level.</summary>
public sealed record GetApprovalFlowLevelUsersByLevelIdQuery(long LevelId)
    : IQuery<Result<IReadOnlyList<ApprovalFlowLevelUserResponse>>>;
