namespace SalesCom.Application.Commands.Account.Login;

using SalesCom.Application.Commands.Account.VerifyAuthToken;
using SalesCom.Application.Interfaces;
using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;
using SalesCom.Domain.Enums;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

/// <summary>
/// Delegates authentication to the Central Login service. An external user (<c>authType "Normal"</c>)
/// authenticates in one round-trip: we apply the account gates, provision the local user, and issue this
/// application's own access token via <see cref="IAuthSessionService"/>. An internal user
/// (<c>authType "SSO"</c>) is redirected to the central OTP page and completes login via
/// <see cref="VerifyAuthTokenHandler"/>. Every outcome stages exactly one login-log row (the success
/// path also provisions the user); a single <see cref="IUnitOfWork.Commit"/> flushes the staged work,
/// rolled back if it throws — so the user and its log row commit together or not at all.
/// </summary>
internal sealed class LoginHandler(
    ICentralLoginClient centralLogin,
    IAuthSessionService authSession,
    ILoginLogger loginLog,
    IUnitOfWork unitOfWork) : ICommandHandler<LoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var loginName = command.Username.Trim();

        // Local-development bypass: skip Central Login and issue a session from the local user alone —
        // no DB writes, no login-log row, no commit. The password is ignored. Never enabled in a
        // deployed environment.
        if (authSession.LocalBypassEnabled)
        {
            var bypassSession = await authSession.IssueLocalBypassAsync(loginName, cancellationToken);
            return bypassSession is null
                ? UserErrors.InvalidCredentials
                : LoginResponse.Normal(bypassSession);
        }

        var outcome = await centralLogin.LoginAsync(loginName, command.Password, command.RememberMe, cancellationToken);

        try
        {
            var result = await ResolveAsync(outcome, loginName, cancellationToken);
            await unitOfWork.Commit(cancellationToken);
            return result;
        }
        catch
        {
            await unitOfWork.Rollback();
            throw;
        }
    }

    private async Task<Result<LoginResponse>> ResolveAsync(
        CentralLoginResult outcome, string loginName, CancellationToken cancellationToken)
    {
        switch (outcome.Status)
        {
            case CentralLoginStatus.Unavailable:
                await loginLog.LogAsync(loginName, string.Empty, LoginStatus.Failed, "CENTRAL LOGIN UNAVAILABLE", cancellationToken);
                return ErrorBase.Unexpected(
                    "CentralLogin.Unavailable", outcome.Message ?? "Central login service is unavailable. Try again later.");

            case CentralLoginStatus.Rejected:
                await loginLog.LogAsync(loginName, string.Empty, LoginStatus.Failed, outcome.Message ?? "Invalid credentials", cancellationToken);
                return outcome.Message is null
                    ? UserErrors.InvalidCredentials
                    : ErrorBase.Unauthorized("User.InvalidCredentials", outcome.Message);

            case CentralLoginStatus.SsoRedirect:
                await loginLog.LogAsync(loginName, string.Empty, LoginStatus.Success, "OTP CHALLENGE ISSUED", cancellationToken);
                return LoginResponse.Sso(outcome.RedirectUrl!);

            default:
                // External user (authType "Normal") — central returned userInfo directly, no OTP.
                return await CompleteLoginAsync(outcome.UserInfo!, loginName, cancellationToken);
        }
    }

    private async Task<Result<LoginResponse>> CompleteLoginAsync(
        CentralUserInfo userInfo, string loginName, CancellationToken cancellationToken)
    {
        if (userInfo.IsLocked)
        {
            await loginLog.LogAsync(loginName, userInfo.FullName, LoginStatus.Failed, "USER LOCKED", cancellationToken);
            return UserErrors.Locked;
        }

        if (!userInfo.IsActive)
        {
            await loginLog.LogAsync(loginName, userInfo.FullName, LoginStatus.Failed, "USER INACTIVE", cancellationToken);
            return UserErrors.NotActive;
        }

        var session = await authSession.IssueAsync(userInfo, cancellationToken);
        await loginLog.LogAsync(loginName, userInfo.FullName, LoginStatus.Success, "LOGIN SUCCESS", cancellationToken);
        return LoginResponse.Normal(session);
    }
}
