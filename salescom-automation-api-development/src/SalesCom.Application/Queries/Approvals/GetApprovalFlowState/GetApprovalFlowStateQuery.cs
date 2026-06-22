namespace SalesCom.Application.Queries.Approvals.GetApprovalFlowState;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

/// <summary>The order the next level will get, plus the still-selectable approval types, for a flow.</summary>
public sealed record GetApprovalFlowStateQuery(long FlowId)
    : IQuery<Result<ApprovalFlowStateResponse>>;
