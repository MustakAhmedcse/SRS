namespace SalesCom.Application.Queries.Approvals.ListApprovalFlowLevelUsers;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Interfaces;

internal sealed class ListApprovalFlowLevelUsersHandler(IUnitOfWork unitOfWork)
    : IQueryHandler<ListApprovalFlowLevelUsersQuery, Result<IReadOnlyList<ApprovalFlowLevelUserDetailResponse>>>
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
        ORDER BY aflu.id
        """;

    public async Task<Result<IReadOnlyList<ApprovalFlowLevelUserDetailResponse>>> HandleAsync(
        ListApprovalFlowLevelUsersQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await unitOfWork.QueryAsync<ApprovalFlowLevelUserDetailResponse>(Sql, cancellationToken);

        return Result.Success(rows);
    }
}
