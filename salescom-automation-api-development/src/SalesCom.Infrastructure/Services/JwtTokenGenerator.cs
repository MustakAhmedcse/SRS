namespace SalesCom.Infrastructure.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using JoseJwt = Jose.JWT;
using JweAlgorithm = Jose.JweAlgorithm;
using JweEncryption = Jose.JweEncryption;
using SalesCom.Application.Interfaces;
using SalesCom.Domain.Interfaces;
using SalesCom.Infrastructure.Configurations;

/// <summary>
/// Mints an access token: an HS256-signed JWT wrapped inside an A128KW + A128CBC_HS256 JWE
/// envelope. Validation in <see cref="AuthenticationRegistration"/> reverses these two steps before
/// the principal is assembled. Claims emitted: <c>UserId</c> (central id), <c>UserName</c>, and
/// <c>Email</c>. Rights are not in the token — they travel in the session response.
/// </summary>
internal sealed class JwtTokenGenerator(IOptions<JwtConfiguration> options, IClock clock) : IJwtTokenGenerator
{
    private readonly JwtConfiguration _jwt = options.Value;

    public JwtToken Generate(LoginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var now = clock.UtcNow;
        var expires = now.AddMinutes(_jwt.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, context.UserId),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(_jwt.UserIdClaim, context.UserId),
            new(_jwt.UserNameClaim, context.UserName ?? string.Empty),
            new(_jwt.EmailClaim, context.Email ?? string.Empty),
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var signed = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var signedJwt = new JwtSecurityTokenHandler().WriteToken(signed);

        // JWE envelope: A128KW key wrap + A128CBC_HS256 content encryption.
        // Algorithms intentionally match the reference for client compatibility.
        var encryptionKey = Encoding.UTF8.GetBytes(_jwt.EncryptionKey);
        var encryptedJwt = JoseJwt.Encode(signedJwt, encryptionKey, JweAlgorithm.A128KW, JweEncryption.A128CBC_HS256);

        return new JwtToken(encryptedJwt, expires);
    }
}
