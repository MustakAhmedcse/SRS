namespace SalesCom.Application.Queries.Users.LookupUserByLogin;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;

/// <summary>Resolves a single user by login name (the <c>UserName</c>) for the approver picker.</summary>
public sealed record LookupUserByLoginQuery(string LoginName)
    : IQuery<Result<UserLookupResponse>>;
