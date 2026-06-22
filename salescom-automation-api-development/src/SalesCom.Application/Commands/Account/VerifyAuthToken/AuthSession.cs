namespace SalesCom.Application.Commands.Account.VerifyAuthToken;

/// <summary>
/// A session this application issues after Central Login authenticates a user: our own access token
/// (short-lived), the caller's granted right ids, and the user's previous successful / failed login
/// times (null when there is no prior attempt). The token is JWE-encrypted and opaque to the client,
/// so the rights and last-login times travel here in the response, not inside the token. Access-token
/// only — there is no refresh token and the central service's own tokens are not returned.
/// </summary>
public sealed record AuthSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    IReadOnlyList<int> Rights,
    string FullName,
    string UserName,
    DateTimeOffset? LastLoginSuccessAtUtc = null,
    DateTimeOffset? LastLoginFailedAtUtc = null);
