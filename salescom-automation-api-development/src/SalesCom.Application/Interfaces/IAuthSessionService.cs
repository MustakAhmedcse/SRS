namespace SalesCom.Application.Interfaces;

using SalesCom.Application.Commands.Account.VerifyAuthToken;

/// <summary>
/// Turns a successful Central Login into a local session: provisions/refreshes the local user and
/// issues this application's own access token. The returned session also carries the user's granted
/// right ids and previous login times (the token itself stays minimal). Persistence is staged on the
/// request's unit of work — the caller owns the single <c>Commit</c> so the user upsert and its
/// login-log row commit atomically. No refresh token, no central-token storage.
/// </summary>
public interface IAuthSessionService
{
    /// <summary>
    /// Local-development only: when true the login flow skips the Central Login service and
    /// authenticates against the local <c>users</c> table by username alone. Always false in a
    /// deployed environment.
    /// </summary>
    bool LocalBypassEnabled { get; }

    Task<AuthSession> IssueAsync(CentralUserInfo userInfo, CancellationToken cancellationToken);

    /// <summary>
    /// Local-development bypass: issues a session for an existing local user resolved by username,
    /// reading only — no user upsert, no login-log row, nothing staged for commit. Returns null when
    /// no such user exists. Used only when <see cref="LocalBypassEnabled"/> is true.
    /// </summary>
    Task<AuthSession?> IssueLocalBypassAsync(string userName, CancellationToken cancellationToken);
}
