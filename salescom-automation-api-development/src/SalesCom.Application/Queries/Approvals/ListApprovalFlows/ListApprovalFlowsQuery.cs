namespace SalesCom.Application.Queries.Approvals.ListApprovalFlows;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

/// <summary>Every registered approval flow (scalar fields only, no level payload).</summary>
public sealed record ListApprovalFlowsQuery()
    : IQuery<Result<IReadOnlyList<ApprovalFlowResponse>>>;
