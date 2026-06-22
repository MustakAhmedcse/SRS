namespace SalesCom.Application.Common;

/// <summary>
/// Remark texts stamped on <see cref="SalesCom.Domain.Entities.Auditing.LoginLog"/> rows that are also
/// matched when reading them back. <see cref="OtpChallengeIssued"/> is logged as a success at login but
/// excluded when resolving a user's last successful sign-in — a challenge is a redirect, not a login.
/// </summary>
public static class LoginRemarks
{
    public const string OtpChallengeIssued = "OTP CHALLENGE ISSUED";
}
