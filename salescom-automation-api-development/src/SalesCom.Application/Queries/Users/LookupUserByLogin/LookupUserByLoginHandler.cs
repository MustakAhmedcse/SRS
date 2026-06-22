namespace SalesCom.Application.Queries.Users.LookupUserByLogin;

using SalesCom.Application.Messaging;
using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Identity;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

internal sealed class LookupUserByLoginHandler(IUnitOfWork unitOfWork)
    : IQueryHandler<LookupUserByLoginQuery, Result<UserLookupResponse>>
{
    public async Task<Result<UserLookupResponse>> HandleAsync(
        LookupUserByLoginQuery query,
        CancellationToken cancellationToken)
    {
        var loginName = query.LoginName?.Trim() ?? string.Empty;
        if (loginName.Length == 0)
        {
            return UserErrors.NotFound;
        }

        // Login name is matched case-insensitively; it isn't uniquely indexed, so take the first match.
        var user = await unitOfWork.Repository<User>()
            .FirstOrDefaultAsync(u => u.UserName.ToLower() == loginName.ToLower(), track: false, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        return new UserLookupResponse(user.Id, user.FullName, user.MobileNo, user.Email);
    }
}
