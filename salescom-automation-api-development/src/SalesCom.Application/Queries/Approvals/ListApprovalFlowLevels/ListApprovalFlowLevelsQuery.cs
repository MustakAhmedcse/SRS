namespace SalesCom.Application.Queries.Approvals.ListApprovalFlowLevels;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

/// <summary>Every approval-flow level (scalar fields only).</summary>
public sealed record ListApprovalFlowLevelsQuery()
    : IQuery<Result<IReadOnlyList<ApprovalFlowLevelResponse>>>;
