namespace SalesCom.Application.Queries.Approvals.GetApprovalFlowLevelUserById;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

internal sealed class GetApprovalFlowLevelUserByIdHandler(IUnitOfWork unitOfWork)
    : IQueryHandler<GetApprovalFlowLevelUserByIdQuery, Result<ApprovalFlowLevelUserDetailResponse>>
{
    private const string Sql = """
        SELECT aflu.id                     AS "Id",
               af.id                       AS "ApprovalFlowId",
               aflu.approval_flow_level_id AS "ApprovalFlowLevelId",
               aflu.user_id                AS "UserId",
               af.flow_name                AS "FlowName",
               afl.level_name              AS "LevelName",
               u.user_name                 AS "UserName",
               u.full_name                 AS "FullName",
               u.mobile_no                 AS "MobileNo",
               u.email                     AS "Email"
        FROM approval_flow_level_users aflu
        JOIN approval_flow_levels afl ON afl.id = aflu.approval_flow_level_id
        JOIN approval_flows af ON af.id = afl.approval_flow_id
        JOIN users u ON u.id = aflu.user_id
        WHERE aflu.id = {0}
        """;

    public async Task<Result<ApprovalFlowLevelUserDetailResponse>> HandleAsync(
        GetApprovalFlowLevelUserByIdQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await unitOfWork.QueryAsync<ApprovalFlowLevelUserDetailResponse>(Sql, cancellationToken, query.Id);

        return rows.Count == 0
            ? ApprovalFlowLevelUserErrors.NotFound
            : rows[0];
    }
}
