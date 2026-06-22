namespace SalesCom.Application.Commands.Account.Login;

using System.Text.Json.Serialization;
using SalesCom.Application.Commands.Account.VerifyAuthToken;

/// <summary>
/// Login outcome. <c>authType "Normal"</c> (external user) carries a full <see cref="AuthSession"/> —
/// our access token + granted rights — issued in one round-trip. <c>authType "SSO"</c> (internal user)
/// carries the central OTP page URL instead: the client redirects there and, after OTP verification,
/// exchanges the returned auth token via <c>POST /api/account/verify-auth-token</c> for a session.
/// Exactly one of <see cref="Session"/> / <see cref="RedirectUrl"/> is populated; the other is omitted
/// from the JSON.
/// </summary>
public sealed record LoginResponse(
    string AuthType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RedirectUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AuthSession? Session)
{
    public const string NormalAuthType = "Normal";
    public const string SsoAuthType = "SSO";

    public static LoginResponse Sso(string redirectUrl) => new(SsoAuthType, redirectUrl, null);

    public static LoginResponse Normal(AuthSession session) => new(NormalAuthType, null, session);
}
