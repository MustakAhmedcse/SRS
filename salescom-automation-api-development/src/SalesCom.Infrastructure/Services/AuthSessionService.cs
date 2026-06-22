namespace SalesCom.Infrastructure.Services;

using System.Globalization;
using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using SalesCom.Application.Commands.Account.VerifyAuthToken;
using SalesCom.Application.Common;
using SalesCom.Application.Interfaces;
using SalesCom.Domain.Entities.Auditing;
using SalesCom.Domain.Entities.Identity;
using SalesCom.Domain.Enums;
using SalesCom.Domain.Interfaces;
using SalesCom.Infrastructure.Configurations;

/// <summary>
/// Issues this application's own session on top of Central Login. Provisions/refreshes the local
/// <see cref="User"/> (keyed by the central user id) through the unit of work, resolves the user's
/// granted right ids and previous login times, and mints our access token. The right ids and last-login
/// times travel in the returned session, not inside the (encrypted) token. Writes are staged on the
/// request's unit of work and flushed by the caller's single <see cref="IUnitOfWork.Commit"/>. No
/// refresh token and no central-token storage: the access token is the whole session.
/// </summary>
internal sealed class AuthSessionService(
    IUnitOfWork unitOfWork,
    IJwtTokenGenerator tokenGenerator,
    IOptions<CentralLoginConfiguration> centralLoginOptions,
    IClock clock) : IAuthSessionService
{
    private const string ProvisionedBy = "central-login";

    public bool LocalBypassEnabled => centralLoginOptions.Value.BypassEnabled;

    public async Task<AuthSession> IssueAsync(CentralUserInfo userInfo, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var centralUserId = userInfo.UserId.ToString(CultureInfo.InvariantCulture);
        var users = unitOfWork.Repository<User>();

        var user = await users.FirstOrDefaultAsync(u => u.UserId == centralUserId, track: true, cancellationToken);
        IReadOnlyList<int> rights;

        if (user is null)
        {
            //user = new User
            //{
            //    UserId = centralUserId,
            //    UserName = userInfo.LoginName,
            //    FullName = userInfo.FullName,
            //    MobileNo = userInfo.MobileNumber ?? string.Empty,
            //    Email = userInfo.Email ?? string.Empty,
            //    Department = userInfo.Department ?? string.Empty,
            //    CreatedOn = now,
            //    CreatedBy = ProvisionedBy,
            //};

            //await users.AddAsync(user, cancellationToken);
            //rights = []; // A brand-new user has no rights until they are granted in the database.

            throw new UnauthorizedAccessException(
                $"User '{userInfo.LoginName}' is not registered in SalesCom.");
        }
        else
        {
            user.UserName = userInfo.LoginName;
            user.FullName = userInfo.FullName;
            user.MobileNo = userInfo.MobileNumber ?? string.Empty;
            user.Email = userInfo.Email ?? string.Empty;
            user.Department = userInfo.Department ?? string.Empty;
            user.UpdatedAt = now;
            user.UpdatedBy = ProvisionedBy;

            var granted = await unitOfWork.Repository<UserRight>()
                .ListAsync(r => r.UserId == user.Id, track: false, cancellationToken);
            rights = granted.Select(r => r.RightsCode).Distinct().OrderBy(r => r).ToList();
        }

        var (lastSuccess, lastFailed) = await ResolveLastLoginsAsync(userInfo.LoginName, cancellationToken);

        var token = tokenGenerator.Generate(new LoginContext(centralUserId, userInfo.LoginName, userInfo.Email));

        return new AuthSession(token.AccessToken, token.ExpiresAtUtc, rights, userInfo.FullName, userInfo.LoginName, lastSuccess, lastFailed);
    }

    public async Task<AuthSession?> IssueLocalBypassAsync(string userName, CancellationToken cancellationToken)
    {
        var user = await unitOfWork.Repository<User>()
            .FirstOrDefaultAsync(u => u.UserName == userName, track: false, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var granted = await unitOfWork.Repository<UserRight>()
            .ListAsync(r => r.UserId == user.Id, track: false, cancellationToken);
        var rights = granted.Select(r => r.RightsCode).Distinct().OrderBy(r => r).ToList();

        var token = tokenGenerator.Generate(new LoginContext(user.UserId, user.UserName, user.Email));

        return new AuthSession(token.AccessToken, token.ExpiresAtUtc, rights, user.FullName, user.UserName);
    }

    /// <summary>
    /// The user's previous successful and failed login times, read from the login audit table. The
    /// current attempt is logged by the caller after this runs, so these reflect prior attempts. An OTP
    /// challenge is logged as a success but excluded here — it is a redirect, not a completed sign-in.
    /// </summary>
    private async Task<(DateTimeOffset? Success, DateTimeOffset? Failed)> ResolveLastLoginsAsync(
        string loginName, CancellationToken cancellationToken)
    {
        var logs = unitOfWork.Repository<LoginLog>();

        var lastSuccess = await LatestLoginTimeAsync(
            logs,
            l => l.UserName == loginName && l.LoginStatus == LoginStatus.Success && l.Remarks != LoginRemarks.OtpChallengeIssued,
            cancellationToken);

        var lastFailed = await LatestLoginTimeAsync(
            logs,
            l => l.UserName == loginName && l.LoginStatus == LoginStatus.Failed,
            cancellationToken);

        return (lastSuccess, lastFailed);
    }

    private static async Task<DateTimeOffset?> LatestLoginTimeAsync(
        IGenericRepository<LoginLog> logs,
        Expression<Func<LoginLog, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var rows = await logs.PagedAsync(predicate, q => q.OrderByDescending(l => l.Id), skip: 0, take: 1, cancellationToken);

        return rows.Count > 0 ? rows[0].LoginTime : null;
    }
}
