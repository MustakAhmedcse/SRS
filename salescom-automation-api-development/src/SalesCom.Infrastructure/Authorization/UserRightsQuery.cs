namespace SalesCom.Infrastructure.Authorization;

using SalesCom.Application.Interfaces;
using SalesCom.Domain.Entities.Identity;
using SalesCom.Domain.Interfaces;

/// <summary>
/// Resolves a user's granted right ids through the unit of work. Filtering <c>user_rights</c> by the
/// <c>User</c> navigation lets EF emit a single <c>user_rights → users</c> INNER JOIN on the central user
/// id — one round-trip, still through the generic repository. No caching — grant edits take effect immediately.
/// </summary>
internal sealed class UserRightsQuery(IUnitOfWork unitOfWork) : IUserRightsQuery
{
    public async Task<IReadOnlyList<int>> GetRightsAsync(string userId, CancellationToken cancellationToken)
    {
        var rights = await unitOfWork.Repository<UserRight>()
            .ListAsync(r => r.User!.UserId == userId, track: false, cancellationToken);

        return rights.Select(r => r.RightsCode).Distinct().OrderBy(r => r).ToList();
    }
}
