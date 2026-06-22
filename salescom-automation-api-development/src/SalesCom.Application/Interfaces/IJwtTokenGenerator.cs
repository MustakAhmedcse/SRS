namespace SalesCom.Application.Interfaces;

/// <summary>
/// Mints the access token for an authenticated user: an HS256-signed JWT wrapped inside an
/// A128KW/A128CBC_HS256 JWE envelope. Implementations must produce that exact shape so the
/// centralized validation middleware can decrypt → verify in one step.
/// </summary>
public interface IJwtTokenGenerator
{
    JwtToken Generate(LoginContext context);
}

/// <summary>
/// Identity snapshot stamped into our access token's claims: the central <see cref="UserId"/> (string)
/// and the user's <see cref="UserName"/> / <see cref="Email"/>. Granted rights are not in the token —
/// they travel in the session response and server-side enforcement is DB-backed.
/// </summary>
public sealed record LoginContext(
    string UserId,
    string UserName,
    string? Email);
