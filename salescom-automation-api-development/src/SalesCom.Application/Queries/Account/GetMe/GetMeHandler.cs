namespace SalesCom.Application.Queries.Account.GetMe;

using SalesCom.Application.Interfaces;
using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Identity;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

/// <summary>
/// Returns the authenticated caller's profile: the local <see cref="User"/> looked up by the central
/// user id from the token, plus the granted right ids resolved from the DB (rights are no longer in the
/// token).
/// </summary>
internal sealed class GetMeHandler(ICurrentUser currentUser, IUserRightsQuery rightsQuery, IUnitOfWork unitOfWork)
    : IQueryHandler<GetMeQuery, Result<MeResponse>>
{
    public async Task<Result<MeResponse>> HandleAsync(GetMeQuery query, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId))
        {
            return UserErrors.NotFound;
        }

        var user = await unitOfWork.Repository<User>()
            .FirstOrDefaultAsync(u => u.UserId == currentUser.UserId, track: false, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        var rights = await rightsQuery.GetRightsAsync(currentUser.UserId, cancellationToken);

        return new MeResponse(
            UserId: user.UserId,
            UserName: user.UserName,
            FullName: user.FullName,
            Email: string.IsNullOrWhiteSpace(user.Email) ? null : user.Email,
            MobileNo: user.MobileNo,
            Department: user.Department,
            Rights: rights);
    }
}
