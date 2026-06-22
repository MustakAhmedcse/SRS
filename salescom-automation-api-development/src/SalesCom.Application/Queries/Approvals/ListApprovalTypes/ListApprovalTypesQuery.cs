namespace SalesCom.Application.Queries.Approvals.ListApprovalTypes;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

/// <summary>Every approval type — populates the type dropdown in the frontend.</summary>
public sealed record ListApprovalTypesQuery()
    : IQuery<Result<IReadOnlyList<ApprovalTypeResponse>>>;
