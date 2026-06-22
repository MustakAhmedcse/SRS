namespace SalesCom.Application.Queries.Approvals.ListApprovalTypes;

using SalesCom.Application.Mappings;
using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

internal sealed class ListApprovalTypesHandler
    : IQueryHandler<ListApprovalTypesQuery, Result<IReadOnlyList<ApprovalTypeResponse>>>
{
    public Task<Result<IReadOnlyList<ApprovalTypeResponse>>> HandleAsync(
        ListApprovalTypesQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ApprovalTypeResponse> types =
            [.. ApprovalTypeCatalog.All.Select(definition => definition.ToResponse())];

        return Task.FromResult(Result.Success(types));
    }
}
