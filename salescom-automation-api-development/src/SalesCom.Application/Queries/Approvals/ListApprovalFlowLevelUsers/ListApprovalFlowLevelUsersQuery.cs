namespace SalesCom.Application.Queries.Approvals.ListApprovalFlowLevelUsers;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

/// <summary>Every approval-flow-level user assignment, with flow/level/user display fields.</summary>
public sealed record ListApprovalFlowLevelUsersQuery()
    : IQuery<Result<IReadOnlyList<ApprovalFlowLevelUserDetailResponse>>>;
