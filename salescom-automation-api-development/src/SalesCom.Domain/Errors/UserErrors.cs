namespace SalesCom.Domain.Errors;

using SalesCom.Domain.Common;

/// <summary>Outcome errors for the account/login flow. Field-level messages come from validators.</summary>
public static class UserErrors
{
    public static readonly ErrorBase InvalidCredentials = ErrorBase.Unauthorized(
        "User.InvalidCredentials", "Username or password is incorrect.");

    public static readonly ErrorBase NotActive = ErrorBase.Forbidden(
        "User.NotActive", "Your account is not active. Contact an administrator.");

    public static readonly ErrorBase Locked = ErrorBase.Forbidden(
        "User.Locked", "Your account is locked. Contact an administrator.");

    public static readonly ErrorBase NotFound = ErrorBase.NotFound(
        "User.NotFound", "The requested user was not found.");
}
