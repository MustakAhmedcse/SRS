namespace SalesCom.Application.Queries.Approvals.GetApprovalFlowById;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

public sealed record GetApprovalFlowByIdQuery(long Id) : IQuery<Result<ApprovalFlowResponse>>;
