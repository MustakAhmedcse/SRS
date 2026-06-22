namespace SalesCom.Application.Commands.Account.VerifyAuthToken;

using SalesCom.Application.Commands.Account.Login;
using SalesCom.Application.Interfaces;
using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;
using SalesCom.Domain.Enums;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

/// <summary>
/// Completes the SSO/2FA login: validates the post-OTP auth token with the Central Login service,
/// applies the same account gates as <see cref="LoginHandler"/>, then provisions the user and issues
/// our own access token. Every outcome stages exactly one login-log row (the success path also
/// provisions the user); a single <see cref="IUnitOfWork.Commit"/> flushes the staged work, rolled
/// back if it throws — so the user and its log row commit together or not at all.
/// </summary>
internal sealed class VerifyAuthTokenHandler(
    ICentralLoginClient centralLogin,
    IAuthSessionService authSession,
    ILoginLogger loginLog,
    IUnitOfWork unitOfWork) : ICommandHandler<VerifyAuthTokenCommand, Result<AuthSession>>
{
    public async Task<Result<AuthSession>> HandleAsync(VerifyAuthTokenCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var result = await ResolveAsync(command, cancellationToken);
            await unitOfWork.Commit(cancellationToken);
            return result;
        }
        catch
        {
            await unitOfWork.Rollback();
            throw;
        }
    }

    private async Task<Result<AuthSession>> ResolveAsync(VerifyAuthTokenCommand command, CancellationToken cancellationToken)
    {
        var outcome = await centralLogin.VerifyAuthTokenAsync(command.AuthToken.Trim(), cancellationToken);

        if (outcome.Status == CentralLoginStatus.Unavailable)
        {
            await loginLog.LogAsync(string.Empty, string.Empty, LoginStatus.Failed, "CENTRAL LOGIN UNAVAILABLE", cancellationToken);
            return ErrorBase.Unexpected(
                "CentralLogin.Unavailable", outcome.Message ?? "Central login service is unavailable. Try again later.");
        }

        if (outcome.Status != CentralLoginStatus.Success || outcome.UserInfo is null)
        {
            await loginLog.LogAsync(string.Empty, string.Empty, LoginStatus.Failed, "OTP VERIFICATION FAILED", cancellationToken);
            return ErrorBase.Unauthorized(
                "User.AuthTokenInvalid", outcome.Message ?? "The authentication token is invalid or has expired.");
        }

        var userInfo = outcome.UserInfo;

        if (userInfo.IsLocked)
        {
            await loginLog.LogAsync(userInfo.LoginName, userInfo.FullName, LoginStatus.Failed, "USER LOCKED", cancellationToken);
            return UserErrors.Locked;
        }

        if (!userInfo.IsActive)
        {
            await loginLog.LogAsync(userInfo.LoginName, userInfo.FullName, LoginStatus.Failed, "USER INACTIVE", cancellationToken);
            return UserErrors.NotActive;
        }

        var session = await authSession.IssueAsync(userInfo, cancellationToken);
        await loginLog.LogAsync(userInfo.LoginName, userInfo.FullName, LoginStatus.Success, "OTP LOGIN SUCCESS", cancellationToken);
        return session;
    }
}
