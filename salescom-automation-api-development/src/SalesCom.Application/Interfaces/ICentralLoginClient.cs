namespace SalesCom.Application.Interfaces;

/// <summary>
/// Gateway to the Central Login service, which owns credential verification and the 2FA/OTP flow.
/// External users authenticate in one round-trip (<see cref="CentralLoginStatus.Success"/>);
/// internal users are redirected to the central OTP page (<see cref="CentralLoginStatus.SsoRedirect"/>)
/// and complete login by exchanging the returned auth token via <see cref="VerifyAuthTokenAsync"/>.
/// Implementations never throw for a down or rejecting service — they return
/// <see cref="CentralLoginStatus.Unavailable"/> / <see cref="CentralLoginStatus.Rejected"/>.
/// </summary>
public interface ICentralLoginClient
{
    Task<CentralLoginResult> LoginAsync(string userName, string password, bool rememberMe, CancellationToken cancellationToken);

    Task<CentralLoginResult> VerifyAuthTokenAsync(string authToken, CancellationToken cancellationToken);
}

public enum CentralLoginStatus
{
    /// <summary>Authenticated; <see cref="CentralLoginResult.UserInfo"/> is populated.</summary>
    Success = 0,

    /// <summary>OTP required; <see cref="CentralLoginResult.RedirectUrl"/> points at the central OTP page.</summary>
    SsoRedirect = 1,

    /// <summary>Credentials or auth token rejected; <see cref="CentralLoginResult.Message"/> carries the central reason.</summary>
    Rejected = 2,

    /// <summary>The central service could not be reached or answered out of contract.</summary>
    Unavailable = 3,
}

public sealed record CentralLoginResult(
    CentralLoginStatus Status,
    CentralUserInfo? UserInfo,
    string? RedirectUrl,
    string? Message);

/// <summary>The slice of the central <c>userInfo</c> payload this application consumes.</summary>
public sealed record CentralUserInfo(
    int UserId,
    string LoginName,
    string FullName,
    string? Email,
    string? MobileNumber,
    bool IsInternal,
    bool IsLocked,
    bool IsActive,
    int? CenterId,
    string? Department,
    int? UserGroupId);
