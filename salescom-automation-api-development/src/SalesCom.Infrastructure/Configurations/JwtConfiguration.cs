namespace SalesCom.Infrastructure.Configurations;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// JWT signing + JWE encryption parameters. The reference design wraps an HS256-signed JWT
/// inside an A128KW / A128CBC_HS256 JWE envelope; we keep both keys here so issuing and
/// validating use the same source of truth.
/// </summary>
public sealed record JwtConfiguration
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 signing key. Minimum 32 bytes (256 bits) for HMAC-SHA256.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>JWE key-wrap key. A128KW requires exactly 16 bytes (128 bits).</summary>
    [Required, MinLength(16, ErrorMessage = "Encryption key must be at least 16 bytes for A128KW.")]
    public string EncryptionKey { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(0, 600)]
    public int ClockSkewSeconds { get; init; } = 30;

    [Range(1, 10080)]
    public int AccessTokenLifetimeMinutes { get; init; } = 30;

    public bool RequireHttpsMetadata { get; init; } = true;

    /// <summary>
    /// When true, validates the issuer claim against <see cref="Issuer"/>. The reference disables
    /// this — we keep it on by default to prevent cross-issuer token replay.
    /// </summary>
    public bool ValidateIssuer { get; init; } = true;

    /// <summary>
    /// When true, validates the audience claim against <see cref="Audience"/>. Same reasoning as
    /// <see cref="ValidateIssuer"/>.
    /// </summary>
    public bool ValidateAudience { get; init; } = true;

    /// <summary>Claim type carrying the central user id (string).</summary>
    public string UserIdClaim { get; init; } = "UserId";

    public string UserNameClaim { get; init; } = "UserName";

    public string EmailClaim { get; init; } = "Email";
}
