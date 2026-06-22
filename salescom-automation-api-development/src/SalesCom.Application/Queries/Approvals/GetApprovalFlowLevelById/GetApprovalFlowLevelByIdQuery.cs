namespace SalesCom.Application.Queries.Approvals.GetApprovalFlowLevelById;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

public sealed record GetApprovalFlowLevelByIdQuery(long Id) : IQuery<Result<ApprovalFlowLevelResponse>>;
