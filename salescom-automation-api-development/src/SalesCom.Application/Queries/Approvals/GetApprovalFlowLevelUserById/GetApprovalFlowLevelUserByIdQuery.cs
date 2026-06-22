namespace SalesCom.Application.Queries.Approvals.GetApprovalFlowLevelUserById;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

public sealed record GetApprovalFlowLevelUserByIdQuery(long Id) : IQuery<Result<ApprovalFlowLevelUserDetailResponse>>;
