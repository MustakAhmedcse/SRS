namespace SalesCom.Application.Interfaces;

/// <summary>
/// The authenticated caller, read from the validated token's claims. Identity originates from the
/// central login <c>userInfo</c> captured at login time. Rights are not in the token — resolve them
/// from the DB via <see cref="IUserRightsQuery"/>.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The central user identifier (string). Empty when unauthenticated.</summary>
    string UserId { get; }

    /// <summary>Login / user name from the <c>UserName</c> claim.</summary>
    string UserName { get; }

    /// <summary>Email from the <c>Email</c> claim; <c>null</c> when absent.</summary>
    string? Email { get; }
}
